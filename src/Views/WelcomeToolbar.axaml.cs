using System;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Komorebi.Views;

/// <summary>
/// ウェルカム画面ツールバーのコードビハインド。
/// </summary>
public partial class WelcomeToolbar : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public WelcomeToolbar()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateWorkspaceDisplay();
    }

    /// <summary>
    /// ワークスペースセレクターの表示（ドット色・名前）を更新する。
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
    /// ワークスペース選択メニューを構築して表示する。
    /// </summary>
    private void OpenWorkspaceMenu(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            App.OpenWorkspaceMenu(btn, PlacementMode.BottomEdgeAlignedRight, UpdateWorkspaceDisplay);

        e.Handled = true;
    }

    /// <summary>
    /// オーバーフローメニュー（···）を表示する。RepositoryToolbarと同一の表現。
    /// </summary>
    private void OpenOverflowMenu(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedRight;

            // 設定
            var prefs = new MenuItem();
            prefs.Header = App.Text("Preferences");
            prefs.Icon = App.CreateMenuIcon("Icons.Settings");
            prefs.Click += (_, ev) =>
            {
                App.OpenPreferencesCommand.Execute(null);
                ev.Handled = true;
            };
            menu.Items.Add(prefs);

            // AppDataDir
            var appData = new MenuItem();
            appData.Header = App.Text("OpenAppDataDir");
            appData.Icon = App.CreateMenuIcon("Icons.Explore");
            appData.Click += (_, ev) =>
            {
                App.OpenAppDataDirCommand.Execute(null);
                ev.Handled = true;
            };
            menu.Items.Add(appData);

            // ホットキー
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

            // セルフアップデート（条件付き）
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

            // 情報
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
    /// OpenLocalRepositoryの処理を行う。
    /// </summary>
    private async void OpenLocalRepository(object _1, RoutedEventArgs e)
    {
        var activePage = App.GetLauncher().ActivePage;
        if (activePage is null || !activePage.CanCreatePopup())
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        var preference = ViewModels.Preferences.Instance;
        var workspace = preference.GetActiveWorkspace();
        var initDir = workspace.DefaultCloneDir;
        if (string.IsNullOrEmpty(initDir) || !Directory.Exists(initDir))
            initDir = preference.GitDefaultCloneDir;

        var options = new FolderPickerOpenOptions() { AllowMultiple = false };
        if (Directory.Exists(initDir))
        {
            var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initDir);
            options.SuggestedStartLocation = folder;
        }

        try
        {
            var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (selected.Count == 1)
            {
                var folder = selected[0];
                var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                var repoPath = await ViewModels.Welcome.Instance.GetRepositoryRootAsync(folderPath);
                if (!string.IsNullOrEmpty(repoPath))
                {
                    await ViewModels.Welcome.Instance.AddRepositoryAsync(repoPath, null, false, true);
                    ViewModels.Welcome.Instance.Refresh();
                }
                else if (Directory.Exists(folderPath))
                {
                    var test = await new Commands.QueryRepositoryRootPath(folderPath).GetResultAsync();
                    ViewModels.Welcome.Instance.InitRepository(folderPath, null, test.StdErr);
                }
            }
        }
        catch (Exception exception)
        {
            App.RaiseException(string.Empty, App.Text("Error.FailedToOpenRepository", exception.Message));
        }

        e.Handled = true;
    }
}
