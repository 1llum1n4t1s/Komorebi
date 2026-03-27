using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Komorebi.Views;

/// <summary>
///     プッシュダイアログのコードビハインド。
/// </summary>
public partial class Push : UserControl
{
    /// <summary>
    ///     コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public Push()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     PushToNewBranchイベントのハンドラ。
    /// </summary>
    private async void OnPushToNewBranch(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.Push push)
            return;

        var launcher = this.FindAncestorOfType<Launcher>();
        if (launcher is null)
            return;

        var dialog = new PushToNewBranch();
        dialog.SetRemote(push.SelectedRemote.Name);

        var name = await dialog.ShowDialog<string>(launcher);
        if (!string.IsNullOrEmpty(name))
            push.PushToNewBranch(name);
    }
}
