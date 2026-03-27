using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
///     Git LFSロック管理ダイアログのコードビハインド。
/// </summary>
public partial class LFSLocks : ChromelessWindow
{
    /// <summary>
    ///     コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public LFSLocks()
    {
        CloseOnESC = true;
        InitializeComponent();
    }

    /// <summary>
    ///     UnlockButtonClickedイベントのハンドラ。
    /// </summary>
    private async void OnUnlockButtonClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.LFSLocks vm && sender is Button button)
            await vm.UnlockAsync(button.DataContext as Models.LFSLock, false);

        e.Handled = true;
    }

    /// <summary>
    ///     ForceUnlockButtonClickedイベントのハンドラ。
    /// </summary>
    private async void OnForceUnlockButtonClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.LFSLocks vm && sender is Button button)
            await vm.UnlockAsync(button.DataContext as Models.LFSLock, true);

        e.Handled = true;
    }

    /// <summary>
    ///     UnlockAllMyLocksButtonClickedイベントのハンドラ。
    /// </summary>
    private async void OnUnlockAllMyLocksButtonClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.LFSLocks vm)
            return;

        var shouldContinue = await App.AskConfirmAsync(App.Text("GitLFS.Locks.UnlockAllMyLocks.Confirm"));
        if (!shouldContinue)
            return;

        await vm.UnlockAllMyLocksAsync();
        e.Handled = true;
    }
}
