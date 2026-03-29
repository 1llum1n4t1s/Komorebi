namespace Komorebi.Models;

/// <summary>
/// コミットのブックマーク（色マーカー）を管理する静的クラス。
/// コミット履歴で特定のコミットに色ラベルを付けるために使用される。
/// </summary>
public static class Bookmarks
{
    /// <summary>
    /// ブックマーク色のブラシ配列。
    /// インデックス0はnull（ブックマークなし）、1以降は各色。
    /// </summary>
    public static readonly Avalonia.Media.IBrush[] Brushes = [
        null,
        Avalonia.Media.Brushes.Red,
        Avalonia.Media.Brushes.Orange,
        Avalonia.Media.Brushes.Gold,
        Avalonia.Media.Brushes.ForestGreen,
        Avalonia.Media.Brushes.DarkCyan,
        Avalonia.Media.Brushes.DeepSkyBlue,
        Avalonia.Media.Brushes.Purple,
    ];

    /// <summary>
    /// インデックスからブックマーク色のブラシを取得する。
    /// </summary>
    /// <param name="i">ブックマーク色のインデックス。</param>
    /// <returns>対応するブラシ。範囲外の場合はnull。</returns>
    public static Avalonia.Media.IBrush Get(int i)
    {
        // 範囲チェックし、有効な場合のみブラシを返す
        return (i >= 0 && i < Brushes.Length) ? Brushes[i] : null;
    }
}
