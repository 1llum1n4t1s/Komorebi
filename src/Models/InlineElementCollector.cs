using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// インライン要素のコレクション。コミットメッセージ内のリンクやSHA参照を管理する。
/// </summary>
public class InlineElementCollector
{
    /// <summary>格納されている要素数</summary>
    public int Count => _implementation.Count;

    /// <summary>指定インデックスの要素を取得する</summary>
    public InlineElement this[int index] => _implementation[index];

    /// <summary>
    /// 指定範囲と交差する最初の要素を返す
    /// </summary>
    /// <param name="start">検索範囲の開始位置</param>
    /// <param name="length">検索範囲の長さ</param>
    /// <returns>交差する要素。見つからない場合はnull</returns>
    public InlineElement Intersect(int start, int length)
    {
        foreach (var elem in _implementation)
        {
            if (elem.IsIntersecting(start, length))
                return elem;
        }

        return null;
    }

    /// <summary>
    /// 要素を追加する
    /// </summary>
    /// <param name="element">追加するインライン要素</param>
    public void Add(InlineElement element)
    {
        _implementation.Add(element);
    }

    /// <summary>
    /// 要素を開始位置の昇順でソートする
    /// </summary>
    public void Sort()
    {
        _implementation.Sort((l, r) => l.Start.CompareTo(r.Start));
    }

    /// <summary>
    /// すべての要素をクリアする
    /// </summary>
    public void Clear()
    {
        _implementation.Clear();
    }

    /// <summary>内部の要素リスト</summary>
    private readonly List<InlineElement> _implementation = [];
}
