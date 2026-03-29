using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Komorebi.Views;

/// <summary>
/// パッチ適用ダイアログのコードビハインド。
/// </summary>
public partial class Apply : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public Apply()
    {
        InitializeComponent();
    }

    /// <summary>
    /// PatchFileの選択処理を行う。
    /// </summary>
    private async void SelectPatchFile(object _, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        var options = new FilePickerOpenOptions()
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }]
        };

        var selected = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        if (selected.Count == 1)
            TxtPatchFile.Text = selected[0].Path.LocalPath;

        e.Handled = true;
    }
}
