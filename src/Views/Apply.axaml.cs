using System;
using System.Diagnostics.CodeAnalysis;

using Avalonia.Controls;
using Avalonia.Input;
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
            FileTypeFilter = [new FilePickerFileType("Patch File") { Patterns = ["*.patch", "*.diff"] }]
        };

        var selected = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        if (selected.Count == 1)
            TxtPatchFile.Text = selected[0].Path.LocalPath;

        e.Handled = true;
    }

    /// <summary>
    /// パッチファイル欄へのドラッグ中に、受け付けられるファイルかどうかを示す。
    /// upstream e112b3d9: .patch / .diff の単一ファイルだけを受け付ける。
    /// </summary>
    private void DragOverPatchFileBox(object sender, DragEventArgs e)
    {
        e.DragEffects = TryGetDroppedPatchFile(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// パッチファイル欄へドロップされたファイルのパスを入力する。
    /// </summary>
    private void DropOnPatchFileBox(object sender, DragEventArgs e)
    {
        if (!TryGetDroppedPatchFile(e, out var file))
            return;

        TxtPatchFile.Text = file;
        e.Handled = true;
    }

    /// <summary>
    /// ドラッグデータがローカルの .patch / .diff 単一ファイルならそのパスを返す。
    /// 拡張子は大文字小文字を区別しない（Windows では .PATCH なども一般的なため。上流は区別する）。
    /// </summary>
    private static bool TryGetDroppedPatchFile(DragEventArgs e, [NotNullWhen(true)] out string? file)
    {
        file = null;
        if (!e.DataTransfer.Contains(DataFormat.File))
            return false;

        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: 1 } || !files[0].Path.IsFile)
            return false;

        var path = files[0].Path.LocalPath;
        if (!path.EndsWith(".patch", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith(".diff", StringComparison.OrdinalIgnoreCase))
            return false;

        file = path;
        return true;
    }
}
