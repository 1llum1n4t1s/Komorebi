using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
///     リポジトリのメインViewModel。
///     ブランチ、タグ、履歴、ワーキングコピー、スタッシュ、サブモジュール、
///     ワークツリー、Bisect、LFS、Git Flow、カスタムアクション、自動フェッチ等を統合管理する。
/// </summary>
public class Repository : ObservableObject, Models.IRepository
{
    /// <summary>ベアリポジトリかどうか。</summary>
    public bool IsBare
    {
        get;
    }

    /// <summary>リポジトリのフルパス（スラッシュ区切り、末尾スラッシュなし）。</summary>
    public string FullPath
    {
        get;
    }

    /// <summary>.gitディレクトリのフルパス（スラッシュ区切り）。</summary>
    public string GitDir
    {
        get;
    }

    /// <summary>リポジトリ固有の設定（カスタムアクション、テンプレート等）。</summary>
    public Models.RepositorySettings Settings
    {
        get => _settings;
    }

    /// <summary>リポジトリのUI状態（展開状態、ソートモード、履歴フィルタ等）。</summary>
    public Models.RepositoryUIStates UIStates
    {
        get => _uiStates;
    }

    /// <summary>Git Flow設定（master/develop/feature/release/hotfixのブランチ名プレフィックス）。</summary>
    public Models.GitFlow GitFlow
    {
        get;
        set;
    } = new();

    /// <summary>履歴のフィルタモード（Include/Exclude/None）。</summary>
    public Models.FilterMode HistoryFilterMode
    {
        get => _historyFilterMode;
        private set => SetProperty(ref _historyFilterMode, value);
    }

    /// <summary>GPG SSH署名の許可済み署名者ファイルが設定されているかどうか。</summary>
    public bool HasAllowedSignersFile
    {
        get => _hasAllowedSignersFile;
    }

    /// <summary>選択中のビューインデックス。0=履歴、1=ワーキングコピー、2=スタッシュ。</summary>
    public int SelectedViewIndex
    {
        get => _selectedViewIndex;
        set
        {
            if (SetProperty(ref _selectedViewIndex, value))
            {
                SelectedView = value switch
                {
                    1 => _workingCopy,
                    2 => _stashesPage,
                    _ => _histories,
                };
            }
        }
    }

    /// <summary>現在選択中のビューオブジェクト（Histories/WorkingCopy/StashesPage）。</summary>
    public object SelectedView
    {
        get => _selectedView;
        set => SetProperty(ref _selectedView, value);
    }

    /// <summary>履歴をトポロジカル順序で表示するかどうか。変更時にコミット一覧を再取得する。</summary>
    public bool EnableTopoOrderInHistory
    {
        get => _uiStates.EnableTopoOrderInHistory;
        set
        {
            if (value != _uiStates.EnableTopoOrderInHistory)
            {
                _uiStates.EnableTopoOrderInHistory = value;
                RefreshCommits();
            }
        }
    }

    /// <summary>履歴表示フラグ（マージコミット表示、ファーストペアレントのみ等）。変更時にコミット一覧を再取得する。</summary>
    public Models.HistoryShowFlags HistoryShowFlags
    {
        get => _uiStates.HistoryShowFlags;
        private set
        {
            if (value != _uiStates.HistoryShowFlags)
            {
                _uiStates.HistoryShowFlags = value;
                RefreshCommits();
            }
        }
    }

    /// <summary>履歴グラフで現在のブランチのみハイライトするかどうか。</summary>
    public bool OnlyHighlightCurrentBranchInHistory
    {
        get => _uiStates.OnlyHighlightCurrentBranchInHistory;
        set
        {
            if (value != _uiStates.OnlyHighlightCurrentBranchInHistory)
            {
                _uiStates.OnlyHighlightCurrentBranchInHistory = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>サイドバーのフィルタ文字列。変更時にデバウンス付きでブランチツリー、タグ、サブモジュールの表示を再構築する。</summary>
    public string Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
            {
                if (_filterDebounceTimer is null)
                    _filterDebounceTimer = new System.Threading.Timer(_ => Dispatcher.UIThread.Post(ApplyFilter), null, Timeout.Infinite, Timeout.Infinite);

                _filterDebounceTimer.Change(150, Timeout.Infinite);
            }
        }
    }

    /// <summary>フィルタの実適用処理。</summary>
    private void ApplyFilter()
    {
        var builder = BuildBranchTree(_branches, _remotes);
        LocalBranchTrees = builder.Locals;
        RemoteBranchTrees = builder.Remotes;
        VisibleTags = BuildVisibleTags();
        VisibleSubmodules = BuildVisibleSubmodules();
    }

    /// <summary>リモートリポジトリの一覧。</summary>
    public List<Models.Remote> Remotes
    {
        get => _remotes;
        private set => SetProperty(ref _remotes, value);
    }

    /// <summary>ブランチの一覧（ローカル・リモート両方）。</summary>
    public List<Models.Branch> Branches
    {
        get => _branches;
        private set => SetProperty(ref _branches, value);
    }

    /// <summary>現在チェックアウト中のブランチ。HEAD変更時にAmendモードをリセットする。</summary>
    public Models.Branch CurrentBranch
    {
        get => _currentBranch;
        private set
        {
            var oldHead = _currentBranch?.Head;
            if (SetProperty(ref _currentBranch, value) && value is not null)
            {
                if (oldHead != _currentBranch.Head && _workingCopy is { UseAmend: true })
                    _workingCopy.UseAmend = false;
            }
        }
    }

    /// <summary>ローカルブランチのツリー表示用ノード一覧。</summary>
    public List<BranchTreeNode> LocalBranchTrees
    {
        get => _localBranchTrees;
        private set => SetProperty(ref _localBranchTrees, value);
    }

    /// <summary>リモートブランチのツリー表示用ノード一覧。</summary>
    public List<BranchTreeNode> RemoteBranchTrees
    {
        get => _remoteBranchTrees;
        private set => SetProperty(ref _remoteBranchTrees, value);
    }

    /// <summary>ワークツリーの一覧。</summary>
    public List<Worktree> Worktrees
    {
        get => _worktrees;
        private set => SetProperty(ref _worktrees, value);
    }

    /// <summary>タグの一覧。</summary>
    public List<Models.Tag> Tags
    {
        get => _tags;
        private set => SetProperty(ref _tags, value);
    }

    /// <summary>タグをツリー形式で表示するかどうか。変更時に表示リストを再構築する。</summary>
    public bool ShowTagsAsTree
    {
        get => _uiStates.ShowTagsAsTree;
        set
        {
            if (value != _uiStates.ShowTagsAsTree)
            {
                _uiStates.ShowTagsAsTree = value;
                VisibleTags = BuildVisibleTags();
                OnPropertyChanged();
            }
        }
    }

    /// <summary>フィルタ・ソート適用後の表示用タグコレクション（ツリーまたはリスト）。</summary>
    public object VisibleTags
    {
        get => _visibleTags;
        private set => SetProperty(ref _visibleTags, value);
    }

    /// <summary>サブモジュールの一覧。</summary>
    public List<Models.Submodule> Submodules
    {
        get => _submodules;
        private set => SetProperty(ref _submodules, value);
    }

    /// <summary>サブモジュールをツリー形式で表示するかどうか。変更時に表示リストを再構築する。</summary>
    public bool ShowSubmodulesAsTree
    {
        get => _uiStates.ShowSubmodulesAsTree;
        set
        {
            if (value != _uiStates.ShowSubmodulesAsTree)
            {
                _uiStates.ShowSubmodulesAsTree = value;
                VisibleSubmodules = BuildVisibleSubmodules();
                OnPropertyChanged();
            }
        }
    }

    /// <summary>フィルタ適用後の表示用サブモジュールコレクション（ツリーまたはリスト）。</summary>
    public object VisibleSubmodules
    {
        get => _visibleSubmodules;
        private set => SetProperty(ref _visibleSubmodules, value);
    }

    /// <summary>ローカル変更ファイル数。タブのダーティ状態インジケーターに使用する。</summary>
    public int LocalChangesCount
    {
        get => _localChangesCount;
        private set => SetProperty(ref _localChangesCount, value);
    }

    /// <summary>スタッシュの総数。</summary>
    public int StashesCount
    {
        get => _stashesCount;
        private set => SetProperty(ref _stashesCount, value);
    }

    /// <summary>ローカルブランチの総数（デタッチHEADを除く）。</summary>
    public int LocalBranchesCount
    {
        get => _localBranchesCount;
        private set => SetProperty(ref _localBranchesCount, value);
    }

    /// <summary>未追跡ファイルをローカル変更に含めるかどうか。変更時にワーキングコピーを再取得する。</summary>
    public bool IncludeUntracked
    {
        get => _uiStates.IncludeUntrackedInLocalChanges;
        set
        {
            if (value != _uiStates.IncludeUntrackedInLocalChanges)
            {
                _uiStates.IncludeUntrackedInLocalChanges = value;
                OnPropertyChanged();
                RefreshWorkingCopyChanges();
            }
        }
    }

    /// <summary>コミット検索中かどうか。trueで履歴ビューに切り替え、falseで検索を終了する。</summary>
    public bool IsSearchingCommits
    {
        get => _isSearchingCommits;
        set
        {
            if (SetProperty(ref _isSearchingCommits, value))
            {
                // 検索開始時は履歴ビューに切り替え、終了時は検索コンテキストをクリア
                if (value)
                    SelectedViewIndex = 0;
                else
                    _searchCommitContext.EndSearch();
            }
        }
    }

    /// <summary>コミット検索コンテキスト。検索条件と結果を管理する。</summary>
    public SearchCommitContext SearchCommitContext
    {
        get => _searchCommitContext;
    }

    /// <summary>サイドバーのローカルブランチグループが展開されているかどうか。</summary>
    public bool IsLocalBranchGroupExpanded
    {
        get => _uiStates.IsLocalBranchesExpandedInSideBar;
        set
        {
            if (value != _uiStates.IsLocalBranchesExpandedInSideBar)
            {
                _uiStates.IsLocalBranchesExpandedInSideBar = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>サイドバーのリモートグループが展開されているかどうか。</summary>
    public bool IsRemoteGroupExpanded
    {
        get => _uiStates.IsRemotesExpandedInSideBar;
        set
        {
            if (value != _uiStates.IsRemotesExpandedInSideBar)
            {
                _uiStates.IsRemotesExpandedInSideBar = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>サイドバーのタググループが展開されているかどうか。</summary>
    public bool IsTagGroupExpanded
    {
        get => _uiStates.IsTagsExpandedInSideBar;
        set
        {
            if (value != _uiStates.IsTagsExpandedInSideBar)
            {
                _uiStates.IsTagsExpandedInSideBar = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>サイドバーのサブモジュールグループが展開されているかどうか。</summary>
    public bool IsSubmoduleGroupExpanded
    {
        get => _uiStates.IsSubmodulesExpandedInSideBar;
        set
        {
            if (value != _uiStates.IsSubmodulesExpandedInSideBar)
            {
                _uiStates.IsSubmodulesExpandedInSideBar = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>サイドバーのワークツリーグループが展開されているかどうか。</summary>
    public bool IsWorktreeGroupExpanded
    {
        get => _uiStates.IsWorktreeExpandedInSideBar;
        set
        {
            if (value != _uiStates.IsWorktreeExpandedInSideBar)
            {
                _uiStates.IsWorktreeExpandedInSideBar = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>ローカルブランチを名前順でソートするかどうか。falseの場合はコミッター日時順。変更時にツリーを再構築する。</summary>
    public bool IsSortingLocalBranchByName
    {
        get => _uiStates.LocalBranchSortMode == Models.BranchSortMode.Name;
        set
        {
            _uiStates.LocalBranchSortMode = value ? Models.BranchSortMode.Name : Models.BranchSortMode.CommitterDate;
            OnPropertyChanged();

            var builder = BuildBranchTree(_branches, _remotes);
            LocalBranchTrees = builder.Locals;
            RemoteBranchTrees = builder.Remotes;
        }
    }

    /// <summary>リモートブランチを名前順でソートするかどうか。falseの場合はコミッター日時順。変更時にツリーを再構築する。</summary>
    public bool IsSortingRemoteBranchByName
    {
        get => _uiStates.RemoteBranchSortMode == Models.BranchSortMode.Name;
        set
        {
            _uiStates.RemoteBranchSortMode = value ? Models.BranchSortMode.Name : Models.BranchSortMode.CommitterDate;
            OnPropertyChanged();

            var builder = BuildBranchTree(_branches, _remotes);
            LocalBranchTrees = builder.Locals;
            RemoteBranchTrees = builder.Remotes;
        }
    }

    /// <summary>タグを名前順でソートするかどうか。falseの場合は作成日時順。変更時に表示リストを再構築する。</summary>
    public bool IsSortingTagsByName
    {
        get => _uiStates.TagSortMode == Models.TagSortMode.Name;
        set
        {
            _uiStates.TagSortMode = value ? Models.TagSortMode.Name : Models.TagSortMode.CreatorDate;
            OnPropertyChanged();
            VisibleTags = BuildVisibleTags();
        }
    }

    /// <summary>進行中の操作コンテキスト（マージ/リベース/チェリーピック等）。ワーキングコピーから取得する。</summary>
    public InProgressContext InProgressContext
    {
        get => _workingCopy?.InProgressContext;
    }

    /// <summary>Bisectの状態（None/InProgress/Finished）。</summary>
    public Models.BisectState BisectState
    {
        get => _bisectState;
        private set => SetProperty(ref _bisectState, value);
    }

    /// <summary>Bisectコマンドが実行中かどうか。UIのボタン無効化に使用する。</summary>
    public bool IsBisectCommandRunning
    {
        get => _isBisectCommandRunning;
        private set => SetProperty(ref _isBisectCommandRunning, value);
    }

    /// <summary>自動フェッチが実行中かどうか。実行中はポップアップ作成を抑制する。</summary>
    public bool IsAutoFetching
    {
        get => _isAutoFetching;
        private set => SetProperty(ref _isAutoFetching, value);
    }

    /// <summary>課題トラッカー（Issue Tracker）のルール一覧。git configから読み込まれる。</summary>
    public AvaloniaList<Models.IssueTracker> IssueTrackers
    {
        get;
    } = [];

    /// <summary>コマンド実行ログの一覧。新しいログが先頭に追加される。</summary>
    public AvaloniaList<CommandLog> Logs
    {
        get;
    } = [];

    /// <summary>
    ///     コンストラクタ。リポジトリのパスを正規化し、ワークツリーの場合はcommondirを解決する。
    /// </summary>
    public Repository(bool isBare, string path, string gitDir)
    {
        IsBare = isBare;
        FullPath = path.Replace('\\', '/').TrimEnd('/');
        GitDir = gitDir.Replace('\\', '/').TrimEnd('/');

        // ワークツリーの場合、commondirファイルから共有gitディレクトリを取得
        var commonDirFile = Path.Combine(GitDir, "commondir");
        var isWorktree = GitDir.IndexOf("/worktrees/", StringComparison.Ordinal) > 0 &&
                      File.Exists(commonDirFile);

        if (isWorktree)
        {
            // commondirが相対パスの場合はGitDirを基準に絶対パスに変換
            var commonDir = File.ReadAllText(commonDirFile).Trim();
            if (Path.IsPathRooted(commonDir))
                commonDir = new DirectoryInfo(commonDir).FullName;
            else
                commonDir = new DirectoryInfo(Path.Combine(GitDir, commonDir)).FullName;

            _gitCommonDir = commonDir.Replace('\\', '/').TrimEnd('/');
        }
        else
        {
            _gitCommonDir = GitDir;
        }
    }

    /// <summary>
    ///     リポジトリを開く。設定・UI状態の読み込み、ファイル監視の開始、
    ///     各種ビューの初期化、自動フェッチタイマーの起動、全データの更新を行う。
    /// </summary>
    public void Open()
    {
        _settings = Models.RepositorySettings.Get(_gitCommonDir);
        _uiStates = Models.RepositoryUIStates.Load(GitDir);

        try
        {
            _watcher = new Models.Watcher(this, FullPath, _gitCommonDir);
        }
        catch (Exception ex)
        {
            App.RaiseException(string.Empty, App.Text("Error.FailedToStartWatcher", FullPath, ex.Message));
        }

        _historyFilterMode = _uiStates.GetHistoryFilterMode();
        _histories = new Histories(this);
        _workingCopy = new WorkingCopy(this) { CommitMessage = _uiStates.LastCommitMessage };
        _stashesPage = new StashesPage(this);
        _searchCommitContext = new SearchCommitContext(this);

        if (Preferences.Instance.ShowLocalChangesByDefault)
        {
            _selectedView = _workingCopy;
            _selectedViewIndex = 1;
        }
        else
        {
            _selectedView = _histories;
            _selectedViewIndex = 0;
        }

        _lastFetchTime = DateTime.Now;
        _autoFetchTimer = new Timer(AutoFetchByTimer, null, 5000, 5000);
        RefreshAll();
    }

    /// <summary>
    ///     リポジトリを閉じる。全ての非同期タスクをキャンセルし、
    ///     タイマー・ウォッチャー・ビューを解放してリソースをクリーンアップする。
    /// </summary>
    public void Close()
    {
        SelectedView = null; // Do NOT modify. Used to remove exists widgets for GC.Collect
        Logs.Clear();

        _uiStates.Unload(_workingCopy.CommitMessage);

        _cancellationRefreshBranches?.Cancel();
        _cancellationRefreshBranches?.Dispose();
        _cancellationRefreshTags?.Cancel();
        _cancellationRefreshTags?.Dispose();
        _cancellationRefreshWorkingCopyChanges?.Cancel();
        _cancellationRefreshWorkingCopyChanges?.Dispose();
        _cancellationRefreshCommits?.Cancel();
        _cancellationRefreshCommits?.Dispose();
        _cancellationRefreshStashes?.Cancel();
        _cancellationRefreshStashes?.Dispose();

        _filterDebounceTimer?.Dispose();
        _filterDebounceTimer = null;

        _autoFetchTimer.Dispose();
        _autoFetchTimer = null;

        _settings = null;
        _uiStates = null;
        _historyFilterMode = Models.FilterMode.None;

        _watcher?.Dispose();
        _histories.Dispose();
        _workingCopy.Dispose();
        _stashesPage.Dispose();
        _searchCommitContext.Dispose();

        _watcher = null;
        _histories = null;
        _workingCopy = null;
        _stashesPage = null;

        _localChangesCount = 0;
        _stashesCount = 0;

        _remotes.Clear();
        _branches.Clear();
        _localBranchTrees.Clear();
        _remoteBranchTrees.Clear();
        _tags.Clear();
        _visibleTags = null;
        _submodules.Clear();
        _visibleSubmodules = null;
    }

    /// <summary>新しいポップアップを作成できるかどうか。自動フェッチ中またはポップアップ実行中はfalse。</summary>
    public bool CanCreatePopup()
    {
        var page = GetOwnerPage();
        if (page is null)
            return false;

        return !_isAutoFetching && page.CanCreatePopup();
    }

    /// <summary>指定されたポップアップダイアログを表示する。</summary>
    public void ShowPopup(Popup popup)
    {
        var page = GetOwnerPage();
        if (page is not null)
            page.Popup = popup;
    }

    /// <summary>ポップアップを表示し、直接開始可能であれば即座に処理を実行する。</summary>
    public async Task ShowAndStartPopupAsync(Popup popup)
    {
        var page = GetOwnerPage();
        page.Popup = popup;

        if (popup.CanStartDirectly())
            await page.ProcessPopupAsync();
    }

    /// <summary>Git Flowが有効かどうか。設定が有効でmaster/developブランチが存在する場合にtrue。</summary>
    public bool IsGitFlowEnabled()
    {
        return GitFlow is { IsValid: true } &&
            _branches.Find(x => x.IsLocal && x.Name.Equals(GitFlow.Master, StringComparison.Ordinal)) is not null &&
            _branches.Find(x => x.IsLocal && x.Name.Equals(GitFlow.Develop, StringComparison.Ordinal)) is not null;
    }

    /// <summary>指定ブランチのGit Flowタイプ（Feature/Release/Hotfix）を判定する。</summary>
    public Models.GitFlowBranchType GetGitFlowType(Models.Branch b)
    {
        if (!IsGitFlowEnabled())
            return Models.GitFlowBranchType.None;

        var name = b.Name;
        if (name.StartsWith(GitFlow.FeaturePrefix, StringComparison.Ordinal))
            return Models.GitFlowBranchType.Feature;
        if (name.StartsWith(GitFlow.ReleasePrefix, StringComparison.Ordinal))
            return Models.GitFlowBranchType.Release;
        if (name.StartsWith(GitFlow.HotfixPrefix, StringComparison.Ordinal))
            return Models.GitFlowBranchType.Hotfix;
        return Models.GitFlowBranchType.None;
    }

    /// <summary>Git LFSが有効かどうか。pre-pushフックの存在とLFSコマンドの含有を確認する。</summary>
    public bool IsLFSEnabled()
    {
        var path = Path.Combine(FullPath, ".git", "hooks", "pre-push");
        if (!File.Exists(path))
            return false;

        try
        {
            var content = File.ReadAllText(path);
            return content.Contains("git lfs pre-push");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Git LFSをこのリポジトリにインストールする。</summary>
    public async Task InstallLFSAsync()
    {
        var log = CreateLog("Install LFS");
        var succ = await new Commands.LFS(FullPath).Use(log).InstallAsync();
        if (succ)
            App.SendNotification(FullPath, "LFS enabled successfully!");

        log.Complete();
    }

    /// <summary>指定パターンでLFSトラッキングを追加する。</summary>
    public async Task<bool> TrackLFSFileAsync(string pattern, bool isFilenameMode)
    {
        var log = CreateLog("Track LFS");
        var succ = await new Commands.LFS(FullPath)
            .Use(log)
            .TrackAsync(pattern, isFilenameMode);

        if (succ)
            App.SendNotification(FullPath, $"Tracking successfully! Pattern: {pattern}");

        log.Complete();
        return succ;
    }

    /// <summary>LFSファイルをロックする。</summary>
    public async Task<bool> LockLFSFileAsync(string remote, string path)
    {
        var log = CreateLog("Lock LFS File");
        var succ = await new Commands.LFS(FullPath)
            .Use(log)
            .LockAsync(remote, path);

        if (succ)
            App.SendNotification(FullPath, $"Lock file successfully! File: {path}");

        log.Complete();
        return succ;
    }

    /// <summary>LFSファイルのロックを解除する。forceがtrueの場合は他ユーザーのロックも解除する。</summary>
    public async Task<bool> UnlockLFSFileAsync(string remote, string path, bool force, bool notify)
    {
        var log = CreateLog("Unlock LFS File");
        var succ = await new Commands.LFS(FullPath)
            .Use(log)
            .UnlockAsync(remote, path, force);

        if (succ && notify)
            App.SendNotification(FullPath, $"Unlock file successfully! File: {path}");

        log.Complete();
        return succ;
    }

    /// <summary>新しいコマンドログを作成し、ログ一覧の先頭に追加する。</summary>
    public CommandLog CreateLog(string name)
    {
        var log = new CommandLog(name);
        Logs.Insert(0, log);
        return log;
    }

    /// <summary>
    ///     全データを一括更新する。コミット、ブランチ、タグ、サブモジュール、
    ///     ワークツリー、ワーキングコピー、スタッシュ、課題トラッカー、Git Flow設定を再取得する。
    /// </summary>
    public void RefreshAll()
    {
        RefreshCommits();
        RefreshBranches();
        RefreshTags();
        RefreshSubmodules();
        RefreshWorktrees();
        RefreshWorkingCopyChanges();
        RefreshStashes();

        Task.Run(async () =>
        {
            var issuetrackers = new List<Models.IssueTracker>();
            await new Commands.IssueTracker(FullPath, true).ReadAllAsync(issuetrackers, true).ConfigureAwait(false);
            await new Commands.IssueTracker(FullPath, false).ReadAllAsync(issuetrackers, false).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() =>
            {
                IssueTrackers.Clear();
                IssueTrackers.AddRange(issuetrackers);
            });

            var config = await new Commands.Config(FullPath).ReadAllAsync().ConfigureAwait(false);
            _hasAllowedSignersFile = config.TryGetValue("gpg.ssh.allowedsignersfile", out var allowedSignersFile) && !string.IsNullOrEmpty(allowedSignersFile);

            if (config.TryGetValue("gitflow.branch.master", out var masterName))
                GitFlow.Master = masterName;
            if (config.TryGetValue("gitflow.branch.develop", out var developName))
                GitFlow.Develop = developName;
            if (config.TryGetValue("gitflow.prefix.feature", out var featurePrefix))
                GitFlow.FeaturePrefix = featurePrefix;
            if (config.TryGetValue("gitflow.prefix.release", out var releasePrefix))
                GitFlow.ReleasePrefix = releasePrefix;
            if (config.TryGetValue("gitflow.prefix.hotfix", out var hotfixPrefix))
                GitFlow.HotfixPrefix = hotfixPrefix;
        });
    }

    /// <summary>フェッチダイアログを表示する。autoStartがtrueの場合は即座に実行を開始する。</summary>
    public async Task FetchAsync(bool autoStart)
    {
        if (!CanCreatePopup())
            return;

        if (_remotes.Count == 0)
        {
            App.RaiseException(FullPath, App.Text("Error.NoRemotes"));
            return;
        }

        if (autoStart)
            await ShowAndStartPopupAsync(new Fetch(this));
        else
            ShowPopup(new Fetch(this));
    }

    /// <summary>プルダイアログを表示する。autoStartがtrueで上流ブランチがある場合は即座に実行を開始する。</summary>
    public async Task PullAsync(bool autoStart)
    {
        if (IsBare || !CanCreatePopup())
            return;

        if (_remotes.Count == 0)
        {
            App.RaiseException(FullPath, App.Text("Error.NoRemotes"));
            return;
        }

        if (_currentBranch is null)
        {
            App.RaiseException(FullPath, App.Text("Error.CanNotFindBranch"));
            return;
        }

        var pull = new Pull(this, null);
        if (autoStart && pull.SelectedBranch is not null)
            await ShowAndStartPopupAsync(pull);
        else
            ShowPopup(pull);
    }

    /// <summary>プッシュダイアログを表示する。autoStartがtrueの場合は即座に実行を開始する。</summary>
    public async Task PushAsync(bool autoStart)
    {
        if (!CanCreatePopup())
            return;

        if (_remotes.Count == 0)
        {
            App.RaiseException(FullPath, App.Text("Error.NoRemotes"));
            return;
        }

        if (_currentBranch is null)
        {
            App.RaiseException(FullPath, App.Text("Error.CanNotFindBranch"));
            return;
        }

        if (autoStart)
            await ShowAndStartPopupAsync(new Push(this, null));
        else
            ShowPopup(new Push(this, null));
    }

    /// <summary>パッチ適用ダイアログを表示する。</summary>
    public void ApplyPatch()
    {
        if (CanCreatePopup())
            ShowPopup(new Apply(this));
    }

    /// <summary>カスタムアクションを実行する。コントロールがない場合は即座に開始、ある場合はダイアログを表示する。</summary>
    public async Task ExecCustomActionAsync(Models.CustomAction action, object scopeTarget)
    {
        if (!CanCreatePopup())
            return;

        var popup = new ExecuteCustomAction(this, action, scopeTarget);
        if (action.Controls.Count == 0)
            await ShowAndStartPopupAsync(popup);
        else
            ShowPopup(popup);
    }

    /// <summary>リポジトリのクリーンアップ（git gc）を実行する。</summary>
    public async Task CleanupAsync()
    {
        if (CanCreatePopup())
            await ShowAndStartPopupAsync(new Cleanup(this));
    }

    /// <summary>インデックスキャッシュをクリアする。</summary>
    public async Task ClearIndexCacheAsync()
    {
        if (CanCreatePopup())
            await ShowAndStartPopupAsync(new ClearIndexCache(this));
    }

    /// <summary>サイドバーのフィルタをクリアする。デバウンスをバイパスして即時適用する。</summary>
    public void ClearFilter()
    {
        _filterDebounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);

        if (SetProperty(ref _filter, string.Empty, nameof(Filter)))
            ApplyFilter();
    }

    /// <summary>ファイル監視を一時的にロックする。IDisposableのDisposeでロック解除される。</summary>
    public IDisposable LockWatcher()
    {
        return _watcher?.Lock();
    }

    /// <summary>ブランチデータを手動で更新する。ブランチ、コミット、ワーキングコピー、ワークツリーを再取得する。</summary>
    public void MarkBranchesDirtyManually()
    {
        _watcher?.MarkBranchUpdated();
        RefreshBranches();
        RefreshCommits();
        RefreshWorkingCopyChanges();
        RefreshWorktrees();
    }

    /// <summary>タグデータを手動で更新する。タグとコミットを再取得する。</summary>
    public void MarkTagsDirtyManually()
    {
        _watcher?.MarkTagUpdated();
        RefreshTags();
        RefreshCommits();
    }

    /// <summary>ワーキングコピーデータを手動で更新する。</summary>
    public void MarkWorkingCopyDirtyManually()
    {
        _watcher?.MarkWorkingCopyUpdated();
        RefreshWorkingCopyChanges();
    }

    /// <summary>スタッシュデータを手動で更新する。</summary>
    public void MarkStashesDirtyManually()
    {
        _watcher?.MarkStashUpdated();
        RefreshStashes();
    }

    /// <summary>サブモジュールデータを手動で更新する。</summary>
    public void MarkSubmodulesDirtyManually()
    {
        _watcher?.MarkSubmodulesUpdated();
        RefreshSubmodules();
    }

    /// <summary>最終フェッチ時刻を現在時刻に更新する。自動フェッチのインターバルリセットに使用する。</summary>
    public void MarkFetched()
    {
        _lastFetchTime = DateTime.Now;
    }

    /// <summary>指定されたSHAのコミットに履歴ビューをナビゲートする。遅延モードではコミット読み込み完了後にナビゲートする。</summary>
    public void NavigateToCommit(string sha, bool isDelayMode = false)
    {
        if (isDelayMode)
        {
            _navigateToCommitDelayed = sha;
        }
        else
        {
            SelectedViewIndex = 0;
            _histories?.NavigateTo(sha);
        }
    }

    /// <summary>ワーキングコピーのコミットメッセージを設定する。</summary>
    public void SetCommitMessage(string message)
    {
        if (_workingCopy is not null)
            _workingCopy.CommitMessage = message;
    }

    /// <summary>ワーキングコピーのコミットメッセージをクリアする。</summary>
    public void ClearCommitMessage()
    {
        if (_workingCopy is not null)
            _workingCopy.CommitMessage = string.Empty;
    }

    /// <summary>履歴ビューで現在選択中のコミットを取得する。</summary>
    public Models.Commit GetSelectedCommitInHistory()
    {
        return (_histories?.DetailContext as CommitDetail)?.Commit;
    }

    /// <summary>全ての履歴フィルタをクリアし、ブランチツリーとタグのフィルタモードもリセットする。</summary>
    public void ClearHistoryFilters()
    {
        _uiStates.HistoryFilters.Clear();
        HistoryFilterMode = Models.FilterMode.None;

        ResetBranchTreeFilterMode(LocalBranchTrees);
        ResetBranchTreeFilterMode(RemoteBranchTrees);
        ResetTagFilterMode();
        RefreshCommits();
    }

    /// <summary>指定された履歴フィルタを削除し、フィルタモードを更新する。</summary>
    public void RemoveHistoryFilter(Models.HistoryFilter filter)
    {
        if (_uiStates.HistoryFilters.Remove(filter))
        {
            HistoryFilterMode = _uiStates.GetHistoryFilterMode();
            RefreshHistoryFilters(true);
        }
    }

    /// <summary>サイドバーの全セクションとブランチツリーの全フォルダノードを展開する。</summary>
    public void ExpandAllBranchNodes()
    {
        IsLocalBranchGroupExpanded = true;
        IsRemoteGroupExpanded = true;
        IsTagGroupExpanded = true;
        IsSubmoduleGroupExpanded = true;
        IsWorktreeGroupExpanded = true;

        var builder = BuildBranchTree(_branches, _remotes, true);
        LocalBranchTrees = builder.Locals;
        RemoteBranchTrees = builder.Remotes;
    }

    /// <summary>サイドバーの全セクションとブランチツリーの全フォルダノードを折りたたむ。</summary>
    public void CollapseAllBranchNodes()
    {
        IsLocalBranchGroupExpanded = false;
        IsRemoteGroupExpanded = false;
        IsTagGroupExpanded = false;
        IsSubmoduleGroupExpanded = false;
        IsWorktreeGroupExpanded = false;

        _uiStates.ExpandedBranchNodesInSideBar = [];

        var builder = BuildBranchTree(_branches, _remotes);
        LocalBranchTrees = builder.Locals;
        RemoteBranchTrees = builder.Remotes;
    }

    /// <summary>ブランチツリーノードの展開状態をUI状態に永続化する。フィルタ適用中は無視する。</summary>
    public void UpdateBranchNodeIsExpanded(BranchTreeNode node)
    {
        if (_uiStates is null || !string.IsNullOrWhiteSpace(_filter))
            return;

        if (node.IsExpanded)
        {
            if (!_uiStates.ExpandedBranchNodesInSideBar.Contains(node.Path))
                _uiStates.ExpandedBranchNodesInSideBar.Add(node.Path);
        }
        else
        {
            _uiStates.ExpandedBranchNodesInSideBar.Remove(node.Path);
        }
    }

    /// <summary>指定タグの履歴フィルタモードを設定し、フィルタを更新する。</summary>
    public void SetTagFilterMode(Models.Tag tag, Models.FilterMode mode)
    {
        var changed = _uiStates.UpdateHistoryFilters(tag.Name, Models.FilterType.Tag, mode);
        if (changed)
            RefreshHistoryFilters(true);
    }

    /// <summary>指定ブランチの履歴フィルタモードを設定する。ブランチオブジェクトからノードを検索して委譲する。</summary>
    public void SetBranchFilterMode(Models.Branch branch, Models.FilterMode mode, bool clearExists, bool refresh)
    {
        var node = FindBranchNode(branch.IsLocal ? _localBranchTrees : _remoteBranchTrees, branch.FullName);
        if (node is not null)
            SetBranchFilterMode(node, mode, clearExists, refresh);
    }

    /// <summary>
    ///     指定ブランチツリーノードの履歴フィルタモードを設定する。
    ///     ブランチの場合は上流も連動、フォルダの場合は子ブランチのフィルタを削除する。
    ///     親フォルダのフィルタモードもリセットする。
    /// </summary>
    public void SetBranchFilterMode(BranchTreeNode node, Models.FilterMode mode, bool clearExists, bool refresh)
    {
        var isLocal = node.Path.StartsWith("refs/heads/", StringComparison.Ordinal);
        var tree = isLocal ? _localBranchTrees : _remoteBranchTrees;

        if (clearExists)
        {
            _uiStates.HistoryFilters.Clear();
            HistoryFilterMode = Models.FilterMode.None;
        }

        if (node.Backend is Models.Branch branch)
        {
            var type = isLocal ? Models.FilterType.LocalBranch : Models.FilterType.RemoteBranch;
            var changed = _uiStates.UpdateHistoryFilters(node.Path, type, mode);
            if (!changed)
                return;

            if (isLocal && !string.IsNullOrEmpty(branch.Upstream) && !branch.IsUpstreamGone)
                _uiStates.UpdateHistoryFilters(branch.Upstream, Models.FilterType.RemoteBranch, mode);
        }
        else
        {
            var type = isLocal ? Models.FilterType.LocalBranchFolder : Models.FilterType.RemoteBranchFolder;
            var changed = _uiStates.UpdateHistoryFilters(node.Path, type, mode);
            if (!changed)
                return;

            _uiStates.RemoveBranchFiltersByPrefix(node.Path);
        }

        var parentType = isLocal ? Models.FilterType.LocalBranchFolder : Models.FilterType.RemoteBranchFolder;
        var cur = node;
        do
        {
            var lastSepIdx = cur.Path.LastIndexOf('/');
            if (lastSepIdx <= 0)
                break;

            var parentPath = cur.Path.Substring(0, lastSepIdx);
            var parent = FindBranchNode(tree, parentPath);
            if (parent is null)
                break;

            _uiStates.UpdateHistoryFilters(parent.Path, parentType, Models.FilterMode.None);
            cur = parent;
        } while (true);

        RefreshHistoryFilters(refresh);
    }

    /// <summary>全変更をスタッシュするダイアログを表示する。autoStartがtrueの場合は即座に実行する。</summary>
    public async Task StashAllAsync(bool autoStart)
    {
        if (!CanCreatePopup())
            return;

        var popup = new StashChanges(this, null);
        if (autoStart)
            await ShowAndStartPopupAsync(popup);
        else
            ShowPopup(popup);
    }

    /// <summary>マージ/リベース/チェリーピックのスキップを実行する。</summary>
    public async Task SkipMergeAsync()
    {
        if (_workingCopy is not null)
            await _workingCopy.SkipMergeAsync();
    }

    /// <summary>マージ/リベース/チェリーピックの中止を実行する。</summary>
    public async Task AbortMergeAsync()
    {
        if (_workingCopy is not null)
            await _workingCopy.AbortMergeAsync();
    }

    /// <summary>指定スコープのカスタムアクション一覧を取得する。グローバルとリポジトリ固有の両方を含む。</summary>
    public List<(Models.CustomAction, CustomActionContextMenuLabel)> GetCustomActions(Models.CustomActionScope scope)
    {
        var actions = new List<(Models.CustomAction, CustomActionContextMenuLabel)>();

        foreach (var act in Preferences.Instance.CustomActions)
        {
            if (act.Scope == scope)
                actions.Add((act, new CustomActionContextMenuLabel(act.Name, true)));
        }

        foreach (var act in _settings.CustomActions)
        {
            if (act.Scope == scope)
                actions.Add((act, new CustomActionContextMenuLabel(act.Name, false)));
        }

        return actions;
    }

    /// <summary>
    ///     Bisectサブコマンド（start/good/bad/reset等）を実行する。
    ///     完了後にブランチを更新し、HEADにナビゲートする。
    /// </summary>
    public async Task ExecBisectCommandAsync(string subcmd)
    {
        using var lockWatcher = _watcher?.Lock();
        IsBisectCommandRunning = true;

        var log = CreateLog($"Bisect({subcmd})");

        var succ = await new Commands.Bisect(FullPath, subcmd).Use(log).ExecAsync();
        log.Complete();

        var head = await new Commands.QueryRevisionByRefName(FullPath, "HEAD").GetResultAsync();
        var nlIdx = log.Content.IndexOf('\n');
        var bisectMsg = nlIdx >= 0 ? log.Content.Substring(nlIdx).Trim() : log.Content.Trim();
        if (!succ)
            App.RaiseException(FullPath, bisectMsg);
        else if (log.Content.Contains("is the first bad commit"))
            App.SendNotification(FullPath, bisectMsg);

        MarkBranchesDirtyManually();
        NavigateToCommit(head, true);
        IsBisectCommandRunning = false;
    }

    /// <summary>サブモジュールが存在する可能性があるかを判定する（.gitmodulesの存在とサイズで判断）。</summary>
    public bool MayHaveSubmodules()
    {
        var modulesFile = Path.Combine(FullPath, ".gitmodules");
        var info = new FileInfo(modulesFile);
        return info.Exists && info.Length > 20;
    }

    /// <summary>
    ///     ブランチとリモートの一覧を非同期で再取得し、ブランチツリーを再構築する。
    ///     前回の取得中タスクがあればキャンセルする。
    /// </summary>
    public void RefreshBranches()
    {
        if (_cancellationRefreshBranches is { IsCancellationRequested: false })
            _cancellationRefreshBranches.Cancel();

        _cancellationRefreshBranches?.Dispose();
        _cancellationRefreshBranches = new CancellationTokenSource();
        var token = _cancellationRefreshBranches.Token;

        Task.Run(async () =>
        {
            var branchesTask = new Commands.QueryBranches(FullPath).GetResultAsync();
            var remotesTask = new Commands.QueryRemotes(FullPath).GetResultAsync();
            await Task.WhenAll(branchesTask, remotesTask).ConfigureAwait(false);

            var branches = branchesTask.Result;
            var remotes = remotesTask.Result;
            var builder = BuildBranchTree(branches, remotes);

            Dispatcher.UIThread.Invoke(() =>
            {
                if (token.IsCancellationRequested)
                    return;

                Remotes = remotes;
                Branches = branches;
                CurrentBranch = branches.Find(x => x.IsCurrent);
                LocalBranchTrees = builder.Locals;
                RemoteBranchTrees = builder.Remotes;

                var localBranchesCount = 0;
                foreach (var b in branches)
                {
                    if (b.IsLocal && !b.IsDetachedHead)
                        localBranchesCount++;
                }
                LocalBranchesCount = localBranchesCount;

                if (_workingCopy is not null)
                    _workingCopy.HasRemotes = remotes.Count > 0;

                var hasPendingPullOrPush = CurrentBranch?.IsTrackStatusVisible ?? false;
                GetOwnerPage()?.ChangeDirtyState(Models.DirtyState.HasPendingPullOrPush, !hasPendingPullOrPush);
            });
        }, token);
    }

    /// <summary>ワークツリー一覧を非同期で再取得し、UIに反映する。</summary>
    public void RefreshWorktrees()
    {
        Task.Run(async () =>
        {
            var worktrees = await new Commands.Worktree(FullPath).ReadAllAsync().ConfigureAwait(false);
            var cleaned = Worktree.Build(FullPath, worktrees);
            Dispatcher.UIThread.Invoke(() => Worktrees = cleaned);
        });
    }

    /// <summary>
    ///     タグ一覧を非同期で再取得し、表示用タグリストを再構築する。
    ///     前回の取得中タスクがあればキャンセルする。
    /// </summary>
    public void RefreshTags()
    {
        if (_cancellationRefreshTags is { IsCancellationRequested: false })
            _cancellationRefreshTags.Cancel();

        _cancellationRefreshTags?.Dispose();
        _cancellationRefreshTags = new CancellationTokenSource();
        var token = _cancellationRefreshTags.Token;

        Task.Run(async () =>
        {
            var tags = await new Commands.QueryTags(FullPath).GetResultAsync().ConfigureAwait(false);
            Dispatcher.UIThread.Invoke(() =>
            {
                if (token.IsCancellationRequested)
                    return;

                Tags = tags;
                VisibleTags = BuildVisibleTags();
            });
        }, token);
    }

    /// <summary>
    ///     コミット履歴を非同期で再取得し、コミットグラフを再構築する。
    ///     フィルタ条件と最大件数を適用する。前回の取得中タスクがあればキャンセルする。
    /// </summary>
    public void RefreshCommits()
    {
        if (_cancellationRefreshCommits is { IsCancellationRequested: false })
            _cancellationRefreshCommits.Cancel();

        _cancellationRefreshCommits?.Dispose();
        _cancellationRefreshCommits = new CancellationTokenSource();
        var token = _cancellationRefreshCommits.Token;

        Task.Run(async () =>
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() => _histories.IsLoading = true);

                var builder = new StringBuilder();
                builder
                    .Append('-').Append(Preferences.Instance.MaxHistoryCommits).Append(' ')
                    .Append(_uiStates.BuildHistoryParams());

                Models.Logger.Log($"RefreshCommits開始: {FullPath}", Models.LogLevel.Debug);
                var commits = await new Commands.QueryCommits(FullPath, builder.ToString()).GetResultAsync().ConfigureAwait(false);
                Models.Logger.Log($"RefreshCommits: {commits.Count}件取得、グラフ解析開始", Models.LogLevel.Debug);
                var graph = Models.CommitGraph.Parse(commits, _uiStates.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.FirstParentOnly));

                Dispatcher.UIThread.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    if (_histories is not null)
                    {
                        _histories.IsLoading = false;
                        _histories.Commits = commits;
                        _histories.Graph = graph;

                        BisectState = _histories.UpdateBisectInfo();

                        if (!string.IsNullOrEmpty(_navigateToCommitDelayed))
                            NavigateToCommit(_navigateToCommitDelayed);
                    }

                    _navigateToCommitDelayed = string.Empty;
                });
            }
            catch (Exception ex)
            {
                Models.Logger.LogException($"RefreshCommits失敗: {FullPath}", ex);
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (_histories is not null)
                        _histories.IsLoading = false;
                });
            }
        }, token);
    }

    /// <summary>
    ///     サブモジュール一覧を非同期で再取得する。
    ///     .gitmodulesが存在しない場合はクリアし、変更がある場合のみUIを更新する。
    /// </summary>
    public void RefreshSubmodules()
    {
        if (!MayHaveSubmodules())
        {
            if (_submodules.Count > 0)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    Submodules = [];
                    VisibleSubmodules = BuildVisibleSubmodules();
                });
            }

            return;
        }

        Task.Run(async () =>
        {
            List<Models.Submodule> submodules;
            try
            {
                submodules = await new Commands.QuerySubmodules(FullPath).GetResultAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Models.Logger.LogException($"RefreshSubmodules失敗: {FullPath}", ex);
                return;
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                bool hasChanged = _submodules.Count != submodules.Count;
                if (!hasChanged)
                {
                    var old = new Dictionary<string, Models.Submodule>();
                    foreach (var module in _submodules)
                        old.Add(module.Path, module);

                    foreach (var module in submodules)
                    {
                        if (!old.TryGetValue(module.Path, out var exist))
                        {
                            hasChanged = true;
                            break;
                        }

                        hasChanged = !exist.SHA.Equals(module.SHA, StringComparison.Ordinal) ||
                                     !exist.Branch.Equals(module.Branch, StringComparison.Ordinal) ||
                                     !exist.URL.Equals(module.URL, StringComparison.Ordinal) ||
                                     exist.Status != module.Status;

                        if (hasChanged)
                            break;
                    }
                }

                if (hasChanged)
                {
                    Submodules = submodules;
                    VisibleSubmodules = BuildVisibleSubmodules();
                }
            });
        });
    }

    /// <summary>
    ///     ワーキングコピーの変更一覧を非同期で再取得する。
    ///     ベアリポジトリの場合は何もしない。前回の取得中タスクがあればキャンセルする。
    /// </summary>
    public void RefreshWorkingCopyChanges()
    {
        if (IsBare)
            return;

        if (_cancellationRefreshWorkingCopyChanges is { IsCancellationRequested: false })
            _cancellationRefreshWorkingCopyChanges.Cancel();

        _cancellationRefreshWorkingCopyChanges?.Dispose();
        _cancellationRefreshWorkingCopyChanges = new CancellationTokenSource();
        var token = _cancellationRefreshWorkingCopyChanges.Token;
        var noOptionalLocks = Interlocked.Add(ref _queryLocalChangesTimes, 1) > 1;

        Task.Run(async () =>
        {
            try
            {
                var changes = await new Commands.QueryLocalChanges(FullPath, _uiStates.IncludeUntrackedInLocalChanges, noOptionalLocks)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                if (_workingCopy is null || token.IsCancellationRequested)
                    return;

                changes.Sort((l, r) => Models.NumericSort.Compare(l.Path, r.Path));
                _workingCopy.SetData(changes, token);

                Dispatcher.UIThread.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    LocalChangesCount = changes.Count;
                    OnPropertyChanged(nameof(InProgressContext));
                    GetOwnerPage()?.ChangeDirtyState(Models.DirtyState.HasLocalChanges, changes.Count == 0);
                });
            }
            catch (Exception ex)
            {
                Models.Logger.LogException($"RefreshWorkingCopyChanges失敗: {FullPath}", ex);
            }
        }, token);
    }

    /// <summary>
    ///     スタッシュ一覧を非同期で再取得する。
    ///     ベアリポジトリの場合は何もしない。前回の取得中タスクがあればキャンセルする。
    /// </summary>
    public void RefreshStashes()
    {
        if (IsBare)
            return;

        if (_cancellationRefreshStashes is { IsCancellationRequested: false })
            _cancellationRefreshStashes.Cancel();

        _cancellationRefreshStashes?.Dispose();
        _cancellationRefreshStashes = new CancellationTokenSource();
        var token = _cancellationRefreshStashes.Token;

        Task.Run(async () =>
        {
            try
            {
                var stashes = await new Commands.QueryStashes(FullPath).GetResultAsync().ConfigureAwait(false);
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    if (_stashesPage is not null)
                        _stashesPage.Stashes = stashes;

                    StashesCount = stashes.Count;
                });
            }
            catch (Exception ex)
            {
                Models.Logger.LogException($"RefreshStashes失敗: {FullPath}", ex);
            }
        }, token);
    }

    /// <summary>履歴表示フラグ（First Parent Only等）をトグルする。</summary>
    public void ToggleHistoryShowFlag(Models.HistoryShowFlags flag)
    {
        if (_uiStates.HistoryShowFlags.HasFlag(flag))
            HistoryShowFlags -= flag;
        else
            HistoryShowFlags |= flag;
    }

    /// <summary>新しいブランチ作成ダイアログを表示する。初回コミット前はエラーを表示する。</summary>
    public void CreateNewBranch()
    {
        if (_currentBranch is null)
        {
            App.RaiseException(FullPath, App.Text("Error.NoBranchBeforeFirstCommit"));
            return;
        }

        if (CanCreatePopup())
            ShowPopup(new CreateBranch(this, _currentBranch));
    }

    /// <summary>
    ///     指定ブランチをチェックアウトする。
    ///     ローカルブランチがワークツリーに紐付いている場合はワークツリーを開く。
    ///     リモートブランチの場合はローカル追跡ブランチがあればそちらを使用し、
    ///     なければ新規ブランチ作成ダイアログを表示する。
    /// </summary>
    public async Task CheckoutBranchAsync(Models.Branch branch)
    {
        if (branch.IsLocal)
        {
            var worktree = _worktrees.Find(x => x.IsAttachedTo(branch));
            if (worktree is not null)
            {
                OpenWorktree(worktree);
                return;
            }
        }

        if (IsBare)
            return;

        if (!CanCreatePopup())
            return;

        if (branch.IsLocal)
        {
            await ShowAndStartPopupAsync(new Checkout(this, branch.Name));
        }
        else
        {
            foreach (var b in _branches)
            {
                if (b.IsLocal &&
                    b.Upstream.Equals(branch.FullName, StringComparison.Ordinal) &&
                    b.Ahead.Count == 0)
                {
                    if (b.Behind.Count > 0)
                        ShowPopup(new CheckoutAndFastForward(this, b, branch));
                    else if (!b.IsCurrent)
                        await CheckoutBranchAsync(b);

                    return;
                }
            }

            ShowPopup(new CreateBranch(this, branch));
        }
    }

    /// <summary>指定タグのコミットを取得し、そのコミットに基づいてブランチをチェックアウトする。</summary>
    public async Task CheckoutTagAsync(Models.Tag tag)
    {
        var c = await new Commands.QuerySingleCommit(FullPath, tag.SHA).GetResultAsync();
        if (c is not null && _histories is not null)
            await _histories.CheckoutBranchByCommitAsync(c);
    }

    /// <summary>ブランチ削除の確認ダイアログを表示する。</summary>
    public void DeleteBranch(Models.Branch branch)
    {
        if (CanCreatePopup())
            ShowPopup(new DeleteBranch(this, branch));
    }

    /// <summary>複数ブランチの一括削除確認ダイアログを表示する。</summary>
    public void DeleteMultipleBranches(List<Models.Branch> branches, bool isLocal)
    {
        if (CanCreatePopup())
            ShowPopup(new DeleteMultipleBranches(this, branches, isLocal));
    }

    /// <summary>複数ブランチのマージダイアログを表示する。</summary>
    public void MergeMultipleBranches(List<Models.Branch> branches)
    {
        if (CanCreatePopup())
            ShowPopup(new MergeMultiple(this, branches));
    }

    /// <summary>新しいタグ作成ダイアログを表示する。初回コミット前はエラーを表示する。</summary>
    public void CreateNewTag()
    {
        if (_currentBranch is null)
        {
            App.RaiseException(FullPath, App.Text("Error.NoBranchBeforeFirstCommit"));
            return;
        }

        if (CanCreatePopup())
            ShowPopup(new CreateTag(this, _currentBranch));
    }

    /// <summary>タグ削除の確認ダイアログを表示する。</summary>
    public void DeleteTag(Models.Tag tag)
    {
        if (CanCreatePopup())
            ShowPopup(new DeleteTag(this, tag));
    }

    /// <summary>リモート追加ダイアログを表示する。</summary>
    public void AddRemote()
    {
        if (CanCreatePopup())
            ShowPopup(new AddRemote(this));
    }

    /// <summary>リモート削除の確認ダイアログを表示する。</summary>
    public void DeleteRemote(Models.Remote remote)
    {
        if (CanCreatePopup())
            ShowPopup(new DeleteRemote(this, remote));
    }

    /// <summary>サブモジュール追加ダイアログを表示する。</summary>
    public void AddSubmodule()
    {
        if (CanCreatePopup())
            ShowPopup(new AddSubmodule(this));
    }

    /// <summary>サブモジュール更新ダイアログを表示する。</summary>
    public void UpdateSubmodules()
    {
        if (CanCreatePopup())
            ShowPopup(new UpdateSubmodules(this, null));
    }

    /// <summary>
    ///     更新可能なサブモジュールを自動更新する。
    ///     設定で確認が有効な場合は確認ダイアログを表示する。
    /// </summary>
    public async Task AutoUpdateSubmodulesAsync(Models.ICommandLog log)
    {
        var submodules = await new Commands.QueryUpdatableSubmodules(FullPath, false).GetResultAsync();
        if (submodules.Count == 0)
            return;

        do
        {
            if (_settings.AskBeforeAutoUpdatingSubmodules)
            {
                var builder = new StringBuilder();
                builder.Append("\n\n");
                foreach (var s in submodules)
                    builder.Append("- ").Append(s).Append('\n');
                builder.Append("\n");

                var msg = App.Text("Checkout.WarnUpdatingSubmodules", builder.ToString());
                var shouldContinue = await App.AskConfirmAsync(msg, Models.ConfirmButtonType.YesNo);
                if (!shouldContinue)
                    break;
            }

            await new Commands.Submodule(FullPath)
                .Use(log)
                .UpdateAsync(submodules);
        } while (false);
    }

    /// <summary>指定サブモジュールを新しいタブで開く。リポジトリノードが未登録の場合は自動作成する。</summary>
    public void OpenSubmodule(string submodule)
    {
        var selfPage = GetOwnerPage();
        if (selfPage is null)
            return;

        var root = Path.GetFullPath(Path.Combine(FullPath, submodule));
        var normalizedPath = root.Replace('\\', '/').TrimEnd('/');

        var node = Preferences.Instance.FindNode(normalizedPath) ??
            new RepositoryNode
            {
                Id = normalizedPath,
                Name = Path.GetFileName(normalizedPath),
                Bookmark = selfPage.Node.Bookmark,
                IsRepository = true,
            };

        App.GetLauncher().OpenRepositoryInTab(node, null);
    }

    /// <summary>ワークツリー追加ダイアログを表示する。</summary>
    public void AddWorktree()
    {
        if (CanCreatePopup())
            ShowPopup(new AddWorktree(this));
    }

    /// <summary>不要なワークツリーのプルーン（削除）を実行する。</summary>
    public async Task PruneWorktreesAsync()
    {
        if (CanCreatePopup())
            await ShowAndStartPopupAsync(new PruneWorktrees(this));
    }

    /// <summary>指定ワークツリーを新しいタブで開く。現在のワークツリーの場合は何もしない。</summary>
    public void OpenWorktree(Worktree worktree)
    {
        if (worktree.IsCurrent)
            return;

        var node = Preferences.Instance.FindNode(worktree.FullPath) ??
            new RepositoryNode
            {
                Id = worktree.FullPath,
                Name = Path.GetFileName(worktree.FullPath),
                Bookmark = 0,
                IsRepository = true,
            };

        App.GetLauncher().OpenRepositoryInTab(node, null);
    }

    /// <summary>指定ワークツリーをロックし、誤削除を防止する。</summary>
    public async Task LockWorktreeAsync(Worktree worktree)
    {
        using var lockWatcher = _watcher?.Lock();
        var log = CreateLog("Lock Worktree");
        var succ = await new Commands.Worktree(FullPath).Use(log).LockAsync(worktree.FullPath);
        if (succ)
            worktree.IsLocked = true;
        log.Complete();
    }

    /// <summary>指定ワークツリーのロックを解除する。</summary>
    public async Task UnlockWorktreeAsync(Worktree worktree)
    {
        using var lockWatcher = _watcher?.Lock();
        var log = CreateLog("Unlock Worktree");
        var succ = await new Commands.Worktree(FullPath).Use(log).UnlockAsync(worktree.FullPath);
        if (succ)
            worktree.IsLocked = false;
        log.Complete();
    }

    /// <summary>
    ///     優先OpenAIサービスのリストを取得する。
    ///     リポジトリ固有の設定があればそれを優先し、なければ全サービスを返す。
    /// </summary>
    public List<Models.OpenAIService> GetPreferredOpenAIServices()
    {
        var services = Preferences.Instance.OpenAIServices;
        if (services is null || services.Count == 0)
            return [];

        if (services.Count == 1)
            return [services[0]];

        var preferred = _settings.PreferredOpenAIService;
        var all = new List<Models.OpenAIService>();
        foreach (var service in services)
        {
            if (service.Name.Equals(preferred, StringComparison.Ordinal))
                return [service];

            all.Add(service);
        }

        return all;
    }

    /// <summary>全変更の破棄確認ダイアログを表示する。</summary>
    public void DiscardAllChanges()
    {
        if (CanCreatePopup())
            ShowPopup(new Discard(this));
    }

    /// <summary>全スタッシュのクリア確認ダイアログを表示する。</summary>
    public void ClearStashes()
    {
        if (CanCreatePopup())
            ShowPopup(new ClearStashes(this));
    }

    /// <summary>
    ///     指定コミットをパッチファイルとして保存する。
    ///     ファイル名はインデックスとコミットサブジェクトから安全な文字列を生成する。
    /// </summary>
    public async Task<bool> SaveCommitAsPatchAsync(Models.Commit commit, string folder, int index = 0)
    {
        var ignoredChars = new HashSet<char> { '/', '\\', ':', ',', '*', '?', '\"', '<', '>', '|', '`', '$', '^', '%', '[', ']', '+', '-' };
        var builder = new StringBuilder();
        builder.Append(index.ToString("D4"));
        builder.Append('-');

        var chars = commit.Subject.ToCharArray();
        var len = 0;
        foreach (var c in chars)
        {
            if (!ignoredChars.Contains(c))
            {
                if (c == ' ' || c == '\t')
                    builder.Append('-');
                else
                    builder.Append(c);

                len++;

                if (len >= 48)
                    break;
            }
        }
        builder.Append(".patch");

        var saveTo = Path.Combine(folder, builder.ToString());
        var log = CreateLog("Save Commit as Patch");
        var succ = await new Commands.FormatPatch(FullPath, commit.SHA, saveTo).Use(log).ExecAsync();
        log.Complete();
        return succ;
    }

    /// <summary>このリポジトリが表示されているランチャーページを取得する。見つからない場合はnull。</summary>
    private LauncherPage GetOwnerPage()
    {
        var launcher = App.GetLauncher();
        if (launcher is null)
            return null;

        foreach (var page in launcher.Pages)
        {
            if (page.Node.Id.Equals(FullPath))
                return page;
        }

        return null;
    }

    /// <summary>
    ///     ブランチとリモートからブランチツリーを構築する。
    ///     フィルタが設定されている場合はフィルタに一致するブランチのみを含める。
    ///     構築後にフィルタモードを適用する。
    /// </summary>
    private BranchTreeNode.Builder BuildBranchTree(List<Models.Branch> branches, List<Models.Remote> remotes, bool forceExpanded = false)
    {
        var builder = new BranchTreeNode.Builder(_uiStates.LocalBranchSortMode, _uiStates.RemoteBranchSortMode);
        if (string.IsNullOrEmpty(_filter))
        {
            if (!forceExpanded)
                builder.SetExpandedNodes(_uiStates.ExpandedBranchNodesInSideBar);
            builder.Run(branches, remotes, forceExpanded);

            // 再構築後のツリーから実際に展開されているパスを収集して保存する。
            // IsCurrentによる自動展開も含め、存在しないパスは自然に除外される。
            var expanded = new List<string>();
            BranchTreeNode.Builder.CollectExpandedPaths(builder.Locals, expanded);
            BranchTreeNode.Builder.CollectExpandedPaths(builder.Remotes, expanded);
            _uiStates.ExpandedBranchNodesInSideBar = expanded;
        }
        else
        {
            var visibles = new List<Models.Branch>();
            foreach (var b in branches)
            {
                if (b.FullName.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    visibles.Add(b);
            }

            builder.Run(visibles, remotes, true);
        }

        var filterMap = _uiStates.GetHistoryFiltersMap();
        UpdateBranchTreeFilterMode(builder.Locals, filterMap);
        UpdateBranchTreeFilterMode(builder.Remotes, filterMap);
        return builder;
    }

    /// <summary>
    ///     表示用タグコレクションを構築する。
    ///     ソートモードに応じてソートし、フィルタを適用後、ツリーまたはリスト形式で返す。
    /// </summary>
    private object BuildVisibleTags()
    {
        switch (_uiStates.TagSortMode)
        {
            case Models.TagSortMode.CreatorDate:
                _tags.Sort((l, r) => r.CreatorDate.CompareTo(l.CreatorDate));
                break;
            default:
                _tags.Sort((l, r) => Models.NumericSort.Compare(l.Name, r.Name));
                break;
        }

        var visible = new List<Models.Tag>();
        if (string.IsNullOrEmpty(_filter))
        {
            visible.AddRange(_tags);
        }
        else
        {
            foreach (var t in _tags)
            {
                if (t.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    visible.Add(t);
            }
        }

        var filterMap = _uiStates.GetHistoryFiltersMap();
        UpdateTagFilterMode(filterMap);

        if (_uiStates.ShowTagsAsTree)
        {
            var tree = TagCollectionAsTree.Build(visible, _visibleTags as TagCollectionAsTree);
            foreach (var node in tree.Tree)
                node.UpdateFilterMode(filterMap);
            return tree;
        }
        else
        {
            var list = new TagCollectionAsList(visible);
            foreach (var item in list.TagItems)
                item.FilterMode = filterMap.GetValueOrDefault(item.Tag.Name, Models.FilterMode.None);
            return list;
        }
    }

    /// <summary>
    ///     表示用サブモジュールコレクションを構築する。
    ///     フィルタを適用後、ツリーまたはリスト形式で返す。
    /// </summary>
    private object BuildVisibleSubmodules()
    {
        var visible = new List<Models.Submodule>();
        if (string.IsNullOrEmpty(_filter))
        {
            visible.AddRange(_submodules);
        }
        else
        {
            foreach (var s in _submodules)
            {
                if (s.Path.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    visible.Add(s);
            }
        }

        if (_uiStates.ShowSubmodulesAsTree)
            return SubmoduleCollectionAsTree.Build(visible, _visibleSubmodules as SubmoduleCollectionAsTree);
        else
            return new SubmoduleCollectionAsList() { Submodules = visible };
    }

    /// <summary>履歴フィルタモードを更新し、refreshがtrueの場合はブランチツリー・タグ・コミットを再描画する。</summary>
    private void RefreshHistoryFilters(bool refresh)
    {
        HistoryFilterMode = _uiStates.GetHistoryFilterMode();
        if (!refresh)
            return;

        var map = _uiStates.GetHistoryFiltersMap();
        UpdateBranchTreeFilterMode(LocalBranchTrees, map);
        UpdateBranchTreeFilterMode(RemoteBranchTrees, map);
        UpdateTagFilterMode(map);
        RefreshCommits();
    }

    /// <summary>ブランチツリーの各ノードにフィルタモードを再帰的に適用する。</summary>
    private void UpdateBranchTreeFilterMode(List<BranchTreeNode> nodes, Dictionary<string, Models.FilterMode> map)
    {
        foreach (var node in nodes)
        {
            var mode = map.GetValueOrDefault(node.Path, Models.FilterMode.None);
            if (node.FilterMode != mode)
                node.FilterMode = mode;

            if (!node.IsBranch)
                UpdateBranchTreeFilterMode(node.Children, map);
        }
    }

    /// <summary>タグのフィルタモードを更新する（ツリー形式またはリスト形式に対応）。</summary>
    private void UpdateTagFilterMode(Dictionary<string, Models.FilterMode> map)
    {
        if (VisibleTags is TagCollectionAsTree tree)
        {
            foreach (var node in tree.Tree)
                node.UpdateFilterMode(map);
        }
        else if (VisibleTags is TagCollectionAsList list)
        {
            foreach (var item in list.TagItems)
                item.FilterMode = map.GetValueOrDefault(item.Tag.Name, Models.FilterMode.None);
        }
    }

    /// <summary>ブランチツリーの全ノードのフィルタモードをNoneに再帰的にリセットする。</summary>
    private void ResetBranchTreeFilterMode(List<BranchTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.FilterMode = Models.FilterMode.None;
            if (!node.IsBranch)
                ResetBranchTreeFilterMode(node.Children);
        }
    }

    /// <summary>全タグのフィルタモードをNoneにリセットする（ツリー形式またはリスト形式に対応）。</summary>
    private void ResetTagFilterMode()
    {
        if (VisibleTags is TagCollectionAsTree tree)
        {
            var filters = new Dictionary<string, Models.FilterMode>();
            foreach (var node in tree.Tree)
                node.UpdateFilterMode(filters);
        }
        else if (VisibleTags is TagCollectionAsList list)
        {
            foreach (var item in list.TagItems)
                item.FilterMode = Models.FilterMode.None;
        }
    }

    /// <summary>指定パスに一致するブランチツリーノードを再帰的に検索する。見つからない場合はnull。</summary>
    private BranchTreeNode FindBranchNode(List<BranchTreeNode> nodes, string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        foreach (var node in nodes)
        {
            if (node.Path.Equals(path, StringComparison.Ordinal))
                return node;

            if (path.StartsWith(node.Path, StringComparison.Ordinal))
            {
                var founded = FindBranchNode(node.Children, path);
                if (founded is not null)
                    return founded;
            }
        }

        return null;
    }

    /// <summary>タイマーコールバック。UIスレッドで自動フェッチを実行する。</summary>
    private void AutoFetchByTimer(object sender)
    {
        try
        {
            Dispatcher.UIThread.Invoke(AutoFetchOnUIThread);
        }
        catch
        {
            // Ignore exception.
        }
    }

    /// <summary>
    ///     UIスレッド上で自動フェッチを実行する。
    ///     設定の有効/無効、ロックファイルの存在、前回フェッチからの経過時間をチェックし、
    ///     条件を満たした場合に全リモートまたはデフォルトリモートからフェッチする。
    /// </summary>
    private async Task AutoFetchOnUIThread()
    {
        if (_uiStates is null)
            return;

        CommandLog log = null;

        try
        {
            if (_settings is not { EnableAutoFetch: true } || !CanCreatePopup())
            {
                _lastFetchTime = DateTime.Now;
                return;
            }

            var lockFile = Path.Combine(GitDir, "index.lock");
            if (File.Exists(lockFile))
                return;

            var now = DateTime.Now;
            var desire = _lastFetchTime.AddMinutes(_settings.AutoFetchInterval);
            if (desire > now)
                return;

            var remotes = new List<string>();
            foreach (var r in _remotes)
                remotes.Add(r.Name);

            if (remotes.Count == 0)
                return;

            IsAutoFetching = true;
            log = CreateLog("Auto-Fetch");

            if (_uiStates.FetchAllRemotes)
            {
                foreach (var remote in remotes)
                    await new Commands.Fetch(FullPath, remote).Use(log).RunAsync();
            }
            else
            {
                var remote = !string.IsNullOrEmpty(_settings.DefaultRemote) ?
                    remotes.Find(x => x.Equals(_settings.DefaultRemote, StringComparison.Ordinal)) :
                    remotes[0];

                await new Commands.Fetch(FullPath, remote).Use(log).RunAsync();
            }

            _lastFetchTime = DateTime.Now;
            IsAutoFetching = false;
        }
        catch
        {
            // Ignore all exceptions.
        }

        log?.Complete();
    }

    private readonly string _gitCommonDir = null;                                    // 共有gitディレクトリパス（worktree用）
    private Models.RepositorySettings _settings = null;                                // リポジトリ固有設定
    private Models.RepositoryUIStates _uiStates = null;                                // UI状態（フィルタ、展開状態等）
    private Models.FilterMode _historyFilterMode = Models.FilterMode.None;             // 履歴フィルタモード
    private bool _hasAllowedSignersFile = false;                                       // GPG許可署名者ファイルの存在フラグ
    private ulong _queryLocalChangesTimes = 0;                                         // ローカル変更クエリの実行回数

    private Models.Watcher _watcher = null;                                            // ファイルシステム監視
    private Histories _histories = null;                                               // 履歴ビューVM
    private WorkingCopy _workingCopy = null;                                           // ワーキングコピーVM
    private StashesPage _stashesPage = null;                                           // スタッシュページVM
    private int _selectedViewIndex = 0;                                                // 選択中のビューインデックス
    private object _selectedView = null;                                               // 選択中のビューオブジェクト

    private int _localBranchesCount = 0;                                               // ローカルブランチ数
    private int _localChangesCount = 0;                                                // ローカル変更数
    private int _stashesCount = 0;                                                     // スタッシュ数

    private bool _isSearchingCommits = false;                                          // コミット検索中フラグ
    private SearchCommitContext _searchCommitContext = null;                            // コミット検索コンテキスト

    private string _filter = string.Empty;                                             // サイドバーフィルタ文字列
    private List<Models.Remote> _remotes = [];                                         // リモート一覧
    private List<Models.Branch> _branches = [];                                        // ブランチ一覧
    private Models.Branch _currentBranch = null;                                       // 現在のブランチ
    private List<BranchTreeNode> _localBranchTrees = [];                               // ローカルブランチツリー
    private List<BranchTreeNode> _remoteBranchTrees = [];                              // リモートブランチツリー
    private List<Worktree> _worktrees = [];                                            // ワークツリー一覧
    private List<Models.Tag> _tags = [];                                               // タグ一覧
    private object _visibleTags = null;                                                // 表示用タグコレクション
    private List<Models.Submodule> _submodules = [];                                   // サブモジュール一覧
    private object _visibleSubmodules = null;                                          // 表示用サブモジュールコレクション
    private string _navigateToCommitDelayed = string.Empty;                            // 遅延ナビゲーション先コミットSHA

    private bool _isAutoFetching = false;                                              // 自動フェッチ実行中フラグ
    private Timer _autoFetchTimer = null;                                              // 自動フェッチタイマー
    private DateTime _lastFetchTime = DateTime.MinValue;                               // 最終フェッチ時刻

    private Models.BisectState _bisectState = Models.BisectState.None;                 // Bisect状態
    private bool _isBisectCommandRunning = false;                                      // Bisectコマンド実行中フラグ

    private System.Threading.Timer _filterDebounceTimer = null;                          // フィルタデバウンスタイマー
    private CancellationTokenSource _cancellationRefreshBranches = null;               // ブランチ更新キャンセルトークン
    private CancellationTokenSource _cancellationRefreshTags = null;                   // タグ更新キャンセルトークン
    private CancellationTokenSource _cancellationRefreshWorkingCopyChanges = null;     // ワーキングコピー更新キャンセルトークン
    private CancellationTokenSource _cancellationRefreshCommits = null;                // コミット更新キャンセルトークン
    private CancellationTokenSource _cancellationRefreshStashes = null;                // スタッシュ更新キャンセルトークン
}
