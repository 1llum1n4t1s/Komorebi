using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Komorebi.Views;

/// <summary>
///     リモート設定編集ダイアログのコードビハインド。
/// </summary>
public partial class EditRemote : UserControl
{
    /// <summary>
    ///     コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public EditRemote()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     SSHKeyの選択処理を行う。
    /// </summary>
    private async void SelectSSHKey(object _, RoutedEventArgs e)
    {
        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel is null)
            return;

        var options = new FilePickerOpenOptions() { AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("SSHKey") { Patterns = ["*.*"] }] };
        var selected = await toplevel.StorageProvider.OpenFilePickerAsync(options);
        if (selected.Count == 1)
            TxtSshKey.Text = selected[0].Path.LocalPath;

        e.Handled = true;
    }
}
