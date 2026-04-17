using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// サブモジュールの 2 リビジョン間の差分を閲覧するダイアログ ViewModel。
/// 親リポジトリの差分から呼び出され、サブモジュール側の変更内容を詳細に表示する。
/// </summary>
public class SubmoduleRevisionCompare : ObservableObject
{
    /// <summary>変更リストの読み込み中かどうか。</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>比較元（古い側）のコミット。</summary>
    public Models.Commit Base
    {
        get => _base;
        private set => SetProperty(ref _base, value);
    }

    /// <summary>比較先（新しい側）のコミット。</summary>
    public Models.Commit To
    {
        get => _to;
        private set => SetProperty(ref _to, value);
    }

    /// <summary>総変更数。</summary>
    public int TotalChanges
    {
        get => _totalChanges;
        private set => SetProperty(ref _totalChanges, value);
    }

    /// <summary>フィルタ後に表示される変更リスト。</summary>
    public List<Models.Change> VisibleChanges
    {
        get => _visibleChanges;
        private set => SetProperty(ref _visibleChanges, value);
    }

    /// <summary>選択中の変更。1 件選択されたら DiffContext を生成する。</summary>
    public List<Models.Change> SelectedChanges
    {
        get => _selectedChanges;
        set
        {
            if (SetProperty(ref _selectedChanges, value))
            {
                if (value is { Count: 1 })
                    DiffContext = new DiffContext(_repo, new Models.DiffOption(_base.SHA, _to.SHA, value[0]), _diffContext);
                else
                    DiffContext = null;
            }
        }
    }

    /// <summary>検索フィルタ文字列。</summary>
    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
                RefreshVisible();
        }
    }

    /// <summary>選択中の変更に対する差分コンテキスト。</summary>
    public DiffContext DiffContext
    {
        get => _diffContext;
        private set => SetProperty(ref _diffContext, value);
    }

    /// <summary>
    /// コンストラクタ。親リポジトリの SubmoduleDiff を受け取って初期化する。
    /// </summary>
    public SubmoduleRevisionCompare(Models.SubmoduleDiff diff)
    {
        _repo = diff.FullPath;
        _base = diff.Old.Commit;
        _to = diff.New.Commit;

        Refresh();
    }

    /// <summary>Base と To を入れ替える。</summary>
    public void Swap()
    {
        (Base, To) = (To, Base);
        Refresh();
    }

    /// <summary>検索フィルタをクリアする。</summary>
    public void ClearSearchFilter()
    {
        SearchFilter = string.Empty;
    }

    /// <summary>リポジトリ相対パスから絶対パスを取得する。</summary>
    public string GetAbsPath(string path)
    {
        return Native.OS.GetAbsPath(_repo, path);
    }

    /// <summary>外部 diff ツールで変更を開く。</summary>
    public void OpenInExternalDiffTool(Models.Change change)
    {
        new Commands.DiffTool(_repo, new Models.DiffOption(_base.SHA, _to.SHA, change)).Open();
    }

    /// <summary>変更一覧をパッチファイルとして保存する。</summary>
    public async Task<bool> SaveChangesAsPatchAsync(List<Models.Change> changes, string saveTo)
    {
        return await Commands.SaveChangesAsPatch.ProcessRevisionCompareChangesAsync(_repo, changes, _base.SHA, _to.SHA, saveTo);
    }

    /// <summary>
    /// サブモジュール内の変更一覧を非同期で再読み込みする。
    /// </summary>
    private void Refresh()
    {
        IsLoading = true;
        VisibleChanges = [];
        SelectedChanges = [];

        Task.Run(async () =>
        {
            _changes = await new Commands.CompareRevisions(_repo, _base.SHA, _to.SHA)
                .ReadAsync()
                .ConfigureAwait(false);

            var visible = _changes;
            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                visible = [];
                foreach (var c in _changes)
                {
                    if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(c);
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                TotalChanges = _changes.Count;
                VisibleChanges = visible;
                IsLoading = false;

                if (VisibleChanges.Count > 0)
                    SelectedChanges = [VisibleChanges[0]];
                else
                    SelectedChanges = [];
            });
        });
    }

    /// <summary>
    /// 現在の検索フィルタで VisibleChanges を再計算する。
    /// </summary>
    private void RefreshVisible()
    {
        if (_changes == null)
            return;

        if (string.IsNullOrEmpty(_searchFilter))
        {
            VisibleChanges = _changes;
        }
        else
        {
            List<Models.Change> visible = [];
            foreach (var c in _changes)
            {
                if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    visible.Add(c);
            }

            VisibleChanges = visible;
        }
    }

    private string _repo;
    private bool _isLoading = true;
    private Models.Commit _base = null;
    private Models.Commit _to = null;
    private int _totalChanges = 0;
    private List<Models.Change> _changes = null;
    private List<Models.Change> _visibleChanges = null;
    private List<Models.Change> _selectedChanges = null;
    private string _searchFilter = string.Empty;
    private DiffContext _diffContext = null;
}
