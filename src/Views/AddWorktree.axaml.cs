using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Komorebi.Views;

/// <summary>
///     ワークツリー追加ダイアログのコードビハインド。
/// </summary>
public partial class AddWorktree : UserControl
{
    /// <summary>
    ///     コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public AddWorktree()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Locationの選択処理を行う。
    /// </summary>
    private async void SelectLocation(object _, RoutedEventArgs e)
    {
        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel is null)
            return;

        var options = new FolderPickerOpenOptions() { AllowMultiple = false };
        try
        {
            var selected = await toplevel.StorageProvider.OpenFolderPickerAsync(options);
            if (selected.Count == 1)
            {
                var folder = selected[0];
                var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder.Path.ToString();
                TxtLocation.Text = folderPath.TrimEnd('\\', '/');
            }
        }
        catch (Exception exception)
        {
            App.RaiseException(string.Empty, App.Text("Error.FailedToSelectLocation", exception.Message));
        }

        e.Handled = true;
    }
}
