using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Komorebi.Views;

/// <summary>
///     カスタムタイトルバーを持つクロムレスウィンドウの基底クラス。ESCで閉じる機能やズーム操作に対応。
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

    /// <summary>
    ///     コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public ChromelessWindow()
    {
        // macOSのキャプションボタン幅をズーム倍率に応じて調整する
        LeftCaptionButtonWidth = 72.0 / Math.Max(1.0, ViewModels.Preferences.Instance.Zoom);
        Focusable = true;
        // プラットフォーム固有のウィンドウ設定を適用する
        Native.OS.SetupForWindow(this);
    }

    /// <summary>
    ///     ウィンドウのドラッグ移動を開始する。シングルクリック時のみ有効。
    /// </summary>
    public void BeginMoveWindow(object _, PointerPressedEventArgs e)
    {
        // シングルクリックの場合のみドラッグ移動を開始する（ダブルクリックは最大化用）
        if (e.ClickCount == 1)
            BeginMoveDrag(e);

        e.Handled = true;
    }

    /// <summary>
    ///     ウィンドウの最大化と通常サイズを切り替える。
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
    ///     テンプレート適用時の処理を行う。
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
    ///     プロパティが変更された際の処理。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Windowsでウィンドウ状態が変わった場合、最大化時のパディングを調整する
        if (change.Property == WindowStateProperty && OperatingSystem.IsWindows())
        {
            if (WindowState == WindowState.Maximized)
            {
                // 最大化時はボーダーを消してパディングで補正する
                BorderThickness = new Thickness(0);
                Padding = new Thickness(8, 6, 8, 8);
            }
            else
            {
                // 通常時はボーダーを表示してパディングをリセットする
                BorderThickness = new Thickness(1);
                Padding = new Thickness(0);
            }
        }
    }

    /// <summary>
    ///     キーが押された際のイベント処理。
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
    ///     ウィンドウボーダーのポインター押下時にリサイズドラッグを開始する。
    /// </summary>
    private void OnWindowBorderPointerPressed(object sender, PointerPressedEventArgs e)
    {
        // ボーダーのTagにセットされたWindowEdge方向でリサイズを開始する
        if (sender is Border { Tag: WindowEdge edge } && CanResize)
            BeginResizeDrag(edge, e);
    }
}
