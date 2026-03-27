using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
///     チェックアウトコマンドパレットのViewModel。
///     ブランチ一覧をフィルタして素早くチェックアウトするための検索パレット。
/// </summary>
public class CheckoutCommandPalette : ICommandPalette
{
    /// <summary>
    ///     フィルタ適用後のブランチリスト。
    /// </summary>
    public List<Models.Branch> Branches
    {
        get => _branches;
        private set => SetProperty(ref _branches, value);
    }

    /// <summary>
    ///     選択されたブランチ。
    /// </summary>
    public Models.Branch SelectedBranch
    {
        get => _selectedBranch;
        set => SetProperty(ref _selectedBranch, value);
    }

    /// <summary>
    ///     ブランチ名フィルタ文字列。変更時にブランチリストを更新する。
    /// </summary>
    public string Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
                // フィルタ変更時にブランチリストを再構築する
                UpdateBranches();
        }
    }

    /// <summary>
    ///     コンストラクタ。リポジトリを受け取りブランチリストを初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    public CheckoutCommandPalette(Repository repo)
    {
        _repo = repo;
        UpdateBranches();
    }

    /// <summary>
    ///     フィルタ文字列をクリアする。
    /// </summary>
    public void ClearFilter()
    {
        Filter = string.Empty;
    }

    /// <summary>
    ///     選択されたブランチのチェックアウトを実行する。
    /// </summary>
    public async Task ExecAsync()
    {
        // リソースをクリアしてパレットを閉じる
        _branches.Clear();
        Close();

        // 選択されたブランチがあればチェックアウトを実行する
        if (_selectedBranch is not null)
            await _repo.CheckoutBranchAsync(_selectedBranch);
    }

    /// <summary>
    ///     フィルタに基づいてブランチリストを更新する。
    ///     現在のブランチは除外し、ローカルブランチを先に表示する。
    /// </summary>
    private void UpdateBranches()
    {
        var current = _repo.CurrentBranch;
        if (current is null)
            return;

        // フィルタに一致するブランチを抽出する（現在のブランチは除外）
        var branches = new List<Models.Branch>();
        foreach (var b in _repo.Branches)
        {
            if (b == current)
                continue;

            if (string.IsNullOrEmpty(_filter) || b.FriendlyName.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                branches.Add(b);
        }

        // ローカルブランチを先に、その後名前順でソートする
        branches.Sort((l, r) =>
        {
            if (l.IsLocal == r.IsLocal)
                return Models.NumericSort.Compare(l.Name, r.Name);

            return l.IsLocal ? -1 : 1;
        });

        // 自動選択：リストが空ならnull、現在の選択が含まれなければ先頭を選択する
        var autoSelected = _selectedBranch;
        if (branches.Count == 0)
            autoSelected = null;
        else if (_selectedBranch is null || !branches.Contains(_selectedBranch))
            autoSelected = branches[0];

        Branches = branches;
        SelectedBranch = autoSelected;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private Repository _repo;
    /// <summary>フィルタ適用後のブランチリスト</summary>
    private List<Models.Branch> _branches = [];
    /// <summary>選択されたブランチ</summary>
    private Models.Branch _selectedBranch = null;
    /// <summary>フィルタ文字列</summary>
    private string _filter;
}
