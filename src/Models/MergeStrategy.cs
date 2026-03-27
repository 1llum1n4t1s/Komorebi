using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
///     git mergeコマンドのマージ戦略を表すクラス。
///     複数ブランチのマージ時に使用するアルゴリズムを定義する。
/// </summary>
public class MergeStrategy
{
    /// <summary>
    ///     戦略の表示名。
    /// </summary>
    public string Name { get; internal set; }

    /// <summary>
    ///     戦略の説明文。
    /// </summary>
    public string Desc { get; internal set; }

    /// <summary>
    ///     gitコマンドに渡す引数文字列。
    /// </summary>
    public string Arg { get; internal set; }

    /// <summary>
    ///     複数ブランチマージ用の戦略リスト。
    /// </summary>
    public static List<MergeStrategy> ForMultiple { get; private set; } = [
        new MergeStrategy("Default", "Let Git automatically select a strategy", string.Empty),
        new MergeStrategy("Octopus", "Attempt merging multiple heads", "octopus"),
        new MergeStrategy("Ours", "Record the merge without modifying the tree", "ours"),
    ];

    /// <summary>
    ///     MergeStrategyの新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="n">戦略の表示名。</param>
    /// <param name="d">戦略の説明文。</param>
    /// <param name="a">gitコマンド引数。</param>
    public MergeStrategy(string n, string d, string a)
    {
        Name = n;
        Desc = d;
        Arg = a;
    }
}
