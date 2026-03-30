using System;
using System.Collections.Generic;

namespace Komorebi.ViewModels;

/// <summary>
/// リポジトリコマンドパレットの個々のコマンドを表すクラス。
/// ラベル、検索キーワード、アイコン、実行アクションを保持する。
/// </summary>
public class RepositoryCommandPaletteCmd
{
    /// <summary>表示ラベル（ローカライズ済みテキスト）。</summary>
    public string Label { get; set; }
    /// <summary>検索用キーワード。</summary>
    public string Keyword { get; set; }
    /// <summary>アイコンリソース名。</summary>
    public string Icon { get; set; }
    /// <summary>実行前にパレットを閉じるかどうか。サブパレットの場合はfalse。</summary>
    public bool CloseBeforeExec { get; set; }
    /// <summary>コマンド実行時のアクション。</summary>
    public Action Action { get; set; }

    /// <summary>直接実行型コマンドのコンストラクタ。実行前にパレットを閉じる。</summary>
    public RepositoryCommandPaletteCmd(string labelKey, string keyword, string icon, Action action)
    {
        Label = $"{App.Text(labelKey)}...";
        Keyword = keyword;
        Icon = icon;
        CloseBeforeExec = true;
        Action = action;
    }

    /// <summary>サブコマンドパレット型コマンドのコンストラクタ。パレットを閉じずに子パレットを開く。</summary>
    public RepositoryCommandPaletteCmd(string labelKey, string keyword, string icon, ICommandPalette child)
    {
        Label = $"{App.Text(labelKey)}...";
        Keyword = keyword;
        Icon = icon;
        CloseBeforeExec = false;
        Action = () => child.Open();
    }
}

/// <summary>
/// リポジトリ操作用のコマンドパレットViewModel。
/// Blame、Checkout、Merge等のサブパレットや、Fetch、Push等の直接コマンドを提供する。
/// </summary>
public class RepositoryCommandPalette : ICommandPalette
{
    /// <summary>フィルタ適用後の表示対象コマンド一覧。</summary>
    public List<RepositoryCommandPaletteCmd> VisibleCmds
    {
        get => _visibleCmds;
        private set => SetProperty(ref _visibleCmds, value);
    }

    /// <summary>現在選択中のコマンド。</summary>
    public RepositoryCommandPaletteCmd SelectedCmd
    {
        get => _selectedCmd;
        set => SetProperty(ref _selectedCmd, value);
    }

    /// <summary>検索フィルタ文字列。変更時に表示リストを更新する。</summary>
    public string Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
                UpdateVisible();
        }
    }

    /// <summary>
    /// コンストラクタ。サブコマンドパレット（Blame、Checkout等）と
    /// 直接実行コマンド（Fetch、Push等）を登録し、ラベル順にソートする。
    /// </summary>
    public RepositoryCommandPalette(Repository repo)
    {
        // サブコマンドパレット（選択後に子パレットが開く）
        _cmds.Add(new("Blame", "blame", "Blame", new BlameCommandPalette(repo.FullPath)));
        _cmds.Add(new("Checkout", "checkout", "Check", new CheckoutCommandPalette(repo)));
        _cmds.Add(new("Compare.WithHead", "compare", "Compare", new CompareCommandPalette(repo, null)));
        _cmds.Add(new("FileHistory", "history", "Histories", new FileHistoryCommandPalette(repo.FullPath)));
        _cmds.Add(new("Merge", "merge", "Merge", new MergeCommandPalette(repo)));
        _cmds.Add(new("OpenFile", "open", "OpenWith", new OpenFileCommandPalette(repo.FullPath)));
        _cmds.Add(new("Repository.CustomActions", "custom actions", "Action", new ExecuteCustomActionCommandPalette(repo)));

        // 直接実行コマンド
        _cmds.Add(new("Repository.NewBranch", "create branch", "Branch.Add", () => repo.CreateNewBranch()));
        _cmds.Add(new("CreateTag.Title", "create tag", "Tag.Add", () => repo.CreateNewTag()));
        _cmds.Add(new("Fetch", "fetch", "Fetch", async () => await repo.FetchAsync(false)));
        _cmds.Add(new("Pull.Title", "pull", "Pull", async () => await repo.PullAsync(false)));
        _cmds.Add(new("Push", "push", "Push", async () => await repo.PushAsync(false)));
        _cmds.Add(new("Stash.Title", "stash", "Stashes.Add", async () => await repo.StashAllAsync(false)));
        _cmds.Add(new("Apply.Title", "apply", "Diff", () => repo.ApplyPatch()));
        _cmds.Add(new("Configure", "configure", "Settings", async () => await App.ShowDialog(new RepositoryConfigure(repo))));

        // ラベルのアルファベット順にソート
        _cmds.Sort((l, r) => l.Label.CompareTo(r.Label));
        _visibleCmds = _cmds;
        _selectedCmd = _cmds[0];
    }

    /// <summary>検索フィルタをクリアする。</summary>
    public void ClearFilter()
    {
        Filter = string.Empty;
    }

    /// <summary>
    /// 選択中のコマンドを実行する。
    /// CloseBeforeExecがtrueの場合は実行前にパレットを閉じる。
    /// </summary>
    public void Exec()
    {
        _cmds.Clear();
        _visibleCmds.Clear();

        if (_selectedCmd is not null)
        {
            if (_selectedCmd.CloseBeforeExec)
                Close();

            _selectedCmd.Action?.Invoke();
        }
    }

    /// <summary>
    /// フィルタに基づいて表示対象コマンドを更新する。
    /// 選択中のコマンドがフィルタ外になった場合は先頭を自動選択する。
    /// </summary>
    private void UpdateVisible()
    {
        if (string.IsNullOrEmpty(_filter))
        {
            VisibleCmds = _cmds;
        }
        else
        {
            List<RepositoryCommandPaletteCmd> visible = [];

            // ラベルまたはキーワードにフィルタ文字列を含むコマンドを収集
            foreach (var cmd in _cmds)
            {
                if (cmd.Label.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                    cmd.Keyword.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    visible.Add(cmd);
            }

            // 選択中コマンドがフィルタ外なら先頭を自動選択
            var autoSelected = _selectedCmd;
            if (!visible.Contains(_selectedCmd))
                autoSelected = visible.Count > 0 ? visible[0] : null;

            VisibleCmds = visible;
            SelectedCmd = autoSelected;
        }
    }

    private List<RepositoryCommandPaletteCmd> _cmds = [];              // 全コマンドリスト
    private List<RepositoryCommandPaletteCmd> _visibleCmds = [];       // フィルタ後の表示コマンド
    private RepositoryCommandPaletteCmd _selectedCmd = null;           // 選択中のコマンド
    private string _filter;                                            // 検索フィルタ
}
