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

namespace Komorebi.ViewModels;

/// <summary>
/// 対話的リベースの事前設定レコード。特定コミットに対するアクションを指定する。
/// </summary>
public record InteractiveRebasePrefill(string SHA, Models.InteractiveRebaseAction Action);

/// <summary>
/// fixup!/squash! コミットの並び替えキューに積むレコード。
/// 同じキー（親コミット Subject）を持つ複数エントリを許容するため、Dictionary ではなく List で管理する。
/// </summary>
public record InteractiveRebaseReorderItem(string Key, InteractiveRebaseItem Item);

/// <summary>
/// 対話的リベースの各コミット項目ViewModel。
/// アクション（pick/reword/edit/squash/fixup/drop）とメッセージ編集状態を管理する。
/// </summary>
public class InteractiveRebaseItem : ObservableObject
{
    /// <summary>元の並び順（逆順にカウント）。</summary>
    public int OriginalOrder
    {
        get;
    }

    /// <summary>対象コミット。</summary>
    public Models.Commit Commit
    {
        get;
    }

    /// <summary>このコミットに対するリベースアクション。</summary>
    public Models.InteractiveRebaseAction Action
    {
        get => _action;
        set => SetProperty(ref _action, value);
    }

    /// <summary>squash/fixupのメッセージ結合状態。</summary>
    public Models.InteractiveRebasePendingType PendingType
    {
        get => _pendingType;
        set => SetProperty(ref _pendingType, value);
    }

    /// <summary>コミットの件名行（1行目）。</summary>
    public string Subject
    {
        get => _subject;
        private set => SetProperty(ref _subject, value);
    }

    /// <summary>コミットの完全なメッセージ。変更時に件名を自動抽出する。</summary>
    public string FullMessage
    {
        get => _fullMessage;
        set
        {
            if (SetProperty(ref _fullMessage, value))
            {
                // メッセージから件名行を抽出（空行で分割して最初の部分）
                var normalized = value.ReplaceLineEndings("\n");
                var parts = normalized.Split("\n\n", 2);
                Subject = parts[0].ReplaceLineEndings(" ");
            }
        }
    }

    /// <summary>元のコミットメッセージ（リセット用）。</summary>
    public string OriginalFullMessage
    {
        get;
        set;
    }

    /// <summary>squashまたはfixupアクションが設定可能かどうか。</summary>
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

    /// <summary>ドラッグ＆ドロップ時の挿入位置インジケーター。</summary>
    public Thickness DropDirectionIndicator
    {
        get => _dropDirectionIndicator;
        set => SetProperty(ref _dropDirectionIndicator, value);
    }

    /// <summary>ユーザーがメッセージを手動編集済みかどうか。</summary>
    public bool IsMessageUserEdited
    {
        get;
        set;
    } = false;

    /// <summary>
    /// コンストラクタ。元の順序、コミット、メッセージを指定する。
    /// </summary>
    public InteractiveRebaseItem(int order, Models.Commit c, string message)
    {
        OriginalOrder = order;
        Commit = c;
        FullMessage = message;
        OriginalFullMessage = message;
    }

    private Models.InteractiveRebaseAction _action = Models.InteractiveRebaseAction.Pick; // リベースアクション
    private Models.InteractiveRebasePendingType _pendingType = Models.InteractiveRebasePendingType.None; // メッセージ結合状態
    private string _subject; // 件名行
    private string _fullMessage; // 完全なメッセージ
    private bool _canSquashOrFixup = true; // squash/fixup可否
    private bool _showEditMessageButton = false; // メッセージ編集ボタン表示
    private Thickness _dropDirectionIndicator = new Thickness(0); // ドロップ位置インジケーター
}

/// <summary>
/// 対話的リベース画面のメインViewModel。
/// コミットの並び替え、アクション変更、メッセージ編集を行い、リベースを実行する。
/// </summary>
public class InteractiveRebase : ObservableObject
{
    /// <summary>現在のブランチ。</summary>
    public Models.Branch Current
    {
        get;
        private set;
    }

    /// <summary>リベースの起点コミット（onto）。</summary>
    public Models.Commit On
    {
        get;
    }

    /// <summary>リベース前にstashを自動実行するかどうか。</summary>
    public bool AutoStash
    {
        get;
        set;
    } = true;

    /// <summary>リポジトリに設定されたイシュートラッカー一覧。</summary>
    public AvaloniaList<Models.IssueTracker> IssueTrackers
    {
        get => _repo.IssueTrackers;
    }

    /// <summary>Conventional Commitsのタイプ上書き設定。</summary>
    public string ConventionalTypesOverride
    {
        get => _repo.Settings.ConventionalTypesOverride;
    }

    /// <summary>コミット一覧の読み込み中かどうか。</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>リベース対象のコミット項目リスト。</summary>
    public AvaloniaList<InteractiveRebaseItem> Items
    {
        get;
    } = [];

    /// <summary>初期選択されるコミット項目。</summary>
    public InteractiveRebaseItem PreSelected
    {
        get => _preSelected;
        private set => SetProperty(ref _preSelected, value);
    }

    /// <summary>詳細パネルのコンテンツ（コミット詳細またはカウント表示）。</summary>
    public object Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    /// <summary>
    /// コンストラクタ。リポジトリ、起点コミット、事前設定を指定する。
    /// バックグラウンドで対象コミット一覧を取得する。
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

            // fixup!/squash! コミットを対応する親コミットの直後に移動する（upstream e6ba0534 + 0d5185b1 + 1ca4145e）
            // 同じ親 Subject を持つ複数 fixup/squash に対応するため List<Record> で保持する（Dictionary では無限ループが発生）
            List<InteractiveRebaseItem> list = [];
            List<InteractiveRebaseReorderItem> needReorder = [];
            for (var i = 0; i < commits.Count; i++)
            {
                var c = commits[i];
                var item = new InteractiveRebaseItem(commits.Count - i, c.Commit, c.Message);
                var subject = c.Commit.Subject;

                if (item.OriginalOrder > 1)
                {
                    if (subject.StartsWith("fixup! ", StringComparison.Ordinal))
                    {
                        item.Action = Models.InteractiveRebaseAction.Fixup;
                        needReorder.Add(new(subject.Substring(7), item));
                        continue;
                    }

                    if (subject.StartsWith("squash! ", StringComparison.Ordinal))
                    {
                        item.Action = Models.InteractiveRebaseAction.Squash;
                        needReorder.Add(new(subject.Substring(8), item));
                        continue;
                    }
                }

                // 対象となる親コミットが見つかった fixup!/squash! を直後に挿入する
                List<InteractiveRebaseReorderItem> reordered = [];
                foreach (var o in needReorder)
                {
                    if (subject.StartsWith(o.Key, StringComparison.Ordinal))
                    {
                        list.Add(o.Item);
                        reordered.Add(o);
                    }
                }

                foreach (var k in reordered)
                    needReorder.Remove(k);

                list.Add(item);
            }

            // 親が見つからなかった fixup!/squash! は元の順序位置に戻し、安全のため Pick にリセットする
            foreach (var v in needReorder)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (v.Item.OriginalOrder > list[i].OriginalOrder)
                    {
                        v.Item.Action = Models.InteractiveRebaseAction.Pick;
                        list.Insert(i, v.Item);
                        break;
                    }
                }
            }

            var selected = list.Count > 0 ? list[0] : null;
            if (prefill is not null)
            {
                var item = list.Find(x => x.Commit.SHA.Equals(prefill.SHA, StringComparison.Ordinal));
                if (item is not null)
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
    /// コミットの選択状態を処理する。0件で詳細クリア、1件でコミット詳細、複数でカウント表示。
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
    /// 選択されたコミット項目のアクションを一括変更する。
    /// squash/fixupはCanSquashOrFixupがtrueの項目のみ変更される。
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
    /// 選択されたコミット項目を指定インデックスに移動する（ドラッグ＆ドロップ用）。
    /// </summary>
    public void Move(List<InteractiveRebaseItem> commits, int index)
    {
        HashSet<string> hashes = [];
        foreach (var c in commits)
            hashes.Add(c.Commit.SHA);

        List<InteractiveRebaseItem> before = [];
        List<InteractiveRebaseItem> ordered = [];
        List<InteractiveRebaseItem> after = [];

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
    /// 対話的リベースを実行する。
    /// 各コミットのアクション・メッセージをJSONファイルに保存し、git rebase -iを起動する。
    /// </summary>
    public async Task<bool> Start()
    {
        using var lockWatcher = _repo.LockWatcher();

        var saveFile = Path.Combine(_repo.GitDir, "sourcegit.interactive_rebase");
        var collection = new Models.InteractiveRebaseJobCollection();
        collection.OrigHead = _repo.CurrentBranch.Head;
        collection.Onto = On.SHA;

        InteractiveRebaseItem pending = null;
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            var item = Items[i];
            var job = new Models.InteractiveRebaseJob()
            {
                SHA = item.Commit.SHA,
                Action = item.Action,
            };

            if (pending is not null && item.PendingType != Models.InteractiveRebasePendingType.Ignore)
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
    /// 全項目のsquash/fixup可否・メッセージ結合状態・表示ボタンを再計算する。
    /// コミット順序やアクション変更後に呼び出す。
    /// </summary>
    private void UpdateItems()
    {
        if (Items.Count == 0)
            return;

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

        var hasPending = false;
        List<string> pendingMessages = [];
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];

            if (item.Action == Models.InteractiveRebaseAction.Drop)
            {
                item.ShowEditMessageButton = false;
                item.PendingType = hasPending ? Models.InteractiveRebasePendingType.Ignore : Models.InteractiveRebasePendingType.None;
                item.FullMessage = item.OriginalFullMessage;
                item.IsMessageUserEdited = false;
                continue;
            }

            if (item.Action == Models.InteractiveRebaseAction.Fixup ||
                item.Action == Models.InteractiveRebaseAction.Squash)
            {
                item.ShowEditMessageButton = false;
                item.PendingType = hasPending ? Models.InteractiveRebasePendingType.Pending : Models.InteractiveRebasePendingType.Last;
                item.FullMessage = item.OriginalFullMessage;
                item.IsMessageUserEdited = false;

                if (item.Action == Models.InteractiveRebaseAction.Squash)
                {
                    // squash! プレフィックス付きのコミットは本文（2行目以降）だけを pending に積む（upstream 0d5185b1）
                    if (item.OriginalFullMessage.StartsWith("squash! ", StringComparison.Ordinal))
                    {
                        var firstLineEnd = item.OriginalFullMessage.IndexOf('\n');
                        if (firstLineEnd > 0)
                            pendingMessages.Add(item.OriginalFullMessage.Substring(firstLineEnd + 1));
                    }
                    else
                    {
                        pendingMessages.Add(item.OriginalFullMessage);
                    }
                }

                hasPending = true;
                continue;
            }

            if (item.Action == Models.InteractiveRebaseAction.Reword ||
                item.Action == Models.InteractiveRebaseAction.Edit)
            {
                var oldPendingType = item.PendingType;
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

    private Repository _repo = null; // 対象リポジトリ
    private bool _isLoading = false; // 読み込み中フラグ
    private InteractiveRebaseItem _preSelected = null; // 初期選択項目
    private object _detail = null; // 詳細パネルコンテンツ
    private CommitDetail _commitDetail = null; // コミット詳細VM（再利用）
}
