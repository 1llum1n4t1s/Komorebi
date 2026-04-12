using System;
using System.Collections.Generic;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Komorebi.Views;

/// <summary>
/// リポジトリメインビュー（ブランチ・履歴・ワーキングコピー）のコードビハインド。
/// </summary>
public partial class Repository : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public Repository()
    {
        InitializeComponent();
    }

    /// <summary>
    /// コントロールが読み込まれた際の処理。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UpdateLeftSidebarLayout();
        UpdateWorkspaceDisplay();
        if (DataContext is ViewModels.Repository repo)
        {
            repo.PropertyChanged += OnRepoPropertyChanged;

            // ListBox(SelectionMode=AlwaysSelected)の初期化がSelectedViewIndexを
            // 0にリセットするため、Dispatcherで遅延適用してListBoxのレイアウト完了後に設定する
            // ベアリポジトリではワーキングコピータブが非表示のためスキップ
            if (!repo.IsBare && ViewModels.Preferences.Instance.ShowLocalChangesByDefault)
            {
                // ListBoxリセット(0)のみ上書きする。
                // ユーザーが既にスタッシュ(2)等に切り替えていた場合はスキップ。
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (repo.SelectedViewIndex == 0)
                        repo.SelectedViewIndex = 1;
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (DataContext is ViewModels.Repository repo)
            repo.PropertyChanged -= OnRepoPropertyChanged;
    }

    private void OnRepoPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.Repository.IsSearchingCommits) &&
            DataContext is ViewModels.Repository { IsSearchingCommits: true })
            Avalonia.Threading.Dispatcher.UIThread.Post(() => TxtSearchCommitsBox.Focus(NavigationMethod.Pointer));
    }

    /// <summary>
    /// ToggleFilterイベントのハンドラ。
    /// </summary>
    private void OnToggleFilter(object _, RoutedEventArgs e)
    {
        FilterBox.Focus();
        e.Handled = true;
    }

    /// <summary>
    /// SearchBoxGotFocusイベントのハンドラ。フォーカス取得のみ（検索モードはEnterキーで有効化）。
    /// </summary>
    private void OnSearchBoxGotFocus(object _, FocusChangedEventArgs e)
    {
        // 検索モード（IsSearchingCommits）はEnterキー押下時に有効化する。
        // ここでは何もしない（サジェストはFilter変更で自動起動する）。
    }

    /// <summary>
    /// SearchKeyDownイベントのハンドラ（コンテンツツールバーの検索ボックス用）。
    /// </summary>
    private void OnSearchKeyDown(object _, KeyEventArgs e)
    {
        if (DataContext is not ViewModels.Repository repo)
            return;

        if (e.Key == Key.Enter)
        {
            repo.IsSearchingCommits = true;
            repo.SearchCommitContext.StartSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (repo.SearchCommitContext.Suggestions is { Count: > 0 })
            {
                SearchSuggestionBox.Focus(NavigationMethod.Tab);
                SearchSuggestionBox.SelectedIndex = 0;
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (repo.IsSearchingCommits)
                repo.IsSearchingCommits = false;
            else
                repo.SearchCommitContext.ClearSuggestions();
            e.Handled = true;
        }
    }

    /// <summary>
    /// ClearSearchCommitFilterイベントのハンドラ。
    /// </summary>
    private void OnClearSearchCommitFilter(object _, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.Repository repo)
            return;

        repo.SearchCommitContext.ClearFilter();
        repo.IsSearchingCommits = false;
        e.Handled = true;
    }

    /// <summary>
    /// ローカルの変更フィルタークリアボタンのハンドラ。
    /// </summary>
    private void OnClearWorkingCopyFilter(object _, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
            repo.WorkingCopyVM?.ClearFilter();
        e.Handled = true;
    }

    /// <summary>
    /// スタッシュ検索フィルタークリアボタンのハンドラ。
    /// </summary>
    private void OnClearStashesFilter(object _, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
            repo.StashesPageVM?.ClearSearchFilter();
        e.Handled = true;
    }

    /// <summary>
    /// SearchSuggestionBoxKeyDownイベントのハンドラ。
    /// </summary>
    private void OnSearchSuggestionBoxKeyDown(object _, KeyEventArgs e)
    {
        if (DataContext is not ViewModels.Repository repo)
            return;

        if (e.Key == Key.Escape)
        {
            repo.SearchCommitContext.ClearSuggestions();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && SearchSuggestionBox.SelectedItem is string content)
        {
            repo.SearchCommitContext.Filter = content;
            TxtSearchCommitsBox.CaretIndex = content.Length;
            repo.IsSearchingCommits = true;
            repo.SearchCommitContext.StartSearch();
            e.Handled = true;
        }
    }

    /// <summary>
    /// SearchSuggestionDoubleTappedイベントのハンドラ。
    /// </summary>
    private void OnSearchSuggestionDoubleTapped(object sender, TappedEventArgs e)
    {
        if (DataContext is not ViewModels.Repository repo)
            return;

        var content = (sender as StackPanel)?.DataContext as string;
        if (!string.IsNullOrEmpty(content))
        {
            repo.SearchCommitContext.Filter = content;
            TxtSearchCommitsBox.CaretIndex = content.Length;
            repo.IsSearchingCommits = true;
            repo.SearchCommitContext.StartSearch();
        }

        e.Handled = true;
    }

    /// <summary>
    /// WorkingCopySegmentContextRequestedイベントのハンドラ（「全変更を破棄」メニュー）。
    /// </summary>
    private void OnWorkingCopySegmentContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
        {
            var discardAll = new MenuItem();
            discardAll.Header = App.Text("Repository.DiscardAll");
            discardAll.Icon = App.CreateMenuIcon("Icons.Discard");
            discardAll.Click += (_, ev) =>
            {
                repo.DiscardAllChanges();
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(discardAll);
            if (sender is Control ctrl)
                menu.Open(ctrl);
        }

        e.Handled = true;
    }

    /// <summary>
    /// StashesSegmentContextRequestedイベントのハンドラ（「全スタッシュをクリア」メニュー）。
    /// </summary>
    private void OnStashesSegmentContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
        {
            var clearAll = new MenuItem();
            clearAll.Header = App.Text("Repository.ClearStashes");
            clearAll.Icon = App.CreateMenuIcon("Icons.Clear");
            clearAll.Click += (_, ev) =>
            {
                repo.ClearStashes();
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(clearAll);
            if (sender is Control ctrl)
                menu.Open(ctrl);
        }

        e.Handled = true;
    }

    /// <summary>
    /// LocalBranchTreeSelectionChangedイベントのハンドラ。
    /// </summary>
    private void OnLocalBranchTreeSelectionChanged(object _1, RoutedEventArgs _2)
    {
        RemoteBranchTree.UnselectAll();
        TagsList.UnselectAll();
    }

    /// <summary>
    /// RemoteBranchTreeSelectionChangedイベントのハンドラ。
    /// </summary>
    private void OnRemoteBranchTreeSelectionChanged(object _1, RoutedEventArgs _2)
    {
        LocalBranchTree.UnselectAll();
        TagsList.UnselectAll();
    }

    /// <summary>
    /// TagsSelectionChangedイベントのハンドラ。
    /// </summary>
    private void OnTagsSelectionChanged(object _1, RoutedEventArgs _2)
    {
        LocalBranchTree.UnselectAll();
        RemoteBranchTree.UnselectAll();
    }

    /// <summary>
    /// WorktreeContextRequestedイベントのハンドラ。
    /// </summary>
    private void OnWorktreeContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (sender is Control { DataContext: ViewModels.Worktree worktree } ctrl && DataContext is ViewModels.Repository repo)
        {
            var menu = new ContextMenu();

            var switchTo = new MenuItem();
            switchTo.Header = App.Text("Worktree.Open");
            switchTo.Icon = App.CreateMenuIcon("Icons.Folder.Open");
            switchTo.Click += (_, ev) =>
            {
                repo.OpenWorktree(worktree);
                ev.Handled = true;
            };
            menu.Items.Add(switchTo);
            menu.Items.Add(new MenuItem() { Header = "-" });

            if (worktree.IsLocked)
            {
                var unlock = new MenuItem();
                unlock.Header = App.Text("Worktree.Unlock");
                unlock.Icon = App.CreateMenuIcon("Icons.Unlock");
                unlock.Click += async (_, ev) =>
                {
                    await repo.UnlockWorktreeAsync(worktree);
                    ev.Handled = true;
                };
                menu.Items.Add(unlock);
            }
            else
            {
                var loc = new MenuItem();
                loc.Header = App.Text("Worktree.Lock");
                loc.Icon = App.CreateMenuIcon("Icons.Lock");
                loc.IsEnabled = !worktree.IsMain;
                loc.Click += async (_, ev) =>
                {
                    await repo.LockWorktreeAsync(worktree);
                    ev.Handled = true;
                };
                menu.Items.Add(loc);
            }

            var remove = new MenuItem();
            remove.Header = App.Text("Worktree.Remove");
            remove.Icon = App.CreateMenuIcon("Icons.Clear");
            remove.IsEnabled = !worktree.IsCurrent && !worktree.IsMain;
            remove.Click += (_, ev) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.RemoveWorktree(repo, worktree));
                ev.Handled = true;
            };
            menu.Items.Add(remove);

            var copy = new MenuItem();
            copy.Header = App.Text("Worktree.CopyPath");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(worktree.FullPath);
                ev.Handled = true;
            };
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(copy);
            menu.Open(ctrl);
        }

        e.Handled = true;
    }

    /// <summary>
    /// WorktreeDoubleTappedイベントのハンドラ。
    /// </summary>
    private void OnWorktreeDoubleTapped(object sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: ViewModels.Worktree worktree } && DataContext is ViewModels.Repository repo)
            repo.OpenWorktree(worktree);

        e.Handled = true;
    }

    /// <summary>
    /// WorktreeListPropertyChangedイベントのハンドラ。
    /// </summary>
    private void OnWorktreeListPropertyChanged(object _, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ItemsControl.ItemsSourceProperty || e.Property == IsVisibleProperty)
            UpdateLeftSidebarLayout();
    }

    /// <summary>
    /// ブランチツリーの全フォルダノードを展開するボタンのクリックハンドラ。
    /// </summary>
    private void OnExpandAllBranchNodes(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
            repo.ExpandAllBranchNodes();
    }

    /// <summary>
    /// ブランチツリーの全フォルダノードを折りたたむボタンのクリックハンドラ。
    /// </summary>
    private void OnCollapseAllBranchNodes(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
            repo.CollapseAllBranchNodes();
    }

    /// <summary>
    /// LeftSidebarRowsChangedイベントのハンドラ。
    /// </summary>
    private void OnLeftSidebarRowsChanged(object _, RoutedEventArgs e)
    {
        UpdateLeftSidebarLayout();
        e.Handled = true;
    }

    /// <summary>
    /// LeftSidebarSizeChangedイベントのハンドラ。
    /// </summary>
    private void OnLeftSidebarSizeChanged(object _, SizeChangedEventArgs e)
    {
        if (e.HeightChanged)
            UpdateLeftSidebarLayout();
    }

    /// <summary>
    /// UpdateLeftSidebarLayoutの処理を行う。
    /// </summary>
    private void UpdateLeftSidebarLayout()
    {
        var vm = DataContext as ViewModels.Repository;
        if (vm?.Settings is null)
            return;

        if (!IsLoaded)
            return;

        var leftHeight = LeftSidebarGroups.Bounds.Height - 28.0 * 5 - 4;
        if (leftHeight <= 0)
            return;

        var localBranchRows = vm.IsLocalBranchGroupExpanded ? LocalBranchTree.Rows.Count : 0;
        var remoteBranchRows = vm.IsRemoteGroupExpanded ? RemoteBranchTree.Rows.Count : 0;
        var desiredBranches = (localBranchRows + remoteBranchRows) * 24.0;
        var desiredTag = vm.IsTagGroupExpanded ? 24.0 * TagsList.Rows : 0;
        var desiredSubmodule = vm.IsSubmoduleGroupExpanded ? 24.0 * SubmoduleList.Rows : 0;
        var desiredWorktree = vm.IsWorktreeGroupExpanded ? 24.0 * vm.Worktrees.Count : 0;
        var desiredOthers = desiredTag + desiredSubmodule + desiredWorktree;
        var hasOverflow = (desiredBranches + desiredOthers > leftHeight);

        if (vm.IsWorktreeGroupExpanded)
        {
            var height = desiredWorktree;
            if (hasOverflow)
            {
                var test = leftHeight - desiredBranches - desiredTag - desiredSubmodule;
                if (test < 0)
                    height = Math.Min(120, height);
                else
                    height = Math.Max(120, test);
            }

            leftHeight -= height;
            WorktreeList.Height = height;
            hasOverflow = (desiredBranches + desiredTag + desiredSubmodule) > leftHeight;
        }

        if (vm.IsSubmoduleGroupExpanded)
        {
            var height = desiredSubmodule;
            if (hasOverflow)
            {
                var test = leftHeight - desiredBranches - desiredTag;
                if (test < 0)
                    height = Math.Min(120, height);
                else
                    height = Math.Max(120, test);
            }

            leftHeight -= height;
            SubmoduleList.Height = height;
            hasOverflow = (desiredBranches + desiredTag) > leftHeight;
        }

        if (vm.IsTagGroupExpanded)
        {
            var height = desiredTag;
            if (hasOverflow)
            {
                var test = leftHeight - desiredBranches;
                if (test < 0)
                    height = Math.Min(120, height);
                else
                    height = Math.Max(120, test);
            }

            leftHeight -= height;
            TagsList.Height = height;
        }

        if (leftHeight > 0 && desiredBranches > leftHeight)
        {
            var local = localBranchRows * 24.0;
            var remote = remoteBranchRows * 24.0;
            var half = leftHeight / 2;
            if (vm.IsLocalBranchGroupExpanded)
            {
                if (vm.IsRemoteGroupExpanded)
                {
                    if (local < half)
                    {
                        LocalBranchTree.Height = local;
                        RemoteBranchTree.Height = leftHeight - local;
                    }
                    else if (remote < half)
                    {
                        RemoteBranchTree.Height = remote;
                        LocalBranchTree.Height = leftHeight - remote;
                    }
                    else
                    {
                        LocalBranchTree.Height = half;
                        RemoteBranchTree.Height = half;
                    }
                }
                else
                {
                    LocalBranchTree.Height = leftHeight;
                }
            }
            else if (vm.IsRemoteGroupExpanded)
            {
                RemoteBranchTree.Height = leftHeight;
            }
        }
        else
        {
            if (vm.IsLocalBranchGroupExpanded)
            {
                var height = localBranchRows * 24;
                LocalBranchTree.Height = height;
            }

            if (vm.IsRemoteGroupExpanded)
            {
                var height = remoteBranchRows * 24;
                RemoteBranchTree.Height = height;
            }
        }
    }


    /// <summary>
    /// OpenAdvancedHistoriesOptionイベントのハンドラ。
    /// </summary>
    private void OnOpenAdvancedHistoriesOption(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && DataContext is ViewModels.Repository repo)
        {
            var pref = ViewModels.Preferences.Instance;

            var layout = new MenuItem();
            layout.Header = App.Text("Repository.HistoriesLayout");
            layout.IsEnabled = false;

            var isHorizontal = pref.UseTwoColumnsLayoutInHistories;
            var horizontal = new MenuItem();
            horizontal.Header = App.Text("Repository.HistoriesLayout.Horizontal");
            if (isHorizontal)
                horizontal.Icon = App.CreateMenuIcon("Icons.Check");
            horizontal.Click += (_, ev) =>
            {
                pref.UseTwoColumnsLayoutInHistories = true;
                ev.Handled = true;
            };

            var vertical = new MenuItem();
            vertical.Header = App.Text("Repository.HistoriesLayout.Vertical");
            if (!isHorizontal)
                vertical.Icon = App.CreateMenuIcon("Icons.Check");
            vertical.Click += (_, ev) =>
            {
                pref.UseTwoColumnsLayoutInHistories = false;
                ev.Handled = true;
            };

            var showFlags = new MenuItem();
            showFlags.Header = App.Text("Repository.ShowFlags");
            showFlags.IsEnabled = false;

            var reflog = new MenuItem();
            reflog.Header = App.Text("Repository.ShowLostCommits");
            reflog.Tag = "--reflog";
            if (repo.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.Reflog))
                reflog.Icon = App.CreateMenuIcon("Icons.Check");
            reflog.Click += (_, ev) =>
            {
                repo.ToggleHistoryShowFlag(Models.HistoryShowFlags.Reflog);
                ev.Handled = true;
            };

            var firstParentOnly = new MenuItem();
            firstParentOnly.Header = App.Text("Repository.ShowFirstParentOnly");
            firstParentOnly.Tag = "--first-parent";
            if (repo.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.FirstParentOnly))
                firstParentOnly.Icon = App.CreateMenuIcon("Icons.Check");
            firstParentOnly.Click += (_, ev) =>
            {
                repo.ToggleHistoryShowFlag(Models.HistoryShowFlags.FirstParentOnly);
                ev.Handled = true;
            };

            var simplifyByDecoration = new MenuItem();
            simplifyByDecoration.Header = App.Text("Repository.ShowDecoratedCommitsOnly");
            simplifyByDecoration.Tag = "--simplify-by-decoration";
            if (repo.HistoryShowFlags.HasFlag(Models.HistoryShowFlags.SimplifyByDecoration))
                simplifyByDecoration.Icon = App.CreateMenuIcon("Icons.Check");
            simplifyByDecoration.Click += (_, ev) =>
            {
                repo.ToggleHistoryShowFlag(Models.HistoryShowFlags.SimplifyByDecoration);
                ev.Handled = true;
            };

            var order = new MenuItem();
            order.Header = App.Text("Repository.HistoriesOrder");
            order.IsEnabled = false;

            var dateOrder = new MenuItem();
            dateOrder.Header = App.Text("Repository.HistoriesOrder.ByDate");
            dateOrder.Tag = "--date-order";
            if (!repo.EnableTopoOrderInHistory)
                dateOrder.Icon = App.CreateMenuIcon("Icons.Check");
            dateOrder.Click += (_, ev) =>
            {
                repo.EnableTopoOrderInHistory = false;
                ev.Handled = true;
            };

            var topoOrder = new MenuItem();
            topoOrder.Header = App.Text("Repository.HistoriesOrder.Topo");
            topoOrder.Tag = "--topo-order";
            if (repo.EnableTopoOrderInHistory)
                topoOrder.Icon = App.CreateMenuIcon("Icons.Check");
            topoOrder.Click += (_, ev) =>
            {
                repo.EnableTopoOrderInHistory = true;
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
            menu.Items.Add(layout);
            menu.Items.Add(horizontal);
            menu.Items.Add(vertical);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(showFlags);
            menu.Items.Add(reflog);
            menu.Items.Add(firstParentOnly);
            menu.Items.Add(simplifyByDecoration);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(order);
            menu.Items.Add(dateOrder);
            menu.Items.Add(topoOrder);
            menu.Open(button);
        }

        e.Handled = true;
    }

    /// <summary>
    /// OpenSortLocalBranchMenuイベントのハンドラ。
    /// </summary>
    private void OnOpenSortLocalBranchMenu(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && DataContext is ViewModels.Repository repo)
        {
            var isSortByName = repo.IsSortingLocalBranchByName;
            var byNameAsc = new MenuItem();
            byNameAsc.Header = App.Text("Repository.BranchSort.ByName");
            if (isSortByName)
                byNameAsc.Icon = App.CreateMenuIcon("Icons.Check");
            byNameAsc.Click += (_, ev) =>
            {
                if (!isSortByName)
                    repo.IsSortingLocalBranchByName = true;
                ev.Handled = true;
            };

            var byCommitterDate = new MenuItem();
            byCommitterDate.Header = App.Text("Repository.BranchSort.ByCommitterDate");
            if (!isSortByName)
                byCommitterDate.Icon = App.CreateMenuIcon("Icons.Check");
            byCommitterDate.Click += (_, ev) =>
            {
                if (isSortByName)
                    repo.IsSortingLocalBranchByName = false;
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
            menu.Items.Add(byNameAsc);
            menu.Items.Add(byCommitterDate);
            menu.Open(button);
        }

        e.Handled = true;
    }

    /// <summary>
    /// OpenSortRemoteBranchMenuイベントのハンドラ。
    /// </summary>
    private void OnOpenSortRemoteBranchMenu(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && DataContext is ViewModels.Repository repo)
        {
            var isSortByName = repo.IsSortingRemoteBranchByName;
            var byNameAsc = new MenuItem();
            byNameAsc.Header = App.Text("Repository.BranchSort.ByName");
            if (isSortByName)
                byNameAsc.Icon = App.CreateMenuIcon("Icons.Check");
            byNameAsc.Click += (_, ev) =>
            {
                if (!isSortByName)
                    repo.IsSortingRemoteBranchByName = true;
                ev.Handled = true;
            };

            var byCommitterDate = new MenuItem();
            byCommitterDate.Header = App.Text("Repository.BranchSort.ByCommitterDate");
            if (!isSortByName)
                byCommitterDate.Icon = App.CreateMenuIcon("Icons.Check");
            byCommitterDate.Click += (_, ev) =>
            {
                if (isSortByName)
                    repo.IsSortingRemoteBranchByName = false;
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
            menu.Items.Add(byNameAsc);
            menu.Items.Add(byCommitterDate);
            menu.Open(button);
        }

        e.Handled = true;
    }

    /// <summary>
    /// OpenSortTagMenuイベントのハンドラ。
    /// </summary>
    private void OnOpenSortTagMenu(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && DataContext is ViewModels.Repository repo)
        {
            var isSortByName = repo.IsSortingTagsByName;
            var byCreatorDate = new MenuItem();
            byCreatorDate.Header = App.Text("Repository.Tags.OrderByCreatorDate");
            if (!isSortByName)
                byCreatorDate.Icon = App.CreateMenuIcon("Icons.Check");
            byCreatorDate.Click += (_, ev) =>
            {
                if (isSortByName)
                    repo.IsSortingTagsByName = false;
                ev.Handled = true;
            };

            var byName = new MenuItem();
            byName.Header = App.Text("Repository.Tags.OrderByName");
            if (isSortByName)
                byName.Icon = App.CreateMenuIcon("Icons.Check");
            byName.Click += (_, ev) =>
            {
                if (!isSortByName)
                    repo.IsSortingTagsByName = true;
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
            menu.Items.Add(byName);
            menu.Items.Add(byCreatorDate);
            menu.Open(button);
        }

        e.Handled = true;
    }

    /// <summary>
    /// PruneWorktreesイベントのハンドラ。
    /// </summary>
    private async void OnPruneWorktrees(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
            await repo.PruneWorktreesAsync();

        e.Handled = true;
    }

    /// <summary>
    /// SkipInProgressイベントのハンドラ。
    /// </summary>
    private async void OnSkipInProgress(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
            await repo.SkipMergeAsync();

        e.Handled = true;
    }

    /// <summary>
    /// ResolveInProgressイベントのハンドラ。
    /// </summary>
    private void OnResolveInProgress(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
            repo.SelectedViewIndex = 1;

        e.Handled = true;
    }

    /// <summary>
    /// AbortInProgressイベントのハンドラ。
    /// </summary>
    private async void OnAbortInProgress(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
            await repo.AbortMergeAsync();

        e.Handled = true;
    }

    /// <summary>
    /// RemoveSelectedHistoryFilterイベントのハンドラ。
    /// </summary>
    private void OnRemoveSelectedHistoryFilter(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo && sender is Button { DataContext: Models.HistoryFilter filter })
            repo.RemoveHistoryFilter(filter);

        e.Handled = true;
    }

    /// <summary>
    /// BisectCommandイベントのハンドラ。
    /// </summary>
    private async void OnBisectCommand(object sender, RoutedEventArgs e)
    {
        if (sender is Button button &&
            DataContext is ViewModels.Repository { IsBisectCommandRunning: false } repo &&
            repo.CanCreatePopup())
            await repo.ExecBisectCommandAsync(button.Tag as string);

        e.Handled = true;
    }

    // ========== 以下、旧 RepositoryToolbar.axaml.cs から移植 ==========

    /// <summary>
    /// ワーキングコピー検索ボックスにフォーカスを移す（Ctrl+F 用）。
    /// </summary>
    public void FocusWorkingCopySearchBox()
    {
        TxtSearchWorkingCopyBox?.Focus();
    }

    /// <summary>
    /// ワークスペースセレクターの表示を更新する。
    /// </summary>
    private void UpdateWorkspaceDisplay()
    {
        if (App.GetLauncher() is { } launcher)
        {
            var ws = launcher.ActiveWorkspace;
            if (WorkspaceDot is not null)
                WorkspaceDot.Fill = ws.Brush;
            if (WorkspaceName is not null)
                WorkspaceName.Text = ws.Name;
        }
    }

    /// <summary>
    /// ブランチセレクタークリックでHEADにナビゲートする。
    /// </summary>
    private void NavigateToHead(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Repository { CurrentBranch: not null } repo)
        {
            LocalBranchTree?.Select(repo.CurrentBranch);
            repo.NavigateToCommit(repo.CurrentBranch.Head);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Fetchの処理を行う。
    /// </summary>
    private async void Fetch(object sender, TappedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
        {
            await repo.FetchAsync(e.KeyModifiers is KeyModifiers.Control);
            e.Handled = true;
        }
    }

    /// <summary>
    /// FetchDirectlyByHotKeyの処理を行う。
    /// </summary>
    private async void FetchDirectlyByHotKey(object sender, RoutedEventArgs e)
    {
        if (App.GetLauncher() is { CommandPalette: { } })
            return;

        if (DataContext is ViewModels.Repository repo)
        {
            await repo.FetchAsync(true);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Pullの処理を行う。
    /// </summary>
    private async void Pull(object sender, TappedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
        {
            await repo.PullAsync(e.KeyModifiers is KeyModifiers.Control);
            e.Handled = true;
        }
    }

    /// <summary>
    /// PullDirectlyByHotKeyの処理を行う。
    /// </summary>
    private async void PullDirectlyByHotKey(object sender, RoutedEventArgs e)
    {
        if (App.GetLauncher() is { CommandPalette: { } })
            return;

        if (DataContext is ViewModels.Repository repo)
        {
            await repo.PullAsync(true);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Pushの処理を行う。
    /// </summary>
    private async void Push(object sender, TappedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
        {
            await repo.PushAsync(e.KeyModifiers is KeyModifiers.Control);
            e.Handled = true;
        }
    }

    /// <summary>
    /// PushDirectlyByHotKeyの処理を行う。
    /// </summary>
    private async void PushDirectlyByHotKey(object sender, RoutedEventArgs e)
    {
        if (App.GetLauncher() is { CommandPalette: { } })
            return;

        if (DataContext is ViewModels.Repository repo)
        {
            await repo.PushAsync(true);
            e.Handled = true;
        }
    }

    /// <summary>
    /// リポジトリ設定ダイアログを開く。
    /// </summary>
    private async void OpenConfigure(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Repository repo)
        {
            await App.ShowDialog(new ViewModels.RepositoryConfigure(repo));
            e.Handled = true;
        }
    }

    /// <summary>
    /// ワークスペース切替メニューを表示する。
    /// </summary>
    private void OpenWorkspaceMenu(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            App.OpenWorkspaceMenu(btn, PlacementMode.BottomEdgeAlignedLeft, UpdateWorkspaceDisplay);

        e.Handled = true;
    }

    /// <summary>
    /// オーバーフローメニュー（···）を表示する。
    /// </summary>
    private void OpenOverflowMenu(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && DataContext is ViewModels.Repository repo)
        {
            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

            // Stash / Apply
            var stash = new MenuItem();
            stash.Header = App.Text("Stash");
            stash.Icon = App.CreateMenuIcon("Icons.Stashes.Add");
            stash.Click += async (_, ev) =>
            {
                await repo.StashAllAsync(false);
                ev.Handled = true;
            };
            if (!repo.IsBare)
                menu.Items.Add(stash);

            var apply = new MenuItem();
            apply.Header = App.Text("Apply");
            apply.Icon = App.CreateMenuIcon("Icons.Diff");
            apply.Click += (_, ev) =>
            {
                repo.ApplyPatch();
                ev.Handled = true;
            };
            if (!repo.IsBare)
                menu.Items.Add(apply);

            if (!repo.IsBare)
                menu.Items.Add(new MenuItem() { Header = "-" });

            // Git Flow
            var gitflow = new MenuItem();
            gitflow.Header = App.Text("GitFlow");
            gitflow.Icon = App.CreateMenuIcon("Icons.GitFlow");
            gitflow.Click += (_, ev) => OpenGitFlowMenu(repo, gitflow, ev);
            if (!repo.IsBare)
                menu.Items.Add(gitflow);

            // Git LFS
            var lfs = new MenuItem();
            lfs.Header = App.Text("GitLFS");
            lfs.Icon = App.CreateMenuIcon("Icons.LFS");
            lfs.Click += (_, ev) => OpenGitLFSMenu(repo, lfs, ev);
            if (!repo.IsBare)
                menu.Items.Add(lfs);

            // Bisect
            var bisect = new MenuItem();
            bisect.Header = App.Text("Bisect");
            bisect.Icon = App.CreateMenuIcon("Icons.Bisect");
            bisect.Click += (_, ev) => StartBisectFromMenu(repo, ev);
            if (!repo.IsBare)
                menu.Items.Add(bisect);

            if (!repo.IsBare)
                menu.Items.Add(new MenuItem() { Header = "-" });

            // Custom Actions
            var customActions = new MenuItem();
            customActions.Header = App.Text("Repository.CustomActions");
            customActions.Icon = App.CreateMenuIcon("Icons.Action");
            customActions.Click += (_, ev) => OpenCustomActionMenu(repo, customActions, ev);
            menu.Items.Add(customActions);

            // Cleanup
            var cleanup = new MenuItem();
            cleanup.Header = App.Text("Repository.Clean");
            cleanup.Icon = App.CreateMenuIcon("Icons.Clean");
            cleanup.Click += async (_, ev) =>
            {
                await repo.CleanupAsync();
                ev.Handled = true;
            };
            menu.Items.Add(cleanup);

            // Clear Index Cache
            var clearCache = new MenuItem();
            clearCache.Header = App.Text("Repository.ClearIndexCache");
            clearCache.Icon = App.CreateMenuIcon("Icons.Reset");
            clearCache.Click += async (_, ev) =>
            {
                await repo.ClearIndexCacheAsync();
                ev.Handled = true;
            };
            menu.Items.Add(clearCache);

            menu.Items.Add(new MenuItem() { Header = "-" });

            // Open With
            var openWith = new MenuItem();
            openWith.Header = App.Text("Repository.OpenWithExternalTools");
            openWith.Icon = App.CreateMenuIcon("Icons.OpenWith");
            openWith.Click += (_, ev) => OpenWithExternalTools(repo, openWith, ev);
            menu.Items.Add(openWith);

            // Git Logs
            var logs = new MenuItem();
            logs.Header = App.Text("Repository.ViewLogs");
            logs.Icon = App.CreateMenuIcon("Icons.Logs");
            logs.Click += async (_, ev) =>
            {
                await App.ShowDialog(new ViewModels.ViewLogs(repo));
                ev.Handled = true;
            };
            menu.Items.Add(logs);

            // Statistics
            var stats = new MenuItem();
            stats.Header = App.Text("Repository.Statistics");
            stats.Icon = App.CreateMenuIcon("Icons.Statistics");
            stats.Click += async (_, ev) =>
            {
                await App.ShowDialog(new ViewModels.Statistics(repo.FullPath));
                ev.Handled = true;
            };
            menu.Items.Add(stats);

            menu.Items.Add(new MenuItem() { Header = "-" });

            // Preferences
            var prefs = new MenuItem();
            prefs.Header = App.Text("Preferences");
            prefs.Icon = App.CreateMenuIcon("Icons.Settings");
            prefs.Click += (_, ev) =>
            {
                App.OpenPreferencesCommand.Execute(null);
                ev.Handled = true;
            };
            menu.Items.Add(prefs);

            // データの保管ディレクトリを開く
            var appData = new MenuItem();
            appData.Header = App.Text("OpenAppDataDir");
            appData.Icon = App.CreateMenuIcon("Icons.Explore");
            appData.Click += (_, ev) =>
            {
                App.OpenAppDataDirCommand.Execute(null);
                ev.Handled = true;
            };
            menu.Items.Add(appData);

            // Hotkeys
            var hotkeys = new MenuItem();
            hotkeys.Header = App.Text("Hotkeys");
            hotkeys.Icon = App.CreateMenuIcon("Icons.Hotkeys");
            hotkeys.Click += (_, ev) =>
            {
                App.OpenHotkeysCommand.Execute(null);
                ev.Handled = true;
            };
            menu.Items.Add(hotkeys);

            menu.Items.Add(new MenuItem() { Header = "-" });

            // 更新を確認（条件付き）
            if (App.IsCheckForUpdateCommandVisible)
            {
                var update = new MenuItem();
                update.Header = App.Text("SelfUpdate");
                update.Icon = App.CreateMenuIcon("Icons.SoftwareUpdate");
                update.Click += (_, ev) =>
                {
                    App.CheckForUpdateCommand.Execute(null);
                    ev.Handled = true;
                };
                menu.Items.Add(update);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            // About
            var about = new MenuItem();
            about.Header = App.Text("About");
            about.Icon = App.CreateMenuIcon("Icons.Info");
            about.Click += (_, ev) =>
            {
                App.OpenAboutCommand.Execute(null);
                ev.Handled = true;
            };
            menu.Items.Add(about);

            // 終了
            var quit = new MenuItem();
            quit.Header = App.Text("Quit");
            quit.Icon = App.CreateMenuIcon("Icons.Quit");
            quit.Click += (_, ev) =>
            {
                App.QuitCommand.Execute(null);
                ev.Handled = true;
            };
            menu.Items.Add(quit);

            menu.Open(btn);
        }

        e.Handled = true;
    }

    /// <summary>
    /// GitFlowメニューを構築して表示する。
    /// </summary>
    private static void OpenGitFlowMenu(ViewModels.Repository repo, Control control, RoutedEventArgs ev)
    {
        var menu = new ContextMenu();
        menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

        if (repo.IsGitFlowEnabled())
        {
            var startFeature = new MenuItem();
            startFeature.Header = App.Text("GitFlow.StartFeature");
            startFeature.Icon = App.CreateMenuIcon("Icons.GitFlow.Feature");
            startFeature.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.GitFlowStart(repo, Models.GitFlowBranchType.Feature));
                e.Handled = true;
            };

            var startRelease = new MenuItem();
            startRelease.Header = App.Text("GitFlow.StartRelease");
            startRelease.Icon = App.CreateMenuIcon("Icons.GitFlow.Release");
            startRelease.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.GitFlowStart(repo, Models.GitFlowBranchType.Release));
                e.Handled = true;
            };

            var startHotfix = new MenuItem();
            startHotfix.Header = App.Text("GitFlow.StartHotfix");
            startHotfix.Icon = App.CreateMenuIcon("Icons.GitFlow.Hotfix");
            startHotfix.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.GitFlowStart(repo, Models.GitFlowBranchType.Hotfix));
                e.Handled = true;
            };

            menu.Items.Add(startFeature);
            menu.Items.Add(startRelease);
            menu.Items.Add(startHotfix);
        }
        else
        {
            var init = new MenuItem();
            init.Header = App.Text("GitFlow.Init");
            init.Icon = App.CreateMenuIcon("Icons.Init");
            init.Click += (_, e) =>
            {
                if (repo.CurrentBranch is null)
                    App.RaiseException(repo.FullPath, App.Text("Error.GitFlowNoBranch"));
                else if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.InitGitFlow(repo));
                e.Handled = true;
            };
            menu.Items.Add(init);
        }

        menu.Open(control);
        ev.Handled = true;
    }

    /// <summary>
    /// GitLFSメニューを構築して表示する。
    /// </summary>
    private static void OpenGitLFSMenu(ViewModels.Repository repo, Control control, RoutedEventArgs ev)
    {
        var menu = new ContextMenu();
        menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

        if (repo.IsLFSEnabled())
        {
            var addPattern = new MenuItem();
            addPattern.Header = App.Text("GitLFS.AddTrackPattern");
            addPattern.Icon = App.CreateMenuIcon("Icons.File.Add");
            addPattern.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.LFSTrackCustomPattern(repo));
                e.Handled = true;
            };
            menu.Items.Add(addPattern);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var fetch = new MenuItem();
            fetch.Header = App.Text("GitLFS.Fetch");
            fetch.Icon = App.CreateMenuIcon("Icons.Fetch");
            fetch.IsEnabled = repo.Remotes.Count > 0;
            fetch.Click += async (_, e) =>
            {
                if (repo.CanCreatePopup())
                {
                    if (repo.Remotes.Count == 1)
                        await repo.ShowAndStartPopupAsync(new ViewModels.LFSFetch(repo));
                    else
                        repo.ShowPopup(new ViewModels.LFSFetch(repo));
                }
                e.Handled = true;
            };
            menu.Items.Add(fetch);

            var pull = new MenuItem();
            pull.Header = App.Text("GitLFS.Pull");
            pull.Icon = App.CreateMenuIcon("Icons.Pull");
            pull.IsEnabled = repo.Remotes.Count > 0;
            pull.Click += async (_, e) =>
            {
                if (repo.CanCreatePopup())
                {
                    if (repo.Remotes.Count == 1)
                        await repo.ShowAndStartPopupAsync(new ViewModels.LFSPull(repo));
                    else
                        repo.ShowPopup(new ViewModels.LFSPull(repo));
                }
                e.Handled = true;
            };
            menu.Items.Add(pull);

            var push = new MenuItem();
            push.Header = App.Text("GitLFS.Push");
            push.Icon = App.CreateMenuIcon("Icons.Push");
            push.IsEnabled = repo.Remotes.Count > 0;
            push.Click += async (_, e) =>
            {
                if (repo.CanCreatePopup())
                {
                    if (repo.Remotes.Count == 1)
                        await repo.ShowAndStartPopupAsync(new ViewModels.LFSPush(repo));
                    else
                        repo.ShowPopup(new ViewModels.LFSPush(repo));
                }
                e.Handled = true;
            };
            menu.Items.Add(push);

            var prune = new MenuItem();
            prune.Header = App.Text("GitLFS.Prune");
            prune.Icon = App.CreateMenuIcon("Icons.Clean");
            prune.Click += async (_, e) =>
            {
                if (repo.CanCreatePopup())
                    await repo.ShowAndStartPopupAsync(new ViewModels.LFSPrune(repo));
                e.Handled = true;
            };
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(prune);

            var locks = new MenuItem();
            locks.Header = App.Text("GitLFS.Locks");
            locks.Icon = App.CreateMenuIcon("Icons.Lock");
            locks.IsEnabled = repo.Remotes.Count > 0;
            if (repo.Remotes.Count == 1)
            {
                locks.Click += async (_, e) =>
                {
                    await App.ShowDialog(new ViewModels.LFSLocks(repo, repo.Remotes[0].Name));
                    e.Handled = true;
                };
            }
            else
            {
                foreach (var remote in repo.Remotes)
                {
                    var remoteName = remote.Name;
                    var lockRemote = new MenuItem();
                    lockRemote.Header = remoteName;
                    lockRemote.Click += async (_, e) =>
                    {
                        await App.ShowDialog(new ViewModels.LFSLocks(repo, remoteName));
                        e.Handled = true;
                    };
                    locks.Items.Add(lockRemote);
                }
            }

            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(locks);
        }
        else
        {
            var install = new MenuItem();
            install.Header = App.Text("GitLFS.Install");
            install.Icon = App.CreateMenuIcon("Icons.Init");
            install.Click += async (_, e) =>
            {
                await repo.InstallLFSAsync();
                e.Handled = true;
            };
            menu.Items.Add(install);
        }

        menu.Open(control);
        ev.Handled = true;
    }

    /// <summary>
    /// Bisect開始をメニューから実行する。
    /// </summary>
    private static async void StartBisectFromMenu(ViewModels.Repository repo, RoutedEventArgs e)
    {
        if (repo is { IsBisectCommandRunning: false, InProgressContext: null } && repo.CanCreatePopup())
        {
            if (repo.LocalChangesCount > 0)
                App.RaiseException(repo.FullPath, App.Text("Error.BisectHasLocalChanges"));
            else if (repo.IsBisectCommandRunning || repo.BisectState != Models.BisectState.None)
                App.RaiseException(repo.FullPath, App.Text("Error.BisectAlreadyRunning"));
            else
                await repo.ExecBisectCommandAsync("start");
        }

        e.Handled = true;
    }

    /// <summary>
    /// カスタムアクションメニューを構築して表示する。
    /// </summary>
    private static void OpenCustomActionMenu(ViewModels.Repository repo, Control control, RoutedEventArgs ev)
    {
        var menu = new ContextMenu();
        menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

        var actions = repo.GetCustomActions(Models.CustomActionScope.Repository);
        if (actions.Count > 0)
        {
            foreach (var action in actions)
            {
                var (dup, label) = action;
                var item = new MenuItem();
                item.Icon = App.CreateMenuIcon("Icons.Action");
                item.Header = label;
                item.Click += async (_, e) =>
                {
                    await repo.ExecCustomActionAsync(dup, null);
                    e.Handled = true;
                };
                menu.Items.Add(item);
            }
        }
        else
        {
            menu.Items.Add(new MenuItem() { Header = App.Text("Repository.CustomActions.Empty") });
        }

        menu.Open(control);
        ev.Handled = true;
    }

    /// <summary>
    /// 外部ツールで開くメニューを構築して表示する。
    /// </summary>
    private static void OpenWithExternalTools(ViewModels.Repository repo, Control anchor, RoutedEventArgs ev)
    {
        var fullpath = repo.FullPath;
        var menu = new ContextMenu();
        menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

        RenderOptions.SetBitmapInterpolationMode(menu, BitmapInterpolationMode.HighQuality);
        RenderOptions.SetEdgeMode(menu, EdgeMode.Antialias);
        TextOptions.SetTextRenderingMode(menu, TextRenderingMode.Antialias);

        var explore = new MenuItem();
        explore.Header = App.Text("Repository.Explore");
        explore.Icon = App.CreateMenuIcon("Icons.Explore");
        explore.Click += (_, e) =>
        {
            Native.OS.OpenInFileManager(fullpath);
            e.Handled = true;
        };

        var terminal = new MenuItem();
        terminal.Header = App.Text("Repository.Terminal");
        terminal.Icon = App.CreateMenuIcon("Icons.Terminal");
        terminal.Click += (_, e) =>
        {
            Native.OS.OpenTerminal(fullpath);
            e.Handled = true;
        };

        menu.Items.Add(explore);
        menu.Items.Add(terminal);

        var tools = Native.OS.ExternalTools;
        if (tools.Count > 0)
        {
            menu.Items.Add(new MenuItem() { Header = "-" });

            foreach (var tool in tools)
            {
                var dupTool = tool;
                var item = new MenuItem();
                item.Header = App.Text("Repository.OpenIn", dupTool.Name);
                item.Icon = new Image { Width = 16, Height = 16, Source = dupTool.IconImage };

                var options = dupTool.MakeLaunchOptions(fullpath);
                if (options is { Count: > 0 })
                {
                    foreach (var opt in options)
                    {
                        var subItem = new MenuItem();
                        subItem.Header = opt.Title;
                        subItem.Click += (_, e) =>
                        {
                            dupTool.Launch(opt.Args);
                            e.Handled = true;
                        };
                        item.Items.Add(subItem);
                    }

                    var openAsFolder = new MenuItem();
                    openAsFolder.Header = App.Text("Repository.OpenAsFolder");
                    openAsFolder.Click += (_, e) =>
                    {
                        dupTool.Launch(fullpath.Quoted());
                        e.Handled = true;
                    };
                    item.Items.Add(new MenuItem() { Header = "-" });
                    item.Items.Add(openAsFolder);
                }
                else
                {
                    item.Click += (_, e) =>
                    {
                        dupTool.Launch(fullpath.Quoted());
                        e.Handled = true;
                    };
                }

                menu.Items.Add(item);
            }
        }

        Dictionary<string, string> urls = [];
        foreach (var r in repo.Remotes)
        {
            if (r.TryGetVisitURL(out var visit))
                urls.Add(r.Name, visit);
        }

        if (urls.Count > 0)
        {
            menu.Items.Add(new MenuItem() { Header = "-" });

            foreach (var (name, addr) in urls)
            {
                var dupUrl = addr;
                var item = new MenuItem();
                item.Header = App.Text("Repository.Visit", name);
                item.Icon = App.CreateMenuIcon("Icons.Remotes");
                item.Click += (_, e) =>
                {
                    Native.OS.OpenBrowser(dupUrl);
                    e.Handled = true;
                };
                menu.Items.Add(item);
            }
        }

        menu.Open(anchor);
        ev.Handled = true;
    }
}
