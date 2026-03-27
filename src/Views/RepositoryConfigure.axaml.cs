using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Komorebi.Views;

/// <summary>
///     リポジトリ設定（.git/config編集）ダイアログのコードビハインド。
/// </summary>
public partial class RepositoryConfigure : ChromelessWindow
{
    /// <summary>
    ///     コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public RepositoryConfigure()
    {
        CloseOnESC = true;
        InitializeComponent();
    }

    /// <summary>
    ///     ウィンドウが閉じられる際の処理。
    /// </summary>
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (!Design.IsDesignMode && DataContext is ViewModels.RepositoryConfigure configure)
            await configure.SaveAsync();
    }

    /// <summary>
    ///     ConventionalTypesFileの選択処理を行う。
    /// </summary>
    private async void SelectConventionalTypesFile(object sender, RoutedEventArgs e)
    {
        var options = new FilePickerOpenOptions()
        {
            FileTypeFilter = [new FilePickerFileType("Conventional Commit Types") { Patterns = ["*.json"] }],
            AllowMultiple = false,
        };

        var selected = await StorageProvider.OpenFilePickerAsync(options);
        if (selected.Count == 1 && DataContext is ViewModels.RepositoryConfigure vm)
            vm.ConventionalTypesOverride = selected[0].Path.LocalPath;

        e.Handled = true;
    }

    /// <summary>
    ///     ExecutableForCustomActionの選択処理を行う。
    /// </summary>
    private async void SelectExecutableForCustomAction(object sender, RoutedEventArgs e)
    {
        var options = new FilePickerOpenOptions()
        {
            FileTypeFilter = [new FilePickerFileType("Executable file(script)") { Patterns = ["*.*"] }],
            AllowMultiple = false,
        };

        var selected = await StorageProvider.OpenFilePickerAsync(options);
        if (selected.Count == 1 && sender is Button { DataContext: Models.CustomAction action })
            action.Executable = selected[0].Path.LocalPath;

        e.Handled = true;
    }

    /// <summary>
    ///     EditCustomActionControlsの処理を行う。
    /// </summary>
    private async void EditCustomActionControls(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Models.CustomAction act })
            return;

        var dialog = new ConfigureCustomActionControls()
        {
            DataContext = new ViewModels.ConfigureCustomActionControls(act.Controls)
        };

        await dialog.ShowDialog(this);
        e.Handled = true;
    }

    /// <summary>
    ///     NewCustomIssueTrackerイベントのハンドラ。
    /// </summary>
    private void OnNewCustomIssueTracker(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RepositoryConfigure vm)
            vm.AddIssueTracker("New Issue Tracker", @"#(\d+)", "https://xxx/$1");

        e.Handled = true;
    }

    /// <summary>
    ///     AddGitHubIssueTrackerイベントのハンドラ。
    /// </summary>
    private void OnAddGitHubIssueTracker(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RepositoryConfigure vm)
        {
            var link = "https://github.com/username/repository/issues/$1";
            var remotes = vm.GetRemoteVisitUrls();
            foreach (var remote in remotes)
            {
                if (remote.Contains("github.com", StringComparison.Ordinal))
                {
                    link = $"{remote}/issues/$1";
                    break;
                }
            }

            vm.AddIssueTracker("GitHub Issue", @"#(\d+)", link);
        }

        e.Handled = true;
    }

    /// <summary>
    ///     AddJiraIssueTrackerイベントのハンドラ。
    /// </summary>
    private void OnAddJiraIssueTracker(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RepositoryConfigure vm)
        {
            vm.AddIssueTracker(
                "Jira Tracker",
                @"PROJ-(\d+)",
                "https://jira.yourcompany.com/browse/PROJ-$1");
        }

        e.Handled = true;
    }

    /// <summary>
    ///     AddAzureWorkItemTrackerイベントのハンドラ。
    /// </summary>
    private void OnAddAzureWorkItemTracker(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RepositoryConfigure vm)
        {
            vm.AddIssueTracker(
                "Azure DevOps Tracker",
                @"#(\d+)",
                "https://dev.azure.com/yourcompany/workspace/_workitems/edit/$1");
        }

        e.Handled = true;
    }

    /// <summary>
    ///     AddGitLabIssueTrackerイベントのハンドラ。
    /// </summary>
    private void OnAddGitLabIssueTracker(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RepositoryConfigure vm)
        {
            var link = "https://gitlab.com/username/repository/-/issues/$1";
            var remotes = vm.GetRemoteVisitUrls();
            foreach (var remote in remotes)
            {
                link = $"{remote}/-/issues/$1";
                break;
            }

            vm.AddIssueTracker("GitLab Issue", @"#(\d+)", link);
        }

        e.Handled = true;
    }

    /// <summary>
    ///     AddGitLabMergeRequestTrackerイベントのハンドラ。
    /// </summary>
    private void OnAddGitLabMergeRequestTracker(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RepositoryConfigure vm)
        {
            var link = "https://gitlab.com/username/repository/-/merge_requests/$1";
            var remotes = vm.GetRemoteVisitUrls();
            foreach (var remote in remotes)
            {
                link = $"{remote}/-/merge_requests/$1";
                break;
            }

            vm.AddIssueTracker("GitLab MR", @"!(\d+)", link);
        }

        e.Handled = true;
    }

    /// <summary>
    ///     AddGiteeIssueTrackerイベントのハンドラ。
    /// </summary>
    private void OnAddGiteeIssueTracker(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RepositoryConfigure vm)
        {
            var link = "https://gitee.com/username/repository/issues/$1";
            var remotes = vm.GetRemoteVisitUrls();
            foreach (var remote in remotes)
            {
                if (remote.Contains("gitee.com", StringComparison.Ordinal))
                {
                    link = $"{remote}/issues/$1";
                    break;
                }
            }

            vm.AddIssueTracker("Gitee Issue", @"#([0-9A-Z]{6,10})", link);
        }

        e.Handled = true;
    }

    /// <summary>
    ///     AddGiteePullRequestTrackerイベントのハンドラ。
    /// </summary>
    private void OnAddGiteePullRequestTracker(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RepositoryConfigure vm)
        {
            var link = "https://gitee.com/username/repository/pulls/$1";
            var remotes = vm.GetRemoteVisitUrls();
            foreach (var remote in remotes)
            {
                if (remote.Contains("gitee.com", StringComparison.Ordinal))
                {
                    link = $"{remote}/pulls/$1";
                    break;
                }
            }

            vm.AddIssueTracker("Gitee Pull Request", @"!(\d+)", link);
        }

        e.Handled = true;
    }

    /// <summary>
    ///     AddGerritChangeIdTrackerイベントのハンドラ。
    /// </summary>
    private void OnAddGerritChangeIdTracker(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RepositoryConfigure vm)
        {
            vm.AddIssueTracker(
                "Gerrit Change-Id",
                @"(I[A-Za-z0-9]{40})",
                "https://gerrit.yourcompany.com/q/$1");
        }

        e.Handled = true;
    }

    /// <summary>
    ///     RemoveIssueTrackerイベントのハンドラ。
    /// </summary>
    private void OnRemoveIssueTracker(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RepositoryConfigure vm)
            vm.RemoveIssueTracker();

        e.Handled = true;
    }
}
