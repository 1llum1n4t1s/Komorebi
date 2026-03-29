using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// リモート設定編集ダイアログのコードビハインド。
/// </summary>
public partial class EditRemote : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public EditRemote()
    {
        InitializeComponent();
    }

    /// <summary>
    /// SSHKeyの選択処理を行う。
    /// </summary>
    private async void SelectSSHKey(object _, RoutedEventArgs e)
    {
        var path = await ViewHelpers.SelectSSHKeyFileAsync(this);
        if (path is not null)
            TxtSshKey.Text = path;

        e.Handled = true;
    }
}
