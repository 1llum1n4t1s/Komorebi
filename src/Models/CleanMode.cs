namespace Komorebi.Models;

/// <summary>
///     git cleanコマンドのクリーンモードを表す列挙型。
/// </summary>
public enum CleanMode
{
    /// <summary>
    ///     未追跡ファイルのみをクリーンする。
    /// </summary>
    OnlyUntrackedFiles = 0,

    /// <summary>
    ///     .gitignoreで無視されたファイルのみをクリーンする。
    /// </summary>
    OnlyIgnoredFiles,

    /// <summary>
    ///     未追跡ファイルと無視されたファイルの両方をクリーンする。
    /// </summary>
    All,
}
