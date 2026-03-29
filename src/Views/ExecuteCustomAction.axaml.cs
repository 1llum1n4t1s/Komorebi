using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Komorebi.Views;

/// <summary>
/// カスタムアクション実行ダイアログのコードビハインド。
/// </summary>
public partial class ExecuteCustomAction : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public ExecuteCustomAction()
    {
        InitializeComponent();
    }

    /// <summary>
    /// パス選択ボタンのクリックイベントハンドラ。フォルダまたはファイルの選択ダイアログを表示する。
    /// </summary>
    private async void SelectPath(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        var control = sender as Control;

        if (control?.DataContext is not ViewModels.CustomActionControlPathSelector selector)
            return;

        if (selector.IsFolder)
        {
            try
            {
                var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                {
                    var folder = selected[0];
                    var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                    selector.Path = folderPath;
                }
            }
            catch (Exception exception)
            {
                App.RaiseException(string.Empty, App.Text("Error.FailedToSelectFolder", exception.Message));
            }
        }
        else
        {
            var options = new FilePickerOpenOptions()
            {
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("File") { Patterns = ["*.*"] }]
            };

            var selected = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
                selector.Path = selected[0].Path.LocalPath;
        }

        e.Handled = true;
    }
}
