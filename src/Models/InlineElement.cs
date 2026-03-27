namespace Komorebi.Models;

/// <summary>
///     コミットメッセージ内のインライン要素の種別
/// </summary>
public enum InlineElementType
{
    /// <summary>キーワード（Issue番号等）</summary>
    Keyword = 0,
    /// <summary>URLリンク</summary>
    Link,
    /// <summary>コミットSHAハッシュ</summary>
    CommitSHA,
    /// <summary>インラインコード（バッククォート囲み）</summary>
    Code,
}

/// <summary>
///     コミットメッセージ内のインライン要素（リンク、SHA参照など）を表すクラス
/// </summary>
public class InlineElement
{
    public InlineElementType Type { get; }
    public int Start { get; }
    public int Length { get; }
    public string Link { get; }

    public InlineElement(InlineElementType type, int start, int length, string link)
    {
        Type = type;
        Start = start;
        Length = length;
        Link = link;
    }

    /// <summary>
    ///     指定範囲がこの要素と重なっているかを判定する
    /// </summary>
    /// <param name="start">判定範囲の開始位置</param>
    /// <param name="length">判定範囲の長さ</param>
    /// <returns>重なっている場合はtrue</returns>
    public bool IsIntersecting(int start, int length)
    {
        if (start == Start)
            return true;

        if (start < Start)
            return start + length > Start;

        return start < Start + Length;
    }
}
