using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    /// リモートブランチからプルするダイアログのViewModel。
    /// リモート選択、ブランチ選択、リベースオプション、ローカル変更の扱いを管理する。
    /// プル前のスタッシュ自動保存・復元にも対応する。
    /// </summary>
    public class Pull : Popup
    {
        /// <summary>リモート一覧。</summary>
        public List<Models.Remote> Remotes => _repo.Remotes;
        /// <summary>現在のローカルブランチ。</summary>
        public Models.Branch Current { get; }

        /// <summary>
        /// 特定のリモートブランチが指定されて開かれたかどうか。
        /// </summary>
        public bool HasSpecifiedRemoteBranch
        {
            get;
            private set;
        }

        /// <summary>
        /// 選択されたリモート。変更時にリモートブランチ一覧を更新する。
        /// </summary>
        public Models.Remote SelectedRemote
        {
            get => _selectedRemote;
            set
            {
                if (SetProperty(ref _selectedRemote, value))
                    PostRemoteSelected();
            }
        }

        /// <summary>
        /// 選択されたリモートに属するブランチ一覧。
        /// </summary>
        public List<Models.Branch> RemoteBranches
        {
            get => _remoteBranches;
            private set => SetProperty(ref _remoteBranches, value);
        }

        /// <summary>
        /// プル元のリモートブランチ（必須入力）。
        /// </summary>
        [Required(ErrorMessage = "Remote branch to pull is required!!!")]
        public Models.Branch SelectedBranch
        {
            get => _selectedBranch;
            set => SetProperty(ref _selectedBranch, value, true);
        }

        /// <summary>
        /// ローカルの変更を破棄してからプルするかどうか。
        /// </summary>
        public bool DiscardLocalChanges
        {
            get;
            set;
        } = false;

        /// <summary>
        /// マージの代わりにリベースを使用するかどうか。リポジトリのUI状態と連動する。
        /// </summary>
        public bool UseRebase
        {
            get => _repo.UIStates.PreferRebaseInsteadOfMerge;
            set => _repo.UIStates.PreferRebaseInsteadOfMerge = value;
        }

        /// <summary>
        /// リポジトリとオプションのリモートブランチを指定してダイアログを初期化する。
        /// 指定されたリモートブランチがある場合はそれを選択し、
        /// ない場合はアップストリームまたはデフォルトリモートから自動選択する。
        /// </summary>
        public Pull(Repository repo, Models.Branch specifiedRemoteBranch)
        {
            _repo = repo;
            Current = repo.CurrentBranch;

            if (specifiedRemoteBranch != null)
            {
                // 特定のリモートブランチが指定された場合
                _selectedRemote = repo.Remotes.Find(x => x.Name == specifiedRemoteBranch.Remote);
                _selectedBranch = specifiedRemoteBranch;

                var branches = new List<Models.Branch>();
                foreach (var branch in _repo.Branches)
                {
                    if (branch.Remote == specifiedRemoteBranch.Remote)
                        branches.Add(branch);
                }

                _remoteBranches = branches;
                HasSpecifiedRemoteBranch = true;
            }
            else
            {
                // アップストリームからリモート名を抽出して自動選択を試みる
                Models.Remote autoSelectedRemote = null;
                if (Current.Upstream is { Length: > 13 } && Current.Upstream.StartsWith("refs/remotes/"))
                {
                    var remoteNameEndIdx = Current.Upstream.IndexOf('/', 13);
                    if (remoteNameEndIdx > 0)
                    {
                        var remoteName = Current.Upstream.Substring(13, remoteNameEndIdx - 13);
                        autoSelectedRemote = _repo.Remotes.Find(x => x.Name == remoteName);
                    }
                }

                // 自動選択できない場合はデフォルトリモートまたは最初のリモートを使用
                if (autoSelectedRemote == null)
                {
                    Models.Remote remote = null;
                    if (!string.IsNullOrEmpty(_repo.Settings.DefaultRemote))
                        remote = _repo.Remotes.Find(x => x.Name == _repo.Settings.DefaultRemote);
                    _selectedRemote = remote ?? (_repo.Remotes.Count > 0 ? _repo.Remotes[0] : null);
                }
                else
                {
                    _selectedRemote = autoSelectedRemote;
                }

                PostRemoteSelected();
                HasSpecifiedRemoteBranch = false;
            }
        }

        /// <summary>
        /// プルを実行する。
        /// ローカル変更がある場合は自動スタッシュまたは破棄を行い、
        /// プル成功後はサブモジュール更新とスタッシュの復元を行う。
        /// </summary>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();

            var log = _repo.CreateLog("Pull");
            Use(log);

            // ローカルの変更数を確認
            var changes = await new Commands.CountLocalChanges(_repo.FullPath, false).GetResultAsync();
            var needPopStash = false;
            if (changes > 0)
            {
                if (DiscardLocalChanges)
                {
                    // ローカル変更を破棄
                    await Commands.Discard.AllAsync(_repo.FullPath, false, false, log);
                }
                else
                {
                    // 自動スタッシュでローカル変更を一時保存
                    var succ = await new Commands.Stash(_repo.FullPath).Use(log).PushAsync("PULL_AUTO_STASH", false);
                    if (!succ)
                    {
                        log.Complete();
                        return false;
                    }

                    needPopStash = true;
                }
            }

            // git pullコマンドを実行（アップストリームと同じ場合はブランチ名省略）
            bool rs = await new Commands.Pull(
                _repo.FullPath,
                _selectedRemote.Name,
                !string.IsNullOrEmpty(Current.Upstream) && Current.Upstream.Equals(_selectedBranch.FullName) ? string.Empty : _selectedBranch.Name,
                UseRebase).Use(log).RunAsync();
            if (rs)
            {
                // プル成功時はサブモジュールの自動更新
                await _repo.AutoUpdateSubmodulesAsync(log);

                // 自動スタッシュを復元
                if (needPopStash)
                    await new Commands.Stash(_repo.FullPath).Use(log).PopAsync("stash@{0}");
            }

            log.Complete();

            // 履歴ビュー表示中の場合、HEADに移動
            if (_repo.SelectedViewIndex == 0)
            {
                var head = await new Commands.QueryRevisionByRefName(_repo.FullPath, "HEAD").GetResultAsync();
                _repo.NavigateToCommit(head, true);
            }

            return rs;
        }

        /// <summary>
        /// リモート選択後にリモートブランチ一覧を更新し、
        /// アップストリームまたは同名のブランチを自動選択する。
        /// </summary>
        private void PostRemoteSelected()
        {
            var remoteName = _selectedRemote.Name;
            var branches = new List<Models.Branch>();
            foreach (var branch in _repo.Branches)
            {
                if (branch.Remote == remoteName)
                    branches.Add(branch);
            }

            RemoteBranches = branches;

            var autoSelectedBranch = false;
            if (!string.IsNullOrEmpty(Current.Upstream) &&
                Current.Upstream.StartsWith($"refs/remotes/{remoteName}/", System.StringComparison.Ordinal))
            {
                foreach (var branch in branches)
                {
                    if (Current.Upstream == branch.FullName)
                    {
                        SelectedBranch = branch;
                        autoSelectedBranch = true;
                        break;
                    }
                }
            }

            if (!autoSelectedBranch)
            {
                foreach (var branch in branches)
                {
                    if (Current.Name == branch.Name)
                    {
                        SelectedBranch = branch;
                        autoSelectedBranch = true;
                        break;
                    }
                }
            }

            if (!autoSelectedBranch)
                SelectedBranch = null;
        }

        /// <summary>対象リポジトリ</summary>
        private readonly Repository _repo = null;
        /// <summary>選択されたリモート</summary>
        private Models.Remote _selectedRemote = null;
        /// <summary>リモートブランチ一覧</summary>
        private List<Models.Branch> _remoteBranches = null;
        /// <summary>選択されたリモートブランチ</summary>
        private Models.Branch _selectedBranch = null;
    }
}
