using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Komorebi.Views;

/// <summary>
/// 差分表示ビュー（テキスト/画像の差分切替）のコードビハインド。
/// </summary>
public partial class DiffView : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public DiffView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// コントロールが読み込まれた際の処理。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is ViewModels.DiffContext vm)
            vm.CheckSettings();
    }

    /// <summary>
    /// GotoFirstChangeイベントのハンドラ。
    /// </summary>
    private void OnGotoFirstChange(object _, RoutedEventArgs e)
    {
        this.FindDescendantOfType<ThemedTextDiffPresenter>()?.GotoChange(ViewModels.BlockNavigationDirection.First);
        e.Handled = true;
    }

    /// <summary>
    /// GotoPrevChangeイベントのハンドラ。
    /// </summary>
    private void OnGotoPrevChange(object _, RoutedEventArgs e)
    {
        this.FindDescendantOfType<ThemedTextDiffPresenter>()?.GotoChange(ViewModels.BlockNavigationDirection.Prev);
        e.Handled = true;
    }

    /// <summary>
    /// GotoNextChangeイベントのハンドラ。
    /// </summary>
    private void OnGotoNextChange(object _, RoutedEventArgs e)
    {
        this.FindDescendantOfType<ThemedTextDiffPresenter>()?.GotoChange(ViewModels.BlockNavigationDirection.Next);
        e.Handled = true;
    }

    /// <summary>
    /// GotoLastChangeイベントのハンドラ。
    /// </summary>
    private void OnGotoLastChange(object _, RoutedEventArgs e)
    {
        this.FindDescendantOfType<ThemedTextDiffPresenter>()?.GotoChange(ViewModels.BlockNavigationDirection.Last);
        e.Handled = true;
    }

    /// <summary>
    /// サブモジュールのリビジョン比較ダイアログを親ウィンドウ上に開く。
    /// </summary>
    private void OnOpenSubmoduleRevisionCompare(object sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null)
            return;

        if (sender is Button { DataContext: Models.SubmoduleDiff diff } && diff.CanOpenDetails)
        {
            var vm = new ViewModels.SubmoduleRevisionCompare(diff);
            var dialog = new SubmoduleRevisionCompare() { DataContext = vm };
            dialog.Show(owner);
        }
    }
}
