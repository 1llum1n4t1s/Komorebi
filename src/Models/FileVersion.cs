namespace Komorebi.Models;

/// <summary>
/// ファイルの特定バージョン（コミット）の情報を保持するクラス。
/// ファイル履歴の各エントリとして使用される。
/// </summary>
public class FileVersion
{
    /// <summary>コミットのSHAハッシュ</summary>
    public string SHA { get; set; } = string.Empty;
    /// <summary>親コミットが存在するかどうか（初回コミットの場合はfalse）</summary>
    public bool HasParent { get; set; } = false;
    /// <summary>コミットの著者情報</summary>
    public User Author { get; set; } = User.Invalid;
    /// <summary>著者のコミット日時（Unixタイムスタンプ）</summary>
    public ulong AuthorTime { get; set; } = 0;
    /// <summary>コミットメッセージの件名行</summary>
    public string Subject { get; set; } = string.Empty;
    /// <summary>このバージョンでのファイル変更情報</summary>
    public Change Change { get; set; } = new();
    /// <summary>ファイルの現在のパス</summary>
    public string Path => Change.Path;
    /// <summary>ファイルのリネーム前のパス（リネームがない場合は空）</summary>
    public string OriginalPath => Change.OriginalPath;
}
