using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     変更をスタッシュに保存するポップアップダイアログのViewModel。
    ///     全変更または選択された変更のみをスタッシュし、スタッシュ後の変更の扱い方を制御する。
    /// </summary>
    public class StashChanges : Popup
    {
        /// <summary>
        ///     スタッシュに付けるメッセージ。
        /// </summary>
        public string Message
        {
            get;
            set;
        }

        /// <summary>
        ///     個別のファイルが選択されているかどうか。
        /// </summary>
        public bool HasSelectedFiles
        {
            get => _changes != null;
        }

        /// <summary>
        ///     未追跡ファイルをスタッシュに含めるかどうか。
        /// </summary>
        public bool IncludeUntracked
        {
            get => _repo.UIStates.IncludeUntrackedWhenStash;
            set => _repo.UIStates.IncludeUntrackedWhenStash = value;
        }

        /// <summary>
        ///     ステージされた変更のみをスタッシュするかどうか。
        /// </summary>
        public bool OnlyStaged
        {
            get => _repo.UIStates.OnlyStagedWhenStash;
            set
            {
                if (_repo.UIStates.OnlyStagedWhenStash != value)
                {
                    _repo.UIStates.OnlyStagedWhenStash = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        ///     スタッシュ後の変更の扱い方（破棄/インデックス維持/全て維持）。
        /// </summary>
        public int ChangesAfterStashing
        {
            get => _repo.UIStates.ChangesAfterStashing;
            set => _repo.UIStates.ChangesAfterStashing = value;
        }

        /// <summary>
        ///     コンストラクタ。リポジトリと選択された変更ファイルリストを受け取る。
        /// </summary>
        public StashChanges(Repository repo, List<Models.Change> selectedChanges)
        {
            _repo = repo;
            _changes = selectedChanges;
        }

        /// <summary>
        ///     スタッシュ操作を実行する。選択ファイルの有無、OnlyStaged設定に応じて適切なスタッシュ方法を使用する。
        /// </summary>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Stash changes ...";

            var log = _repo.CreateLog("Stash Local Changes");
            Use(log);

            var mode = (DealWithChangesAfterStashing)ChangesAfterStashing;
            var keepIndex = mode == DealWithChangesAfterStashing.KeepIndex;
            bool succ;

            if (_changes == null)
            {
                if (OnlyStaged)
                {
                    if (Native.OS.GitVersion >= Models.GitVersions.STASH_PUSH_ONLY_STAGED)
                    {
                        succ = await new Commands.Stash(_repo.FullPath)
                            .Use(log)
                            .PushOnlyStagedAsync(Message, keepIndex);
                    }
                    else
                    {
                        var all = await new Commands.QueryLocalChanges(_repo.FullPath, false)
                            .Use(log)
                            .GetResultAsync();

                        var staged = new List<Models.Change>();
                        foreach (var c in all)
                        {
                            if (c.Index != Models.ChangeState.None && c.Index != Models.ChangeState.Untracked)
                                staged.Add(c);
                        }

                        succ = await StashWithChangesAsync(staged, keepIndex, log);
                    }
                }
                else
                {
                    succ = await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PushAsync(Message, IncludeUntracked, keepIndex);
                }
            }
            else
            {
                succ = await StashWithChangesAsync(_changes, keepIndex, log);
            }

            if (mode == DealWithChangesAfterStashing.KeepAll && succ)
                succ = await new Commands.Stash(_repo.FullPath)
                    .Use(log)
                    .ApplyAsync("stash@{0}", true);

            log.Complete();
            _repo.MarkWorkingCopyDirtyManually();
            _repo.MarkStashesDirtyManually();
            return succ;
        }

        /// <summary>
        ///     特定の変更ファイルのみをスタッシュする。
        ///     Gitバージョンに応じてpathspecfileまたはバッチ処理を使い分ける。
        /// </summary>
        private async Task<bool> StashWithChangesAsync(List<Models.Change> changes, bool keepIndex, CommandLog log)
        {
            if (changes.Count == 0)
                return true;

            var succ = false;
            if (Native.OS.GitVersion >= Models.GitVersions.STASH_PUSH_WITH_PATHSPECFILE)
            {
                var paths = new List<string>();
                foreach (var c in changes)
                    paths.Add(c.Path);

                var pathSpecFile = Path.GetTempFileName();
                await File.WriteAllLinesAsync(pathSpecFile, paths);
                succ = await new Commands.Stash(_repo.FullPath)
                    .Use(log)
                    .PushAsync(Message, pathSpecFile, keepIndex)
                    .ConfigureAwait(false);
                File.Delete(pathSpecFile);
            }
            else
            {
                for (int i = 0; i < changes.Count; i += 32)
                {
                    var count = Math.Min(32, changes.Count - i);
                    var step = changes.GetRange(i, count);
                    succ = await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PushAsync(Message, step, keepIndex)
                        .ConfigureAwait(false);
                    if (!succ)
                        break;
                }
            }

            return succ;
        }

        private enum DealWithChangesAfterStashing
        {
            Discard = 0,
            KeepIndex,
            KeepAll,
        }

        private readonly Repository _repo = null;
        private readonly List<Models.Change> _changes = null;
    }
}
