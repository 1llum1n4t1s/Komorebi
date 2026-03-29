using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Komorebi.Views;

/// <summary>
/// リポジトリクローンダイアログのコードビハインド。
/// </summary>
public partial class Clone : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public Clone()
    {
        InitializeComponent();
    }

    /// <summary>
    /// ParentFolderの選択処理を行う。
    /// </summary>
    private async void SelectParentFolder(object _, RoutedEventArgs e)
    {
        var options = new FolderPickerOpenOptions() { AllowMultiple = false };
        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel is null)
            return;

        try
        {
            var selected = await toplevel.StorageProvider.OpenFolderPickerAsync(options);
            if (selected.Count == 1)
            {
                var folder = selected[0];
                var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                TxtParentFolder.Text = folderPath;
            }
        }
        catch (Exception exception)
        {
            App.RaiseException(string.Empty, App.Text("Error.FailedToSelectFolder", exception.Message));
        }

        e.Handled = true;
    }

    /// <summary>
    /// SSHKeyの選択処理を行う。
    /// </summary>
    private async void SelectSSHKey(object _, RoutedEventArgs e)
    {
        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel is null)
            return;

        var options = new FilePickerOpenOptions()
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("SSHKey") { Patterns = ["*.*"] }]
        };

        var selected = await toplevel.StorageProvider.OpenFilePickerAsync(options);
        if (selected.Count == 1)
            TxtSshKey.Text = selected[0].Path.LocalPath;

        e.Handled = true;
    }
}
