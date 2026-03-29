using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// ポップアップ操作実行中ステータス表示のコードビハインド。
/// </summary>
public partial class PopupRunningStatus : UserControl
{
    /// <summary>
    /// 実行中の操作説明テキストのスタイルプロパティ。
    /// </summary>
    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<PopupRunningStatus, string>(nameof(Description));

    /// <summary>
    /// 実行中の操作説明テキストを取得・設定する。
    /// </summary>
    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public PopupRunningStatus()
    {
        InitializeComponent();
    }

    /// <summary>
    /// コントロールが読み込まれた際の処理。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _isUnloading = false;
        if (IsVisible)
            StartAnim();
    }

    /// <summary>
    /// コントロールがアンロードされた際の処理。
    /// </summary>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _isUnloading = true;
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
            if (IsVisible && !_isUnloading)
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
        Icon.Content = new Path() { Classes = { "waiting" } };
        ProgressBar.IsIndeterminate = true;
    }

    /// <summary>
    /// StopAnimの処理を行う。
    /// </summary>
    private void StopAnim()
    {
        if (Icon.Content is Path path)
            path.Classes.Clear();
        Icon.Content = null;
        ProgressBar.IsIndeterminate = false;
    }

    /// <summary>
    /// アンロード中かどうか。アンロード時のアニメーション開始を防止する。
    /// </summary>
    private bool _isUnloading = false;
}
