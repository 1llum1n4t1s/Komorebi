using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

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
    /// オーバーフローメニュー（···）を表示する。
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
}
