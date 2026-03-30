using System;
using System.IO;
using System.Text;

using Avalonia.Collections;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// アプリケーションのメインランチャーViewModel。
/// タブ管理、ワークスペース切り替え、リポジトリの開閉を担当する。
/// </summary>
public class Launcher : ObservableObject
{
    /// <summary>ウィンドウタイトル。アクティブタブとワークスペース名から構築される。</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>開いているタブページの一覧。</summary>
    public AvaloniaList<LauncherPage> Pages
    {
        get;
        private set;
    }

    /// <summary>現在アクティブなワークスペース。</summary>
    public Workspace ActiveWorkspace
    {
        get => _activeWorkspace;
        private set => SetProperty(ref _activeWorkspace, value);
    }

    /// <summary>現在アクティブな（前面表示中の）タブページ。変更時にタイトルを更新する。</summary>
    public LauncherPage ActivePage
    {
        get => _activePage;
        set
        {
            if (SetProperty(ref _activePage, value))
                PostActivePageChanged();
        }
    }

    /// <summary>現在表示中のコマンドパレット。nullの場合は非表示。</summary>
    public ICommandPalette CommandPalette
    {
        get => _commandPalette;
        set => SetProperty(ref _commandPalette, value);
    }

    /// <summary>
    /// コンストラクタ。ワークスペースのリポジトリを復元し、
    /// 起動引数で指定されたリポジトリがあればそれを開く。
    /// </summary>
    public Launcher(string startupRepo)
    {
        _ignoreIndexChange = true;

        Pages = new AvaloniaList<LauncherPage>();
        AddNewTab();

        var pref = Preferences.Instance;
        ActiveWorkspace = pref.GetActiveWorkspace();

        var repos = ActiveWorkspace.Repositories.ToArray();
        foreach (var repo in repos)
        {
            var node = pref.FindNode(repo) ??
                new RepositoryNode
                {
                    Id = repo,
                    Name = Path.GetFileName(repo),
                    Bookmark = 0,
                    IsRepository = true,
                };

            OpenRepositoryInTab(node, null);
        }

        _ignoreIndexChange = false;

        if (!string.IsNullOrEmpty(startupRepo))
        {
            var test = new Commands.QueryRepositoryRootPath(startupRepo).GetResult();
            if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
            {
                var node = pref.FindOrAddNodeByRepositoryPath(test.StdOut.Trim(), null, false);
                Welcome.Instance.Refresh();

                OpenRepositoryInTab(node, null);
                return;
            }
        }

        var activeIdx = ActiveWorkspace.ActiveIdx;
        if (activeIdx > 0 && activeIdx < Pages.Count)
        {
            ActivePage = Pages[activeIdx];
            return;
        }

        ActivePage = Pages[0];
        PostActivePageChanged();
    }

    /// <summary>アプリケーション終了時に全タブのリポジトリを閉じる。</summary>
    public void Quit()
    {
        _ignoreIndexChange = true;

        foreach (var one in Pages)
            CloseRepositoryInTab(one, false);

        _ignoreIndexChange = false;
    }

    /// <summary>
    /// 指定されたワークスペースに切り替える。
    /// 実行中タスクがある場合は中止する。現在のタブを閉じて新ワークスペースのリポジトリを開く。
    /// </summary>
    public void SwitchWorkspace(Workspace to)
    {
        if (to is null || to.IsActive)
            return;

        foreach (var one in Pages)
        {
            if (!one.CanCreatePopup() || one.Data is Repository { IsAutoFetching: true })
            {
                App.RaiseException(null, App.Text("Error.UnfinishedTasks"));
                return;
            }
        }

        _ignoreIndexChange = true;

        var pref = Preferences.Instance;
        foreach (var w in pref.Workspaces)
            w.IsActive = false;

        ActiveWorkspace = to;
        to.IsActive = true;

        foreach (var one in Pages)
            CloseRepositoryInTab(one, false);

        Pages.Clear();
        AddNewTab();

        var repos = to.Repositories.ToArray();
        foreach (var repo in repos)
        {
            var node = pref.FindNode(repo) ??
                new RepositoryNode
                {
                    Id = repo,
                    Name = Path.GetFileName(repo),
                    Bookmark = 0,
                    IsRepository = true,
                };

            OpenRepositoryInTab(node, null);
        }

        var activeIdx = to.ActiveIdx;
        if (activeIdx >= 0 && activeIdx < Pages.Count)
            ActivePage = Pages[activeIdx];
        else
            ActivePage = Pages[0];

        _ignoreIndexChange = false;
        PostActivePageChanged();
        Preferences.Instance.Save();
        GC.Collect();

        var cloneDir = to.DefaultCloneDir;
        if (string.IsNullOrEmpty(cloneDir))
            cloneDir = Preferences.Instance.GitDefaultCloneDir;
        if (!string.IsNullOrEmpty(cloneDir))
            _ = ScanRepositories.ScanDirectoryAsync(cloneDir);
    }

    /// <summary>新しい空タブ（Welcomeページ）を追加し、アクティブにする。</summary>
    public void AddNewTab()
    {
        var page = new LauncherPage();
        Pages.Add(page);
        ActivePage = page;
    }

    /// <summary>タブをドラッグ＆ドロップで並び替える。ワークスペースのリポジトリ順序も更新する。</summary>
    public void MoveTab(LauncherPage from, LauncherPage to)
    {
        _ignoreIndexChange = true;

        var fromIdx = Pages.IndexOf(from);
        var toIdx = Pages.IndexOf(to);
        Pages.Move(fromIdx, toIdx);

        _activeWorkspace.Repositories.Clear();
        foreach (var p in Pages)
        {
            if (p.Data is Repository r)
                _activeWorkspace.Repositories.Add(r.FullPath);
        }

        _ignoreIndexChange = false;
        ActivePage = from;
    }

    /// <summary>次のタブに切り替える（循環）。</summary>
    public void GotoNextTab()
    {
        if (Pages.Count == 1)
            return;

        var activeIdx = Pages.IndexOf(_activePage);
        var nextIdx = (activeIdx + 1) % Pages.Count;
        ActivePage = Pages[nextIdx];
    }

    /// <summary>前のタブに切り替える（循環）。</summary>
    public void GotoPrevTab()
    {
        if (Pages.Count == 1)
            return;

        var activeIdx = Pages.IndexOf(_activePage);
        var prevIdx = activeIdx == 0 ? Pages.Count - 1 : activeIdx - 1;
        ActivePage = Pages[prevIdx];
    }

    /// <summary>
    /// タブを閉じる。最後のタブの場合はリポジトリを閉じてWelcomeに戻るか、
    /// Welcomeならアプリケーションを終了する。
    /// </summary>
    public void CloseTab(LauncherPage page)
    {
        if (Pages.Count == 1)
        {
            var last = Pages[0];
            if (last.Data is Repository repo)
            {
                _activeWorkspace.Repositories.Clear();
                _activeWorkspace.ActiveIdx = 0;

                repo.Close();

                Welcome.Instance.ClearSearchFilter();
                last.Node = new RepositoryNode() { Id = Guid.NewGuid().ToString() };
                last.Data = Welcome.Instance;
                last.Popup?.Cleanup();
                last.Popup = null;

                PostActivePageChanged();
                GC.Collect();
            }
            else
            {
                App.Quit(0);
            }

            return;
        }

        page ??= _activePage;

        var removeIdx = Pages.IndexOf(page);
        var activeIdx = Pages.IndexOf(_activePage);
        if (removeIdx == activeIdx)
            ActivePage = Pages[removeIdx > 0 ? removeIdx - 1 : removeIdx + 1];

        CloseRepositoryInTab(page);
        Pages.RemoveAt(removeIdx);
        GC.Collect();
    }

    /// <summary>アクティブタブ以外の全タブを閉じる。</summary>
    public void CloseOtherTabs()
    {
        if (Pages.Count == 1)
            return;

        _ignoreIndexChange = true;

        var id = ActivePage.Node.Id;
        foreach (var one in Pages)
        {
            if (one.Node.Id != id)
                CloseRepositoryInTab(one);
        }

        Pages = new AvaloniaList<LauncherPage> { ActivePage };
        OnPropertyChanged(nameof(Pages));

        _activeWorkspace.ActiveIdx = 0;
        _ignoreIndexChange = false;
        GC.Collect();
    }

    /// <summary>アクティブタブより右側の全タブを閉じる。</summary>
    public void CloseRightTabs()
    {
        _ignoreIndexChange = true;

        var endIdx = Pages.IndexOf(ActivePage);
        for (var i = Pages.Count - 1; i > endIdx; i--)
        {
            CloseRepositoryInTab(Pages[i]);
            Pages.Remove(Pages[i]);
        }

        _ignoreIndexChange = false;
        GC.Collect();
    }

    /// <summary>
    /// リポジトリをタブで開く。既に開いているタブがあればそちらに切り替える。
    /// pageがnullの場合は新規タブまたはアクティブタブを再利用する。
    /// </summary>
    public void OpenRepositoryInTab(RepositoryNode node, LauncherPage page)
    {
        // 既に開いているタブがあればそちらに切り替え
        foreach (var one in Pages)
        {
            if (one.Node.Id == node.Id)
            {
                ActivePage = one;
                return;
            }
        }

        // リポジトリパスの存在チェック
        if (!Path.Exists(node.Id))
        {
            App.RaiseException(node.Id, App.Text("Error.RepositoryNotExist"));
            return;
        }

        // ベアリポジトリ判定とgitディレクトリの取得
        var isBare = new Commands.IsBareRepository(node.Id).GetResult();
        var gitDir = isBare ? node.Id : GetRepositoryGitDir(node.Id);
        if (string.IsNullOrEmpty(gitDir))
        {
            App.RaiseException(node.Id, App.Text("Error.InvalidGitRepository"));
            return;
        }

        var repo = new Repository(isBare, node.Id, gitDir);
        repo.Open();

        if (page is null)
        {
            // アクティブタブがリポジトリの場合は新規タブを追加、そうでなければ既存タブを再利用
            if (_activePage is null || _activePage.Node.IsRepository)
            {
                page = new LauncherPage(node, repo);
                Pages.Add(page);
            }
            else
            {
                page = _activePage;
                page.Node = node;
                page.Data = repo;
            }
        }
        else
        {
            page.Node = node;
            page.Data = repo;
        }

        // ワークスペースのリポジトリ一覧を再構築
        _activeWorkspace.Repositories.Clear();
        foreach (var p in Pages)
        {
            if (p.Data is Repository r)
                _activeWorkspace.Repositories.Add(r.FullPath);
        }

        if (_activePage == page)
            PostActivePageChanged();
        else
            ActivePage = page;
    }

    /// <summary>
    /// 指定されたページIDに対して通知を配信する。
    /// UIスレッド以外から呼ばれた場合はUIスレッドにディスパッチする。
    /// 該当ページが見つからない場合はアクティブページに追加する。
    /// </summary>
    public void DispatchNotification(string pageId, string message, bool isError, string hint = "", string actionLabel = null, Action actionCallback = null)
    {
        // UIスレッド以外からの呼び出しはUIスレッドにディスパッチ
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Invoke(() => DispatchNotification(pageId, message, isError, hint, actionLabel, actionCallback));
            return;
        }

        var notification = new Models.Notification()
        {
            IsError = isError,
            Message = message,
            Hint = hint,
            ActionLabel = actionLabel ?? string.Empty,
            ActionCallback = actionCallback,
        };

        // パス区切り文字を統一して該当ページを検索
        foreach (var page in Pages)
        {
            var id = page.Node.Id.Replace('\\', '/').TrimEnd('/');
            if (id == pageId)
            {
                page.Notifications.Add(notification);
                return;
            }
        }

        // 該当ページが見つからなければアクティブページに通知を追加
        _activePage?.Notifications.Add(notification);
    }

    /// <summary>
    /// リポジトリの.gitディレクトリのパスを取得する。
    /// .gitがディレクトリなら直接返し、ファイルならリダイレクト先を解決する（worktree対応）。
    /// </summary>
    private static string GetRepositoryGitDir(string repo)
    {
        var fullpath = Path.Combine(repo, ".git");

        // .gitがディレクトリの場合：refs/objects/HEADの存在を確認
        if (Directory.Exists(fullpath))
        {
            if (Directory.Exists(Path.Combine(fullpath, "refs")) &&
                Directory.Exists(Path.Combine(fullpath, "objects")) &&
                File.Exists(Path.Combine(fullpath, "HEAD")))
                return fullpath;

            return null;
        }

        // .gitがファイルの場合：worktreeのgitdirリダイレクトを解決
        if (File.Exists(fullpath))
        {
            var redirect = File.ReadAllText(fullpath).Trim();
            if (redirect.StartsWith("gitdir: ", StringComparison.Ordinal))
                redirect = redirect[8..];

            // 相対パスの場合は絶対パスに変換
            if (!Path.IsPathRooted(redirect))
                redirect = Path.GetFullPath(Path.Combine(repo, redirect));

            if (Directory.Exists(redirect))
                return redirect;

            return null;
        }

        // 上記以外の場合はgitコマンドで問い合わせ
        return new Commands.QueryGitDir(repo).GetResult();
    }

    /// <summary>
    /// タブ内のリポジトリを閉じる。ポップアップのクリーンアップも行う。
    /// removeFromWorkspaceがtrueの場合はワークスペースからも削除する。
    /// </summary>
    private void CloseRepositoryInTab(LauncherPage page, bool removeFromWorkspace = true)
    {
        if (page.Data is Repository repo)
        {
            if (removeFromWorkspace)
                _activeWorkspace.Repositories.Remove(repo.FullPath);

            repo.Close();
        }

        page.Popup?.Cleanup();
        page.Popup = null;
        page.Data = null;
    }

    /// <summary>
    /// アクティブページ変更後の処理。ワークスペースのアクティブインデックスを更新し、
    /// ウィンドウタイトルを再構築する。
    /// </summary>
    private void PostActivePageChanged()
    {
        if (_ignoreIndexChange)
            return;

        // ワークスペースのアクティブインデックスを更新
        if (_activePage is { Data: Repository repo })
            _activeWorkspace.ActiveIdx = _activeWorkspace.Repositories.IndexOf(repo.FullPath);

        // ウィンドウタイトルを「ページ名 - ワークスペース名」形式で構築
        var builder = new StringBuilder(512);
        builder.Append(string.IsNullOrEmpty(_activePage.Node.Name) ? "Repositories" : _activePage.Node.Name);

        var workspaces = Preferences.Instance.Workspaces;
        if (workspaces.Count == 0 || workspaces.Count > 1 || workspaces[0] != _activeWorkspace)
            builder.Append(" - ").Append(_activeWorkspace.Name);

        Title = builder.ToString();
        CommandPalette = null;
    }

    private Workspace _activeWorkspace;       // 現在アクティブなワークスペース
    private LauncherPage _activePage;          // 現在アクティブなタブページ
    private bool _ignoreIndexChange;           // インデックス変更通知を抑制するフラグ
    private string _title = string.Empty;      // ウィンドウタイトル
    private ICommandPalette _commandPalette;   // 現在表示中のコマンドパレット
}
