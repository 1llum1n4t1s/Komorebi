using Avalonia;
using Avalonia.Data.Converters;

namespace Komorebi.Converters;

/// <summary>
/// double値を他の型に変換するコンバータのコレクション。
/// XAMLバインディングで使用される。
/// </summary>
public static class DoubleConverters
{
    /// <summary>
    /// double値を1.0増加させるコンバータ。
    /// </summary>
    public static readonly FuncValueConverter<double, double> Increase =
        new FuncValueConverter<double, double>(v => v + 1.0);

    /// <summary>
    /// double値を1.0減少させるコンバータ。
    /// </summary>
    public static readonly FuncValueConverter<double, double> Decrease =
        new FuncValueConverter<double, double>(v => v - 1.0);

    /// <summary>
    /// double値（0.0～1.0）をパーセンテージ文字列に変換するコンバータ。
    /// 例: 0.75 → "75%"
    /// </summary>
    public static readonly FuncValueConverter<double, string> ToPercentage =
        new FuncValueConverter<double, string>(v => (v * 100).ToString("F0") + "%");

    /// <summary>
    /// double値を(1-値)のパーセンテージ文字列に変換するコンバータ。
    /// 例: 0.75 → "25%"（補数のパーセンテージ）
    /// </summary>
    public static readonly FuncValueConverter<double, string> OneMinusToPercentage =
        new FuncValueConverter<double, string>(v => ((1.0 - v) * 100).ToString("F0") + "%");

    /// <summary>
    /// double値を左マージンのThicknessに変換するコンバータ。
    /// 指定値を左マージンとし、他のマージンは0にする。
    /// </summary>
    public static readonly FuncValueConverter<double, Thickness> ToLeftMargin =
        new FuncValueConverter<double, Thickness>(v => new Thickness(v, 0, 0, 0));
}
