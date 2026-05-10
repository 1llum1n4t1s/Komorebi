using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Komorebi.Views;

/// <summary>
/// カスタムタイトルバーを持つクロムレスウィンドウの基底クラス。ESCで閉じる機能やズーム操作に対応。
/// </summary>
public class ChromelessWindow : Window
{
    public static readonly StyledProperty<double> LeftCaptionButtonWidthProperty =
        AvaloniaProperty.Register<ChromelessWindow, double>(nameof(LeftCaptionButtonWidth), 72.0);

    public double LeftCaptionButtonWidth
    {
        get => GetValue(LeftCaptionButtonWidthProperty);
        set => SetValue(LeftCaptionButtonWidthProperty, value);
    }

    public bool UseSystemWindowFrame
    {
        get => Native.OS.UseSystemWindowFrame;
    }

    public bool CloseOnESC
    {
        get;
        set;
    } = false;

    protected override Type StyleKeyOverride => typeof(Window);

    private const double RESIZE_GRIP_SIZE = 6;

    // スクリーン構成変化時、自身が非アクティブだった場合に再最大化を保留しておくフラグ。
    // アクティブになった際に追従処理を実行することで、他アプリ（Chrome 等）からフォーカスを奪うのを避ける。
    private bool _pendingMaximizedSync;

    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public ChromelessWindow()
    {
        // macOSのキャプションボタン幅をズーム倍率に応じて調整する
        LeftCaptionButtonWidth = 72.0 / Math.Max(1.0, ViewModels.Preferences.Instance.Zoom);
        Focusable = true;
        // プラットフォーム固有のウィンドウ設定を適用する
        Native.OS.SetupForWindow(this);

        // Windowsではボーダーレスウィンドウのリサイズをトンネルイベントで処理する
        if (OperatingSystem.IsWindows())
        {
            AddHandler(PointerPressedEvent, OnResizeGripPointerPressed, RoutingStrategies.Tunnel);
            AddHandler(PointerMovedEvent, OnResizeGripPointerMoved, RoutingStrategies.Tunnel);
        }
    }

    /// <summary>
    /// 保存済みのウィンドウ位置とサイズが接続されているいずれかのスクリーンの作業領域に
    /// 完全に収まるかを判定し、収まる場合のみ <see cref="Window.Position"/> に適用する。
    /// 複数モニタ環境で別モニタに保存された位置を復元する際に、スクリーン構成変更により
    /// 画面外に出てしまうのを防ぐ（upstream issue #2100 対応の共通化）。
    /// </summary>
    /// <param name="x">復元対象の X 座標（physical pixels）。<see cref="int.MinValue"/> なら未保存扱い。</param>
    /// <param name="y">復元対象の Y 座標（physical pixels）。<see cref="int.MinValue"/> なら未保存扱い。</param>
    /// <param name="width">ウィンドウの期待幅（logical pixels、<c>Window.Width</c> 由来）。</param>
    /// <param name="height">ウィンドウの期待高さ（logical pixels、<c>Window.Height</c> 由来）。</param>
    /// <returns>位置を適用できた場合 true。スクリーン外／未保存なら false。</returns>
    /// <remarks>
    /// Avalonia 11 では <see cref="WindowBase.Screens"/> が constructor 時点で null の場合があるため、
    /// このメソッドは <c>OnOpened</c> 以降で呼び出す必要がある。
    /// <para>
    /// <c>Window.Width</c>/<c>Window.Height</c> は logical pixels だが、
    /// <c>Screen.WorkingArea</c> は physical pixels。マルチモニタで DPI が異なる場合に
    /// 収容判定が誤らないよう、各スクリーンの <c>Scaling</c> で物理サイズに換算する
    /// （gemini PR #17 レビュー対応）。
    /// </para>
    /// </remarks>
    protected bool TryRestoreWindowPosition(int x, int y, double width, double height)
    {
        if (x == int.MinValue || y == int.MinValue || Screens is not { } screens)
            return false;

        var position = new PixelPoint(x, y);
        for (var i = 0; i < screens.ScreenCount; i++)
        {
            var screen = screens.All[i];
            // 各モニタの scaling を掛けて physical pixels に換算した上で収容判定する
            var physicalWidth = (int)(width * screen.Scaling);
            var physicalHeight = (int)(height * screen.Scaling);
            var desiredRect = new PixelRect(position, new PixelSize(physicalWidth, physicalHeight));
            if (screen.WorkingArea.Contains(desiredRect))
            {
                Position = position;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// ウィンドウのドラッグ移動を開始する。シングルクリック時のみ有効。
    /// </summary>
    public void BeginMoveWindow(object _, PointerPressedEventArgs e)
    {
        // シングルクリックの場合のみドラッグ移動を開始する（ダブルクリックは最大化用）
        if (e.ClickCount == 1)
            BeginMoveDrag(e);

        e.Handled = true;
    }

    /// <summary>
    /// ウィンドウの最大化と通常サイズを切り替える。
    /// </summary>
    public void MaximizeOrRestoreWindow(object _, TappedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
        else
            WindowState = WindowState.Maximized;

        e.Handled = true;
    }

    /// <summary>
    /// 派生クラスでスクリーン構成変化（モニタ追加・削除、解像度変更、DPI 変更、リモートデスクトップ接続/再接続）に
    /// 追従して最大化サイズを再計算するかどうかを表す opt-in プロパティ。
    /// 既定は false。最大化を頻繁に使う Launcher / Blame / FileHistories のみ true を返す。
    /// 短命なダイアログ（Alert / Confirm / Askpass 等）では追従不要のため false で良い。
    /// </summary>
    protected virtual bool TrackScreenChanges => false;

    /// <summary>
    /// ウィンドウが開かれた際の処理。スクリーン構成変化の監視を開始する（opt-in）。
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // 大量に存在する短命ダイアログまで Screens.Changed を購読すると、構成変化時に
        // 全インスタンスが ScheduleMaximizedSync を投げてフォーカスが奪われる事故源になる。
        // 派生クラス側で opt-in したウィンドウのみ監視する。
        if (TrackScreenChanges && Screens is { } screens)
            screens.Changed += OnScreensChanged;
    }

    /// <summary>
    /// ウィンドウが閉じられた際の処理。スクリーン構成変化の監視を解除する。
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        if (TrackScreenChanges && Screens is { } screens)
            screens.Changed -= OnScreensChanged;

        base.OnClosed(e);
    }

    /// <summary>
    /// スクリーン構成（モニタ追加・削除、解像度変更、DPI 変更、リモートデスクトップ接続/再接続）が
    /// 変化した際に、最大化中のウィンドウを新しい WorkingArea に追従させる。
    /// 例: 接続元の 3440x1440 モニタから 2560x1440 モニタへリモートデスクトップを移動すると
    /// 接続先の解像度が縮小されるが、Maximized 状態のままだと旧解像度のサイズが残ってしまう。
    /// </summary>
    private void OnScreensChanged(object sender, EventArgs e)
    {
        if (WindowState != WindowState.Maximized)
            return;

        // 自身が非アクティブな場合はその場で再最大化せず、アクティブ化時まで保留する。
        // Normal → Maximized のトグルは Win32 ShowWindow を伴いフォアグラウンドを奪う動作のため、
        // 他アプリ（例: Chrome）を操作中に Komorebi に勝手に切り替わるのを防ぐ。
        if (!IsActive)
        {
            _pendingMaximizedSync = true;
            return;
        }

        ScheduleMaximizedSync();
    }

    /// <summary>
    /// 最大化サイズを現在のスクリーン WorkingArea に再追従させる。
    /// 同期的に Normal → Maximized すると Avalonia 内部のスクリーン構成更新と競合して
    /// 旧 WorkingArea が拾われる可能性があるため、レイアウトパス完了後に Background で実行する。
    /// </summary>
    private void ScheduleMaximizedSync()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (WindowState != WindowState.Maximized)
                return;

            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// テンプレート適用時の処理を行う。
    /// </summary>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // カスタムウィンドウフレーム使用時はリサイズ用のボーダーにイベントハンドラを登録する
        if (Classes.Contains("custom_window_frame") && CanResize)
        {
            // ウィンドウの8方向のリサイズボーダー名
            string[] borderNames = [
                "PART_BorderTopLeft",
                "PART_BorderTop",
                "PART_BorderTopRight",
                "PART_BorderLeft",
                "PART_BorderRight",
                "PART_BorderBottomLeft",
                "PART_BorderBottom",
                "PART_BorderBottomRight",
            ];

            // 各ボーダーにポインター押下のイベントハンドラを設定する
            foreach (var name in borderNames)
            {
                var border = e.NameScope.Find<Border>(name);
                if (border is not null)
                {
                    border.PointerPressed -= OnWindowBorderPointerPressed;
                    border.PointerPressed += OnWindowBorderPointerPressed;
                }
            }
        }
    }

    /// <summary>
    /// プロパティが変更された際の処理。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Windowsでウィンドウ状態が変わった場合、最大化時のパディングを調整する
        if (change.Property == WindowStateProperty && OperatingSystem.IsWindows())
        {
            if (WindowState == WindowState.Maximized)
            {
                // 最大化時はボーダーとパディングをすべて除去する
                BorderThickness = new Thickness(0);
                Padding = new Thickness(0);
            }
            else
            {
                // 通常時はボーダーを表示する
                BorderThickness = new Thickness(1);
                Padding = new Thickness(0);
            }
        }
        else if (change.Property == IsActiveProperty && IsActive && _pendingMaximizedSync)
        {
            // 非アクティブ時に発生したスクリーン構成変化に対する再最大化を、アクティブ化のタイミングで消化する
            _pendingMaximizedSync = false;
            if (WindowState == WindowState.Maximized)
                ScheduleMaximizedSync();
        }
    }

    /// <summary>
    /// キーが押された際のイベント処理。
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled)
            return;

        // ESCキーでウィンドウを閉じる（CloseOnESCが有効な場合）
        if (e is { Key: Key.Escape, KeyModifiers: KeyModifiers.None } && CloseOnESC)
        {
            Close();
            e.Handled = true;
            return;
        }

        // Ctrl/Cmd + +/- でズームイン/アウトする
        if (e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
        {
            if (e.Key == Key.OemPlus)
            {
                // ズームイン（最大2.5倍）
                var zoom = Math.Min(ViewModels.Preferences.Instance.Zoom + 0.05, 2.5);
                ViewModels.Preferences.Instance.Zoom = zoom;
                LeftCaptionButtonWidth = 72.0 / zoom;
                e.Handled = true;
            }
            else if (e.Key == Key.OemMinus)
            {
                // ズームアウト（最小1.0倍）
                var zoom = Math.Max(ViewModels.Preferences.Instance.Zoom - 0.05, 1);
                ViewModels.Preferences.Instance.Zoom = zoom;
                LeftCaptionButtonWidth = 72.0 / zoom;
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// ウィンドウボーダーのポインター押下時にリサイズドラッグを開始する。
    /// </summary>
    private void OnWindowBorderPointerPressed(object sender, PointerPressedEventArgs e)
    {
        // ボーダーのTagにセットされたWindowEdge方向でリサイズを開始する
        if (sender is Border { Tag: WindowEdge edge } && CanResize)
            BeginResizeDrag(edge, e);
    }

    /// <summary>
    /// ウィンドウ端付近のポインター位置からリサイズ方向を判定する。
    /// </summary>
    private WindowEdge? DetectResizeEdge(Point pos)
    {
        if (WindowState != WindowState.Normal || !CanResize)
            return null;

        var w = Bounds.Width;
        var h = Bounds.Height;
        var left = pos.X < RESIZE_GRIP_SIZE;
        var right = pos.X > w - RESIZE_GRIP_SIZE;
        var top = pos.Y < RESIZE_GRIP_SIZE;
        var bottom = pos.Y > h - RESIZE_GRIP_SIZE;

        // 上端はトンネルイベントで先に処理することで、タイトルバー本体（CaptionHeight 内の
        // 下半分）の BeginMoveWindow とキャプションボタンの操作はそのまま維持される。
        if (top && left)
            return WindowEdge.NorthWest;
        if (top && right)
            return WindowEdge.NorthEast;
        if (top)
            return WindowEdge.North;
        if (bottom && left)
            return WindowEdge.SouthWest;
        if (bottom && right)
            return WindowEdge.SouthEast;
        if (bottom)
            return WindowEdge.South;
        if (left)
            return WindowEdge.West;
        if (right)
            return WindowEdge.East;

        return null;
    }

    /// <summary>
    /// ウィンドウ端でポインターが押された時にリサイズドラッグを開始する（トンネルイベント）。
    /// </summary>
    private void OnResizeGripPointerPressed(object sender, PointerPressedEventArgs e)
    {
        var edge = DetectResizeEdge(e.GetPosition(this));
        if (edge is not null)
        {
            BeginResizeDrag(edge.Value, e);
            e.Handled = true;
        }
    }

    /// <summary>
    /// ウィンドウ端付近でポインターが移動した時にリサイズカーソルを表示する（トンネルイベント）。
    /// </summary>
    private void OnResizeGripPointerMoved(object sender, PointerEventArgs e)
    {
        var edge = DetectResizeEdge(e.GetPosition(this));
        Cursor = edge switch
        {
            WindowEdge.NorthWest or WindowEdge.SouthEast => new Cursor(StandardCursorType.TopLeftCorner),
            WindowEdge.NorthEast or WindowEdge.SouthWest => new Cursor(StandardCursorType.TopRightCorner),
            WindowEdge.North or WindowEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
            WindowEdge.West or WindowEdge.East => new Cursor(StandardCursorType.SizeWestEast),
            _ => Cursor.Default,
        };
    }
}
