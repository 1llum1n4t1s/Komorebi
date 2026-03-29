namespace Komorebi.Models;

/// <summary>
/// タグのソート順モード
/// </summary>
public enum TagSortMode
{
    /// <summary>作成日時順</summary>
    CreatorDate = 0,
    /// <summary>名前順</summary>
    Name,
}

/// <summary>
/// Gitタグの情報を保持するクラス
/// </summary>
public class Tag
{
    /// <summary>タグ名</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>注釈付きタグかどうか</summary>
    public bool IsAnnotated { get; set; } = false;
    /// <summary>タグが指すオブジェクトのSHAハッシュ</summary>
    public string SHA { get; set; } = string.Empty;
    /// <summary>タグの作成者</summary>
    public User Creator { get; set; } = null;
    /// <summary>タグの作成日時（UNIXタイムスタンプ）</summary>
    public ulong CreatorDate { get; set; } = 0;
    /// <summary>タグのメッセージ（注釈付きタグの場合）</summary>
    public string Message { get; set; } = string.Empty;
}
