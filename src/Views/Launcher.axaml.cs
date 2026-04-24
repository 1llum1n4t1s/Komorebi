using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.VisualTree;

namespace Komorebi.Views;

/// <summary>
/// ランチャーメインウィンドウ（タブ管理・ホットキー処理）のコードビハインド。
/// </summary>
public partial class Launcher : ChromelessWindow
{
    /// <summary>
    /// キャプション（タイトルバー）の高さを保持するスタイルプロパティ。
    /// </summary>
    public static readonly StyledProperty<GridLength> CaptionHeightProperty =
        AvaloniaProperty.Register<Launcher, GridLength>(nameof(CaptionHeight));

    /// <summary>
    /// キャプションの高さを取得・設定する。
    /// </summary>
    public GridLength CaptionHeight
    {
        get => GetValue(CaptionHeightProperty);
        set => SetValue(CaptionHeightProperty, value);
    }

    /// <summary>
    /// 左側にキャプションボタンを表示するかどうかのスタイルプロパティ。macOSで使用。
    /// </summary>
    public static readonly StyledProperty<bool> HasLeftCaptionButtonProperty =
        AvaloniaProperty.Register<Launcher, bool>(nameof(HasLeftCaptionButton));

    /// <summary>
    /// 左側にキャプションボタンを表示するかどうかを取得・設定する。
    /// </summary>
    public bool HasLeftCaptionButton
    {
        get => GetValue(HasLeftCaptionButtonProperty);
        set => SetValue(HasLeftCaptionButtonProperty, value);
    }

    /// <summary>
    /// 右側にキャプションボタンを表示するかどうかを取得する。WindowsおよびLinux（非システムフレーム時）で表示。
    /// </summary>
    public bool HasRightCaptionButton
    {
        get
        {
            if (OperatingSystem.IsLinux())
                return !Native.OS.UseSystemWindowFrame;

            return OperatingSystem.IsWindows();
        }
    }

    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public Launcher()
    {
        // プラットフォームごとのキャプション（タイトルバー）高さを設定する
        if (OperatingSystem.IsMacOS())
        {
            HasLeftCaptionButton = true;
            CaptionHeight = new GridLength(34);
        }
        else if (UseSystemWindowFrame)
        {
            CaptionHeight = new GridLength(30);
        }
        else
        {
            CaptionHeight = new GridLength(38);
        }

        InitializeComponent();
        PositionChanged += OnPositionChanged;

        TitleBarBG.Bind(BackgroundProperty, new DynamicResourceExtension("Brush.TitleBar"));

        // 前回のウィンドウサイズを復元する
        var layout = ViewModels.Preferences.Instance.Layout;
        Width = layout.LauncherWidth;
        Height = layout.LauncherHeight;

        // 前回のウィンドウ位置がスクリーン内に収まる場合は復元する
        var x = layout.LauncherPositionX;
        var y = layout.LauncherPositionY;
        if (x != int.MinValue && y != int.MinValue && Screens is { } screens)
        {
            var position = new PixelPoint(x, y);
            var size = new PixelSize((int)layout.LauncherWidth, (int)layout.LauncherHeight);
            var desiredRect = new PixelRect(position, size);
            for (var i = 0; i < screens.ScreenCount; i++)
            {
                var screen = screens.All[i];
                if (screen.WorkingArea.Contains(desiredRect))
                {
                    Position = position;
                    return;
                }
            }
        }

        // スクリーン外の場合は中央に表示する
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    /// <summary>
    /// ウィンドウを最前面に表示する。
    /// </summary>
    public void BringToTop()
    {
        // 最小化されている場合は前の状態に戻し、そうでなければアクティブにする
        if (WindowState == WindowState.Minimized)
            WindowState = _lastWindowState;
        else
            Activate();
    }

    /// <summary>
    /// ウィンドウが開かれた際の処理。
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // 前回の最大化/フルスクリーン状態を復元する
        var state = ViewModels.Preferences.Instance.Layout.LauncherWindowState;
        if (state == WindowState.Maximized || state == WindowState.FullScreen)
            WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// プロパティが変更された際の処理。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            var state = (WindowState)change.NewValue!;
            _lastWindowState = (WindowState)change.OldValue!;

            // macOSではフルスクリーン時にキャプションボタンを非表示にする
            if (OperatingSystem.IsMacOS())
                HasLeftCaptionButton = state != WindowState.FullScreen;
            else if (!UseSystemWindowFrame)
                // 最大化時はキャプション高さを小さくする
                CaptionHeight = new GridLength(state == WindowState.Maximized ? 30 : 38);

            // ウィンドウ状態を設定に保存する（Minimized は除外。タスクバー最小化で次回起動が最小化状態になるのを防ぐ）
            if (state != WindowState.Minimized)
                ViewModels.Preferences.Instance.Layout.LauncherWindowState = state;
        }
        else if (change.Property == IsActiveProperty)
        {
            if (!IsActive && DataContext is ViewModels.Launcher { CommandPalette: { } } vm)
                vm.CommandPalette = null;
        }
    }

    /// <summary>
    /// サイズが変更された際の処理。
    /// </summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        // 通常ウィンドウ状態の場合のみサイズを設定に保存する
        if (WindowState == WindowState.Normal)
        {
            var layout = ViewModels.Preferences.Instance.Layout;
            layout.LauncherWidth = Width;
            layout.LauncherHeight = Height;
        }
    }

    /// <summary>
    /// キーが押された際のイベント処理。
    /// </summary>
    protected override async void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is not ViewModels.Launcher vm)
            return;

        // AltGr（Ctrl+Altとして検出される）の場合はホットキー処理をスキップする
        bool isAltGr = e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                       e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (isAltGr)
        {
            base.OnKeyDown(e);
            return;
        }

        // Windows/Linux専用のホットキー（macOSではシステムメニューバーで登録済み）
        if (!OperatingSystem.IsMacOS())
        {
            if (e is { KeyModifiers: KeyModifiers.Control, Key: Key.OemComma })
            {
                // Ctrl+, → 環境設定ダイアログを開く
                await App.ShowDialog(new Preferences());
                e.Handled = true;
                return;
            }

            if (e is { KeyModifiers: KeyModifiers.None, Key: Key.F1 })
            {
                // F1 → ホットキー一覧ダイアログを開く
                await App.ShowDialog(new Hotkeys());
                e.Handled = true;
                return;
            }

            if (e is { KeyModifiers: KeyModifiers.Control, Key: Key.Q })
            {
                // Ctrl+Q → アプリケーションを終了する
                App.Quit(0);
                return;
            }
        }

        var cmdKey = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

        // コマンドパレット表示中はホットキーをブロックする
        if (vm.CommandPalette is not null)
        {
            if (e.Key == Key.Escape)
            {
                vm.CommandPalette = null;
                e.Handled = true;
            }
            else if (vm.ActivePage.Data is ViewModels.Repository repo
                && vm.CommandPalette is ViewModels.LauncherPagesCommandPalette
                && e.Key == Key.P
                && e.KeyModifiers == (cmdKey | KeyModifiers.Shift))
            {
                vm.CommandPalette = new ViewModels.RepositoryCommandPalette(repo);
                e.Handled = true;
            }

            return;
        }

        // Ctrl（macOSではCmd）キーとの組み合わせ
        if (e.KeyModifiers.HasFlag(cmdKey))
        {
            if (e.Key == Key.W)
            {
                // Ctrl+W → 現在のタブを閉じる
                vm.CloseTab(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N)
            {
                // Ctrl+N → 新しいタブでクローンダイアログを開く
                if (vm.ActivePage.Data is not ViewModels.Welcome)
                    vm.AddNewTab();

                ViewModels.Welcome.Instance.Clone();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                // Ctrl+Shift+O → 新しいタブでローカルリポジトリを開くダイアログを表示する
                if (vm.ActivePage.Data is not ViewModels.Welcome)
                    vm.AddNewTab();

                ViewModels.Welcome.Instance.OpenLocalRepository();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.T)
            {
                // Ctrl+T → 新しいタブを追加する
                vm.AddNewTab();
                e.Handled = true;
                return;
            }

            // Ctrl+Tab / Cmd+Alt+Right → 次のタブに移動する
            if ((OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.Right) ||
                (!OperatingSystem.IsMacOS() && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Tab))
            {
                vm.GotoNextTab();
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+Tab / Cmd+Alt+Left → 前のタブに移動する
            if ((OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.Left) ||
                (!OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Tab))
            {
                vm.GotoPrevTab();
                e.Handled = true;
                return;
            }

            if (vm.ActivePage.Data is ViewModels.Repository repo)
            {
                // リポジトリビュー内のホットキー
                switch (e.Key)
                {
                    case Key.D1 or Key.NumPad1:
                        // Ctrl+1 → 履歴ビューに切り替える
                        repo.SelectedViewIndex = 0;
                        e.Handled = true;
                        return;
                    case Key.D2 or Key.NumPad2:
                        // Ctrl+2 → ワーキングコピービューに切り替える
                        repo.SelectedViewIndex = 1;
                        e.Handled = true;
                        return;
                    case Key.D3 or Key.NumPad3:
                        // Ctrl+3 → スタッシュビューに切り替える
                        repo.SelectedViewIndex = 2;
                        e.Handled = true;
                        return;
                    case Key.F when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                        // Ctrl+Shift+F → コミット検索を開始する
                        repo.IsSearchingCommits = true;
                        e.Handled = true;
                        return;
                    case Key.H when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                        // Ctrl+Shift+H → コミット検索を終了する
                        repo.IsSearchingCommits = false;
                        e.Handled = true;
                        return;
                    case Key.P when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                        // Ctrl+Shift+P → コマンドパレットを開く
                        vm.CommandPalette = new ViewModels.RepositoryCommandPalette(repo);
                        e.Handled = true;
                        return;
                }
            }
            else
            {
                // ウェルカム画面でのホットキー
                var welcome = this.FindDescendantOfType<Welcome>();
                if (welcome is not null)
                {
                    if (e.Key == Key.F)
                    {
                        // Ctrl+F → 検索ボックスにフォーカスする
                        welcome.SearchBox.Focus();
                        e.Handled = true;
                        return;
                    }
                }
            }
        }
        else if (e.Key == Key.Escape)
        {
            // ESC → コマンドパレットを閉じるか、ポップアップをキャンセルする
            if (vm.CommandPalette is not null)
                vm.CommandPalette = null;
            else
                vm.ActivePage.CancelPopup();

            e.Handled = true;
            return;
        }
        else if (e.Key == Key.F5)
        {
            // F5 → リポジトリを全更新するか、ウェルカム画面のステータスを更新する
            if (vm.ActivePage.Data is ViewModels.Repository repo)
            {
                repo.RefreshAll();
                e.Handled = true;
                return;
            }
            else if (vm.ActivePage.Data is ViewModels.Welcome welcome)
            {
                e.Handled = true;
                await welcome.UpdateStatusAsync(true, null);
                return;
            }
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// ウィンドウが閉じられる直前の処理。全タブのリポジトリを閉じる。
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (!Design.IsDesignMode && DataContext is ViewModels.Launcher launcher)
            launcher.CloseAll();
    }

    /// <summary>
    /// ウィンドウが閉じられた後の処理。Preferences を保存し、明示的に App.Quit でシャットダウンする。
    /// ShutdownMode.OnExplicitShutdown の運用に合わせ、MainWindow の Closed イベントで
    /// 初めて desktop.Shutdown が呼ばれる形に集約している。
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Design モード（Hot Reload / Previewer 等）で OnClosed が発火した場合、
        // Preferences 保存 & App.Quit によるプロセス終了を避け、デザイナーセッションを道連れにしない。
        if (Design.IsDesignMode)
            return;

        ViewModels.Preferences.Instance.Save();
        App.Quit(0);
    }

    /// <summary>
    /// ウィンドウ位置変更時のハンドラ。通常状態の場合のみ位置を設定に保存する。
    /// </summary>
    private void OnPositionChanged(object sender, PixelPointEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            var layout = ViewModels.Preferences.Instance.Layout;
            layout.LauncherPositionX = Position.X;
            layout.LauncherPositionY = Position.Y;
        }
    }

    /// <summary>
    /// OpenPagesCommandPaletteイベントのハンドラ。
    /// </summary>
    private void OnOpenPagesCommandPalette(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Launcher vm)
            vm.CommandPalette = new ViewModels.LauncherPagesCommandPalette(vm);
        e.Handled = true;
    }

    /// <summary>
    /// CloseCommandPaletteイベントのハンドラ。
    /// </summary>
    private void OnCloseCommandPalette(object sender, PointerPressedEventArgs e)
    {
        if (e.Source == sender && DataContext is ViewModels.Launcher vm)
            vm.CommandPalette = null;
        e.Handled = true;
    }

    /// <summary>
    /// 最小化前のウィンドウ状態を保持するフィールド。復元時に使用する。
    /// </summary>
    private WindowState _lastWindowState = WindowState.Normal;
}
