namespace Komorebi.Models;

/// <summary>
/// アプリケーションで必要なGitバージョンの定数を管理する静的クラス
/// </summary>
public static class GitVersions
{
    /// <summary>
    /// このアプリが必要とするGitの最低バージョン
    /// </summary>
    public static readonly System.Version MINIMAL = new(2, 25, 1);

    /// <summary>
    /// `stash push`コマンドで`--pathspec-from-file`オプションをサポートする最低バージョン
    /// </summary>
    public static readonly System.Version STASH_PUSH_WITH_PATHSPECFILE = new(2, 26, 0);

    /// <summary>
    /// `stash push`コマンドで`--staged`オプションをサポートする最低バージョン
    /// </summary>
    public static readonly System.Version STASH_PUSH_ONLY_STAGED = new(2, 35, 0);
}
