using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Komorebi.Views;

/// <summary>
/// ウィンドウキャプションボタン（最小化・最大化・閉じる）のコードビハインド。
/// </summary>
public partial class CaptionButtons : UserControl
{
    /// <summary>閉じるボタンのみ表示するかのスタイルプロパティ。</summary>
    public static readonly StyledProperty<bool> IsCloseButtonOnlyProperty =
        AvaloniaProperty.Register<CaptionButtons, bool>(nameof(IsCloseButtonOnly));

    /// <summary>trueの場合、最小化・最大化ボタンを非表示にして閉じるボタンのみ表示する。</summary>
    public bool IsCloseButtonOnly
    {
        get => GetValue(IsCloseButtonOnlyProperty);
        set => SetValue(IsCloseButtonOnlyProperty, value);
    }

    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public CaptionButtons()
    {
        InitializeComponent();
    }

    /// <summary>
    /// MinimizeWindowの処理を行う。
    /// </summary>
    private void MinimizeWindow(object _, RoutedEventArgs e)
    {
        var window = this.FindAncestorOfType<Window>();
        if (window is not null)
            window.WindowState = WindowState.Minimized;

        e.Handled = true;
    }

    /// <summary>
    /// ウィンドウの最大化と通常サイズを切り替える。
    /// </summary>
    private void MaximizeOrRestoreWindow(object _, RoutedEventArgs e)
    {
        var window = this.FindAncestorOfType<Window>();
        if (window is not null)
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        e.Handled = true;
    }

    /// <summary>
    /// CloseWindowの処理を行う。
    /// </summary>
    private void CloseWindow(object _, RoutedEventArgs e)
    {
        var window = this.FindAncestorOfType<Window>();
        window?.Close();

        e.Handled = true;
    }
}
