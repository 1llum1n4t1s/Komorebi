using System;
using System.Collections.Generic;

namespace Komorebi.ViewModels;

/// <summary>
/// 比較コマンドパレットのViewModel。
/// ブランチやタグの一覧をフィルタして比較対象のリビジョンを素早く選択するための検索パレット。
/// </summary>
public class CompareCommandPalette : ICommandPalette
{
    /// <summary>
    /// 比較の基準となるオブジェクト（ブランチ、タグ、コミット）。
    /// </summary>
    public object BasedOn
    {
        get => _basedOn;
    }

    /// <summary>
    /// 比較対象のオブジェクト（ブランチ、タグ、コミット）。
    /// </summary>
    public object CompareTo
    {
        get => _compareTo;
        set => SetProperty(ref _compareTo, value);
    }

    /// <summary>
    /// フィルタ適用後の参照リスト（ブランチとタグの混合リスト）。
    /// </summary>
    public List<object> Refs
    {
        get => _refs;
        private set => SetProperty(ref _refs, value);
    }

    /// <summary>
    /// 参照名フィルタ文字列。変更時に参照リストを更新する。
    /// </summary>
    public string Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
                // フィルタ変更時に参照リストを再構築する
                UpdateRefs();
        }
    }

    /// <summary>
    /// コンストラクタ。リポジトリと基準オブジェクトを受け取り、参照リストを初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    /// <param name="basedOn">比較の基準となるオブジェクト（nullの場合は現在のブランチ）</param>
    public CompareCommandPalette(Repository repo, object basedOn)
    {
        _repo = repo;
        _basedOn = basedOn ?? repo.CurrentBranch;
        UpdateRefs();
    }

    /// <summary>
    /// フィルタ文字列をクリアする。
    /// </summary>
    public void ClearFilter()
    {
        Filter = string.Empty;
    }

    /// <summary>
    /// 比較を実行する。リソースをクリアしてパレットを閉じ、比較ウィンドウを表示する。
    /// </summary>
    public void Launch()
    {
        // リソースをクリアしてパレットを閉じる
        _refs.Clear();
        Close();

        // 比較対象が選択されている場合は比較ウィンドウを表示する
        if (_compareTo is not null)
            App.ShowWindow(new Compare(_repo, _basedOn, _compareTo));
    }

    /// <summary>
    /// フィルタに基づいて参照リストを更新する。
    /// 基準オブジェクトは除外し、ブランチを先に、タグを後に表示する。
    /// </summary>
    private void UpdateRefs()
    {
        List<object> refs = [];

        // フィルタに一致するブランチを追加する（基準オブジェクトは除外）
        foreach (var b in _repo.Branches)
        {
            if (b == _basedOn)
                continue;

            if (string.IsNullOrEmpty(_filter) || b.FriendlyName.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                refs.Add(b);
        }

        // フィルタに一致するタグを追加する（基準オブジェクトは除外）
        foreach (var t in _repo.Tags)
        {
            if (t == _basedOn)
                continue;

            if (string.IsNullOrEmpty(_filter) || t.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                refs.Add(t);
        }

        // ブランチを先に（ローカル優先）、タグを後に、名前順でソートする
        refs.Sort((l, r) =>
        {
            if (l is Models.Branch lb)
            {
                if (r is Models.Branch rb)
                {
                    // ブランチ同士はローカル優先、その後名前順
                    if (lb.IsLocal == rb.IsLocal)
                        return Models.NumericSort.Compare(lb.FriendlyName, rb.FriendlyName);
                    return lb.IsLocal ? -1 : 1;
                }

                // ブランチはタグより先に表示する
                return -1;
            }

            // タグはブランチの後に表示する
            if (r is Models.Branch)
                return 1;

            // タグ同士は名前順でソートする
            return Models.NumericSort.Compare((l as Models.Tag).Name, (r as Models.Tag).Name);
        });

        // 自動選択：リストが空ならnull、現在の選択が含まれなければ先頭を選択する
        var autoSelected = _compareTo;
        if (refs.Count == 0)
            autoSelected = null;
        else if (_compareTo is null || !refs.Contains(_compareTo))
            autoSelected = refs[0];

        Refs = refs;
        CompareTo = autoSelected;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private Repository _repo;
    /// <summary>比較の基準オブジェクト</summary>
    private object _basedOn = null;
    /// <summary>比較対象オブジェクト</summary>
    private object _compareTo = null;
    /// <summary>フィルタ適用後の参照リスト</summary>
    private List<object> _refs = [];
    /// <summary>フィルタ文字列</summary>
    private string _filter;
}
