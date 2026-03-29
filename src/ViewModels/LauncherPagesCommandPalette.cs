using System;
using System.Collections.Generic;

namespace Komorebi.ViewModels;

/// <summary>
/// タブ切り替え用のコマンドパレットViewModel。
/// 開いているタブと未開封のリポジトリを検索・切り替えできる。
/// </summary>
public class LauncherPagesCommandPalette : ICommandPalette
{
    /// <summary>フィルタ適用後の表示対象タブ一覧。</summary>
    public List<LauncherPage> VisiblePages
    {
        get => _visiblePages;
        private set => SetProperty(ref _visiblePages, value);
    }

    /// <summary>フィルタ適用後の表示対象リポジトリ一覧（未開封のもの）。</summary>
    public List<RepositoryNode> VisibleRepos
    {
        get => _visibleRepos;
        private set => SetProperty(ref _visibleRepos, value);
    }

    /// <summary>検索フィルタ文字列。変更時に表示リストを更新する。</summary>
    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
                UpdateVisible();
        }
    }

    /// <summary>選択中のタブ。選択時にリポジトリの選択を解除する。</summary>
    public LauncherPage SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (SetProperty(ref _selectedPage, value) && value is not null)
                SelectedRepo = null;
        }
    }

    /// <summary>選択中のリポジトリ。選択時にタブの選択を解除する。</summary>
    public RepositoryNode SelectedRepo
    {
        get => _selectedRepo;
        set
        {
            if (SetProperty(ref _selectedRepo, value) && value is not null)
                SelectedPage = null;
        }
    }

    /// <summary>
    /// コンストラクタ。既に開いているリポジトリのIDを記録し、初期表示を構築する。
    /// </summary>
    public LauncherPagesCommandPalette(Launcher launcher)
    {
        _launcher = launcher;

        foreach (var page in _launcher.Pages)
        {
            if (page.Node.IsRepository)
                _opened.Add(page.Node.Id);
        }

        UpdateVisible();
    }

    /// <summary>検索フィルタをクリアする。</summary>
    public void ClearFilter()
    {
        SearchFilter = string.Empty;
    }

    /// <summary>
    /// 選択されたタブに切り替えるか、リポジトリを新しいタブで開く。
    /// </summary>
    public void OpenOrSwitchTo()
    {
        _opened.Clear();
        _visiblePages.Clear();
        _visibleRepos.Clear();
        Close();

        if (_selectedPage is not null)
            _launcher.ActivePage = _selectedPage;
        else if (_selectedRepo is not null)
            _launcher.OpenRepositoryInTab(_selectedRepo, null);
    }

    /// <summary>
    /// フィルタに基づいて表示対象のタブとリポジトリを更新し、
    /// 自動選択ロジックで適切な項目を選択する。
    /// </summary>
    private void UpdateVisible()
    {
        List<LauncherPage> pages = [];
        CollectVisiblePages(pages);

        List<RepositoryNode> repos = [];
        CollectVisibleRepository(repos, Preferences.Instance.RepositoryNodes);

        var autoSelectPage = _selectedPage;
        var autoSelectRepo = _selectedRepo;

        if (_selectedPage is not null)
        {
            if (pages.Contains(_selectedPage))
            {
                // Keep selection
            }
            else if (pages.Count > 0)
            {
                autoSelectPage = pages[0];
            }
            else if (repos.Count > 0)
            {
                autoSelectPage = null;
                autoSelectRepo = repos[0];
            }
            else
            {
                autoSelectPage = null;
            }
        }
        else if (_selectedRepo is not null)
        {
            if (repos.Contains(_selectedRepo))
            {
                // Keep selection
            }
            else if (repos.Count > 0)
            {
                autoSelectRepo = repos[0];
            }
            else if (pages.Count > 0)
            {
                autoSelectPage = pages[0];
                autoSelectRepo = null;
            }
            else
            {
                autoSelectRepo = null;
            }
        }
        else if (pages.Count > 0)
        {
            autoSelectPage = pages[0];
            autoSelectRepo = null;
        }
        else if (repos.Count > 0)
        {
            autoSelectPage = null;
            autoSelectRepo = repos[0];
        }
        else
        {
            autoSelectPage = null;
            autoSelectRepo = null;
        }

        VisiblePages = pages;
        VisibleRepos = repos;
        SelectedPage = autoSelectPage;
        SelectedRepo = autoSelectRepo;
    }

    /// <summary>アクティブタブ以外でフィルタに合致するタブを収集する。</summary>
    private void CollectVisiblePages(List<LauncherPage> pages)
    {
        foreach (var page in _launcher.Pages)
        {
            if (page == _launcher.ActivePage)
                continue;

            if (string.IsNullOrEmpty(_searchFilter) ||
                page.Node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                (page.Node.IsRepository && page.Node.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)))
                pages.Add(page);
        }
    }

    /// <summary>未開封でフィルタに合致するリポジトリを再帰的に収集する。</summary>
    private void CollectVisibleRepository(List<RepositoryNode> outs, List<RepositoryNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsRepository)
            {
                CollectVisibleRepository(outs, node.SubNodes);
                continue;
            }

            if (_opened.Contains(node.Id))
                continue;

            if (string.IsNullOrEmpty(_searchFilter) ||
                node.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                outs.Add(node);
        }
    }

    private Launcher _launcher = null;
    private HashSet<string> _opened = [];
    private List<LauncherPage> _visiblePages = [];
    private List<RepositoryNode> _visibleRepos = [];
    private string _searchFilter = string.Empty;
    private LauncherPage _selectedPage = null;
    private RepositoryNode _selectedRepo = null;
}
