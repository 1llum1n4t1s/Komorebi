using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>インタラクティブリベースの事前設定（特定コミットのアクションを指定）。</summary>
    public record InteractiveRebasePrefill(string SHA, Models.InteractiveRebaseAction Action);

    /// <summary>
    ///     インタラクティブリベースの個々のコミットアイテム。
    ///     pick/squash/fixup/reword/drop/editのアクションを管理する。
    /// </summary>
    public class InteractiveRebaseItem : ObservableObject
    {
        /// <summary>元の並び順（逆順インデックス）。</summary>
        public int OriginalOrder
        {
            get;
        }

        /// <summary>対象のコミット。</summary>
        public Models.Commit Commit
        {
            get;
        }

        /// <summary>このコミットに対するアクション（Pick/Squash/Fixup/Reword/Drop/Edit）。</summary>
        public Models.InteractiveRebaseAction Action
        {
            get => _action;
            set => SetProperty(ref _action, value);
        }

        /// <summary>Squash/Fixupチェーンにおけるこのアイテムの保留状態。</summary>
        public Models.InteractiveRebasePendingType PendingType
        {
            get => _pendingType;
            set => SetProperty(ref _pendingType, value);
        }

        /// <summary>コミットメッセージの件名行（1行目）。</summary>
        public string Subject
        {
            get => _subject;
            private set => SetProperty(ref _subject, value);
        }

        /// <summary>コミットメッセージの全文。変更時に件名も自動更新する。</summary>
        public string FullMessage
        {
            get => _fullMessage;
            set
            {
                if (SetProperty(ref _fullMessage, value))
                {
                    var normalized = value.ReplaceLineEndings("\n");
                    var parts = normalized.Split("\n\n", 2);
                    Subject = parts[0].ReplaceLineEndings(" ");
                }
            }
        }

        /// <summary>元のコミットメッセージ全文（リセット用に保持）。</summary>
        public string OriginalFullMessage
        {
            get;
            set;
        }

        /// <summary>Squash/Fixupアクションが選択可能かどうか（先頭アイテムは不可）。</summary>
        public bool CanSquashOrFixup
        {
            get => _canSquashOrFixup;
            set => SetProperty(ref _canSquashOrFixup, value);
        }

        /// <summary>メッセージ編集ボタンを表示するかどうか。</summary>
        public bool ShowEditMessageButton
        {
            get => _showEditMessageButton;
            set => SetProperty(ref _showEditMessageButton, value);
        }

        /// <summary>このアイテムのメッセージがリベース結果に使用されるかどうか。</summary>
        public bool IsFullMessageUsed
        {
            get => _isFullMessageUsed;
            set => SetProperty(ref _isFullMessageUsed, value);
        }

        /// <summary>ドラッグ＆ドロップ時の方向インジケータ。</summary>
        public Thickness DropDirectionIndicator
        {
            get => _dropDirectionIndicator;
            set => SetProperty(ref _dropDirectionIndicator, value);
        }

        /// <summary>ユーザーがメッセージを手動編集したかどうか。</summary>
        public bool IsMessageUserEdited
        {
            get;
            set;
        } = false;

        /// <summary>コンストラクタ。順序、コミット、メッセージを指定して初期化する。</summary>
        public InteractiveRebaseItem(int order, Models.Commit c, string message)
        {
            OriginalOrder = order;
            Commit = c;
            FullMessage = message;
            OriginalFullMessage = message;
        }

        private Models.InteractiveRebaseAction _action = Models.InteractiveRebaseAction.Pick;
        private Models.InteractiveRebasePendingType _pendingType = Models.InteractiveRebasePendingType.None;
        private string _subject;
        private string _fullMessage;
        private bool _canSquashOrFixup = true;
        private bool _showEditMessageButton = false;
        private bool _isFullMessageUsed = true;
        private Thickness _dropDirectionIndicator = new Thickness(0);
    }

    /// <summary>
    ///     インタラクティブリベースのセッション全体を管理するViewModel。
    ///     コミットリストの読み込み、アクション変更、並び替え、リベースの実行を行う。
    /// </summary>
    public class InteractiveRebase : ObservableObject
    {
        /// <summary>現在のブランチ。</summary>
        public Models.Branch Current
        {
            get;
            private set;
        }

        /// <summary>リベースの基点となるコミット（onto）。</summary>
        public Models.Commit On
        {
            get;
        }

        /// <summary>リベース前に自動的にstashを行うかどうか。</summary>
        public bool AutoStash
        {
            get;
            set;
        } = true;

        /// <summary>リポジトリのイシュートラッカー一覧。</summary>
        public AvaloniaList<Models.IssueTracker> IssueTrackers
        {
            get => _repo.IssueTrackers;
        }

        /// <summary>コンベンショナルコミットタイプのオーバーライド設定。</summary>
        public string ConventionalTypesOverride
        {
            get => _repo.Settings.ConventionalTypesOverride;
        }

        /// <summary>コミット一覧を読み込み中かどうか。</summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>リベース対象のコミットアイテム一覧。</summary>
        public AvaloniaList<InteractiveRebaseItem> Items
        {
            get;
        } = [];

        /// <summary>初期選択されるアイテム。</summary>
        public InteractiveRebaseItem PreSelected
        {
            get => _preSelected;
            private set => SetProperty(ref _preSelected, value);
        }

        /// <summary>選択中コミットの詳細情報（CommitDetailまたはCount）。</summary>
        public object Detail
        {
            get => _detail;
            private set => SetProperty(ref _detail, value);
        }

        /// <summary>
        ///     コンストラクタ。バックグラウンドでリベース対象のコミット一覧を取得し、
        ///     prefillが指定されている場合はそのコミットのアクションを事前設定する。
        /// </summary>
        public InteractiveRebase(Repository repo, Models.Commit on, InteractiveRebasePrefill prefill = null)
        {
            _repo = repo;
            _commitDetail = new CommitDetail(repo, null);
            Current = repo.CurrentBranch;
            On = on;
            IsLoading = true;

            Task.Run(async () =>
            {
                var commits = await new Commands.QueryCommitsForInteractiveRebase(_repo.FullPath, on.SHA)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                var list = new List<InteractiveRebaseItem>();
                for (var i = 0; i < commits.Count; i++)
                {
                    var c = commits[i];
                    list.Add(new InteractiveRebaseItem(commits.Count - i, c.Commit, c.Message));
                }

                var selected = list.Count > 0 ? list[0] : null;
                if (prefill != null)
                {
                    var item = list.Find(x => x.Commit.SHA.Equals(prefill.SHA, StringComparison.Ordinal));
                    if (item != null)
                    {
                        item.Action = prefill.Action;
                        selected = item;
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    Items.AddRange(list);
                    UpdateItems();
                    PreSelected = selected;
                    IsLoading = false;
                });
            });
        }

        /// <summary>
        ///     コミット選択時の処理。選択数に応じて詳細表示を切り替える。
        /// </summary>
        public void SelectCommits(List<InteractiveRebaseItem> items)
        {
            if (items.Count == 0)
            {
                Detail = null;
            }
            else if (items.Count == 1)
            {
                _commitDetail.Commit = items[0].Commit;
                Detail = _commitDetail;
            }
            else
            {
                Detail = new Models.Count(items.Count);
            }
        }

        /// <summary>
        ///     選択されたアイテムのアクションを変更する。
        ///     Squash/Fixupは条件を満たすアイテムのみ適用される。
        /// </summary>
        public void ChangeAction(List<InteractiveRebaseItem> selected, Models.InteractiveRebaseAction action)
        {
            if (action == Models.InteractiveRebaseAction.Squash || action == Models.InteractiveRebaseAction.Fixup)
            {
                foreach (var item in selected)
                {
                    if (item.CanSquashOrFixup)
                        item.Action = action;
                }
            }
            else
            {
                foreach (var item in selected)
                    item.Action = action;
            }

            UpdateItems();
        }

        /// <summary>
        ///     選択されたコミットを指定位置に移動する（ドラッグ＆ドロップ対応）。
        /// </summary>
        public void Move(List<InteractiveRebaseItem> commits, int index)
        {
            var hashes = new HashSet<string>();
            foreach (var c in commits)
                hashes.Add(c.Commit.SHA);

            var before = new List<InteractiveRebaseItem>();
            var ordered = new List<InteractiveRebaseItem>();
            var after = new List<InteractiveRebaseItem>();

            for (int i = 0; i < index; i++)
            {
                var item = Items[i];
                if (!hashes.Contains(item.Commit.SHA))
                    before.Add(item);
                else
                    ordered.Add(item);
            }

            for (int i = index; i < Items.Count; i++)
            {
                var item = Items[i];
                if (!hashes.Contains(item.Commit.SHA))
                    after.Add(item);
                else
                    ordered.Add(item);
            }

            Items.Clear();
            Items.AddRange(before);
            Items.AddRange(ordered);
            Items.AddRange(after);
            UpdateItems();
        }

        /// <summary>
        ///     インタラクティブリベースを実行する。
        ///     ジョブ情報をJSONファイルに書き出してからgit rebaseコマンドを実行する。
        /// </summary>
        public async Task<bool> Start()
        {
            using var lockWatcher = _repo.LockWatcher();

            var saveFile = Path.Combine(_repo.GitDir, "komorebi.interactive_rebase");
            var collection = new Models.InteractiveRebaseJobCollection();
            collection.OrigHead = _repo.CurrentBranch.Head;
            collection.Onto = On.SHA;

            // 逆順にジョブを構築（gitのリベース順序に合わせる）
            InteractiveRebaseItem pending = null;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                var job = new Models.InteractiveRebaseJob()
                {
                    SHA = item.Commit.SHA,
                    Action = item.Action,
                };

                if (pending != null && item.PendingType != Models.InteractiveRebasePendingType.Ignore)
                    job.Message = pending.FullMessage;
                else
                    job.Message = item.FullMessage;

                collection.Jobs.Add(job);

                if (item.PendingType == Models.InteractiveRebasePendingType.Last)
                    pending = null;
                else if (item.PendingType == Models.InteractiveRebasePendingType.Target)
                    pending = item;
            }

            await using (var stream = File.Create(saveFile))
            {
                await JsonSerializer.SerializeAsync(stream, collection, JsonCodeGen.Default.InteractiveRebaseJobCollection);
            }

            var log = _repo.CreateLog("Interactive Rebase");
            var succ = await new Commands.InteractiveRebase(_repo.FullPath, On.SHA, AutoStash)
                .Use(log)
                .ExecAsync();

            log.Complete();
            return succ;
        }

        /// <summary>
        ///     全アイテムの状態を再計算する。
        ///     Squash/Fixupの連鎖、メッセージの結合、CanSquashOrFixupフラグを更新する。
        /// </summary>
        private void UpdateItems()
        {
            if (Items.Count == 0)
                return;

            // 逆順にスキャンして、Squash/Fixup可能かどうかを判定
            var hasValidParent = false;
            for (var i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                if (hasValidParent)
                {
                    item.CanSquashOrFixup = true;
                }
                else
                {
                    item.CanSquashOrFixup = false;
                    if (item.Action == Models.InteractiveRebaseAction.Squash || item.Action == Models.InteractiveRebaseAction.Fixup)
                        item.Action = Models.InteractiveRebaseAction.Pick;

                    hasValidParent = item.Action != Models.InteractiveRebaseAction.Drop;
                }
            }

            // 順方向にスキャンしてSquash/Fixupチェーンのメッセージ結合を処理
            var hasPending = false;
            var pendingMessages = new List<string>();
            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];

                if (item.Action == Models.InteractiveRebaseAction.Drop)
                {
                    item.IsFullMessageUsed = false;
                    item.ShowEditMessageButton = false;
                    item.PendingType = hasPending ? Models.InteractiveRebasePendingType.Ignore : Models.InteractiveRebasePendingType.None;
                    item.FullMessage = item.OriginalFullMessage;
                    item.IsMessageUserEdited = false;
                    continue;
                }

                if (item.Action == Models.InteractiveRebaseAction.Fixup ||
                    item.Action == Models.InteractiveRebaseAction.Squash)
                {
                    item.IsFullMessageUsed = false;
                    item.ShowEditMessageButton = false;
                    item.PendingType = hasPending ? Models.InteractiveRebasePendingType.Pending : Models.InteractiveRebasePendingType.Last;
                    item.FullMessage = item.OriginalFullMessage;
                    item.IsMessageUserEdited = false;

                    if (item.Action == Models.InteractiveRebaseAction.Squash)
                        pendingMessages.Add(item.OriginalFullMessage);

                    hasPending = true;
                    continue;
                }

                if (item.Action == Models.InteractiveRebaseAction.Reword ||
                    item.Action == Models.InteractiveRebaseAction.Edit)
                {
                    var oldPendingType = item.PendingType;
                    item.IsFullMessageUsed = true;
                    item.ShowEditMessageButton = true;
                    item.PendingType = hasPending ? Models.InteractiveRebasePendingType.Target : Models.InteractiveRebasePendingType.None;

                    if (hasPending)
                    {
                        if (!item.IsMessageUserEdited)
                        {
                            var builder = new StringBuilder();
                            builder.Append(item.OriginalFullMessage);
                            for (var j = pendingMessages.Count - 1; j >= 0; j--)
                                builder.Append("\n").Append(pendingMessages[j]);

                            item.FullMessage = builder.ToString();
                        }

                        hasPending = false;
                        pendingMessages.Clear();
                    }
                    else if (oldPendingType == Models.InteractiveRebasePendingType.Target)
                    {
                        if (!item.IsMessageUserEdited)
                            item.FullMessage = item.OriginalFullMessage;
                    }

                    continue;
                }

                if (item.Action == Models.InteractiveRebaseAction.Pick)
                {
                    item.IsFullMessageUsed = true;
                    item.IsMessageUserEdited = false;

                    if (hasPending)
                    {
                        var builder = new StringBuilder();
                        builder.Append(item.OriginalFullMessage);
                        for (var j = pendingMessages.Count - 1; j >= 0; j--)
                            builder.Append("\n").Append(pendingMessages[j]);

                        item.Action = Models.InteractiveRebaseAction.Reword;
                        item.PendingType = Models.InteractiveRebasePendingType.Target;
                        item.ShowEditMessageButton = true;
                        item.FullMessage = builder.ToString();

                        hasPending = false;
                        pendingMessages.Clear();
                    }
                    else
                    {
                        item.PendingType = Models.InteractiveRebasePendingType.None;
                        item.ShowEditMessageButton = false;
                        item.FullMessage = item.OriginalFullMessage;
                    }
                }
            }
        }

        private Repository _repo = null;
        private bool _isLoading = false;
        private InteractiveRebaseItem _preSelected = null;
        private object _detail = null;
        private CommitDetail _commitDetail = null;
    }
}
