using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// 読み込み中アニメーションアイコンのコードビハインド。
/// </summary>
public partial class LoadingIcon : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public LoadingIcon()
    {
        IsHitTestVisible = false;
        InitializeComponent();
    }

    /// <summary>
    /// コントロールが読み込まれた際の処理。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (IsVisible)
            StartAnim();
    }

    /// <summary>
    /// コントロールがアンロードされた際の処理。
    /// </summary>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        StopAnim();
        base.OnUnloaded(e);
    }

    /// <summary>
    /// プロパティが変更された際の処理。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty)
        {
            if (IsVisible)
                StartAnim();
            else
                StopAnim();
        }
    }

    /// <summary>
    /// StartAnimの処理を行う。
    /// </summary>
    private void StartAnim()
    {
        Content = new Path() { Classes = { "rotating" } };
    }

    /// <summary>
    /// StopAnimの処理を行う。
    /// </summary>
    private void StopAnim()
    {
        if (Content is Path path)
            path.Classes.Clear();

        Content = null;
    }
}
