using System.ComponentModel;

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

        // Launcher の ActiveWorkspace 変化を自動反映する（旧コードは明示呼出ししかなく workspace 切替が反映されなかった）
        if (App.GetLauncher() is { } launcher)
            launcher.PropertyChanged += OnLauncherPropertyChanged;
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (App.GetLauncher() is { } launcher)
            launcher.PropertyChanged -= OnLauncherPropertyChanged;
    }

    private void OnLauncherPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.Launcher.ActiveWorkspace))
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
        if (sender is not Button btn)
        {
            e.Handled = true;
            return;
        }

        var menu = new ContextMenu
        {
            Placement = PlacementMode.BottomEdgeAlignedRight,
        };

        var prefs = new MenuItem
        {
            Header = App.Text("Preferences"),
            Icon = App.CreateMenuIcon("Icons.Preferences"),
        };
        prefs.Click += (_, ev) =>
        {
            App.OpenPreferencesCommand.Execute(null);
            ev.Handled = true;
        };
        menu.Items.Add(prefs);

        var appData = new MenuItem
        {
            Header = App.Text("OpenAppDataDir"),
            Icon = App.CreateMenuIcon("Icons.Explore"),
        };
        appData.Click += (_, ev) =>
        {
            App.OpenAppDataDirCommand.Execute(null);
            ev.Handled = true;
        };
        menu.Items.Add(appData);

        var hotkeys = new MenuItem
        {
            Header = App.Text("Hotkeys"),
            Icon = App.CreateMenuIcon("Icons.Hotkeys"),
        };
        hotkeys.Click += (_, ev) =>
        {
            App.OpenHotkeysCommand.Execute(null);
            ev.Handled = true;
        };
        menu.Items.Add(hotkeys);

        menu.Items.Add(new MenuItem() { Header = "-" });

        if (App.IsCheckForUpdateCommandVisible)
        {
            var update = new MenuItem
            {
                Header = App.Text("SelfUpdate"),
                Icon = App.CreateMenuIcon("Icons.SoftwareUpdate"),
            };
            update.Click += (_, ev) =>
            {
                App.CheckForUpdateCommand.Execute(null);
                ev.Handled = true;
            };
            menu.Items.Add(update);
            menu.Items.Add(new MenuItem() { Header = "-" });
        }

        var about = new MenuItem
        {
            Header = App.Text("About"),
            Icon = App.CreateMenuIcon("Icons.Info"),
        };
        about.Click += (_, ev) =>
        {
            App.OpenAboutCommand.Execute(null);
            ev.Handled = true;
        };
        menu.Items.Add(about);

        var quit = new MenuItem
        {
            Header = App.Text("Quit"),
            Icon = App.CreateMenuIcon("Icons.Quit"),
        };
        quit.Click += (_, ev) =>
        {
            App.QuitCommand.Execute(null);
            ev.Handled = true;
        };
        menu.Items.Add(quit);

        menu.Open(btn);
        e.Handled = true;
    }
}
