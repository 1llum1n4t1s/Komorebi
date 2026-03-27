using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
///     assume-unchanged管理ダイアログのコードビハインド。
/// </summary>
public partial class AssumeUnchangedManager : ChromelessWindow
{
    /// <summary>
    ///     コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public AssumeUnchangedManager()
    {
        CloseOnESC = true;
        InitializeComponent();
    }

    /// <summary>
    ///     RemoveButtonClickedイベントのハンドラ。
    /// </summary>
    private async void OnRemoveButtonClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.AssumeUnchangedManager vm && sender is Button button)
            await vm.RemoveAsync(button.DataContext as string);

        e.Handled = true;
    }
}
