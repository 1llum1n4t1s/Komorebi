using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Komorebi.Views;

/// <summary>
///     アーカイブ作成ダイアログのコードビハインド。
/// </summary>
public partial class Archive : UserControl
{
    /// <summary>
    ///     コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public Archive()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     OutputFileの選択処理を行う。
    /// </summary>
    private async void SelectOutputFile(object _, RoutedEventArgs e)
    {
        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel is null)
            return;

        var options = new FilePickerSaveOptions()
        {
            DefaultExtension = ".zip",
            FileTypeChoices = [new FilePickerFileType("ZIP") { Patterns = ["*.zip"] }]
        };

        var selected = await toplevel.StorageProvider.SaveFilePickerAsync(options);
        if (selected is not null)
            TxtSaveFile.Text = selected.Path.LocalPath;

        e.Handled = true;
    }
}
