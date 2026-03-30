using System.Collections.Generic;

using Avalonia.Media;

namespace Komorebi.Models;

/// <summary>
/// テーマのカスタマイズ設定を保持するクラス。
/// 基本色、グラフ描画の線幅、不透明度、グラフ色などをオーバーライドできる。
/// </summary>
public class ThemeOverrides
{
    /// <summary>
    /// 基本色のオーバーライドマップ（キー: 色名、値: Color）。
    /// </summary>
    public Dictionary<string, Color> BasicColors { get; set; } = [];

    /// <summary>
    /// コミットグラフの線の太さ（デフォルト: 2ピクセル）。
    /// </summary>
    public double GraphPenThickness { get; set; } = 2;

    /// <summary>
    /// マージされていないコミットの不透明度（デフォルト: 0.5）。
    /// </summary>
    public double OpacityForNotMergedCommits { get; set; } = 0.5;

    /// <summary>
    /// コミットグラフで使用される色のリスト。
    /// </summary>
    public List<Color> GraphColors { get; set; } = [];
}
