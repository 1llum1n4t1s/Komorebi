using System.IO;
using Avalonia.Data.Converters;

namespace Komorebi.Converters;

/// <summary>
///     ファイルパス文字列を変換するコンバータのコレクション。
///     XAMLバインディングで使用される。
/// </summary>
public static class PathConverters
{
    /// <summary>
    ///     フルパスからファイル名のみを抽出するコンバータ。
    ///     例: "C:/repos/project/file.txt" → "file.txt"
    /// </summary>
    public static readonly FuncValueConverter<string, string> PureFileName =
        new(v => Path.GetFileName(v) ?? "");

    /// <summary>
    ///     フルパスからディレクトリ名のみを抽出するコンバータ。
    ///     例: "C:/repos/project/file.txt" → "C:/repos/project"
    /// </summary>
    public static readonly FuncValueConverter<string, string> PureDirectoryName =
        new(v => Path.GetDirectoryName(v) ?? "");

    /// <summary>
    ///     フルパスをホームディレクトリからの相対パスに変換するコンバータ。
    ///     プラットフォーム固有の実装を使用する。
    /// </summary>
    public static readonly FuncValueConverter<string, string> RelativeToHome =
        new(Native.OS.GetRelativePathToHome);
}
