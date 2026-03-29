using System;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia;

namespace Komorebi.Views;

/// <summary>
/// View共通のヘルパーメソッド群。
/// </summary>
internal static class ViewHelpers
{
    /// <summary>
    /// SSH秘密鍵ファイルの選択ダイアログを開く。
    /// macOSでは隠しフォルダが表示されないため、初期ディレクトリを ~/.ssh に設定する。
    /// </summary>
    /// <returns>選択されたファイルのローカルパス。キャンセル時はnull。</returns>
    public static async Task<string> SelectSSHKeyFileAsync(Visual visual)
    {
        var toplevel = TopLevel.GetTopLevel(visual);
        if (toplevel is null)
            return null;

        var options = new FilePickerOpenOptions()
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("SSHKey") { Patterns = ["*"] }]
        };

        var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        if (Directory.Exists(sshDir))
            options.SuggestedStartLocation = await toplevel.StorageProvider.TryGetFolderFromPathAsync(sshDir);

        var selected = await toplevel.StorageProvider.OpenFilePickerAsync(options);
        return selected.Count == 1 ? selected[0].Path.LocalPath : null;
    }
}
