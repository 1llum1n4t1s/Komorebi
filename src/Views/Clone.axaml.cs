using System;
using System.IO;

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
            var prefilled = TxtParentFolder.Text;
            if (!string.IsNullOrWhiteSpace(prefilled) && Directory.Exists(prefilled))
                options.SuggestedStartLocation = await toplevel.StorageProvider.TryGetFolderFromPathAsync(prefilled);

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

}
