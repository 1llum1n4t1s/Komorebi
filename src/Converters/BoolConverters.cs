using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Komorebi.Converters;

/// <summary>
/// ブール値を他の型に変換するコンバータのコレクション。
/// XAMLバインディングで使用される。
/// </summary>
public static class BoolConverters
{
    /// <summary>
    /// ブール値をフォントウェイトに変換するコンバータ。
    /// trueの場合はBold、falseの場合はRegularを返す。
    /// </summary>
    public static readonly FuncValueConverter<bool, FontWeight> IsBoldToFontWeight =
        new(x => x ? FontWeight.Bold : FontWeight.Regular);

    /// <summary>
    /// ブール値を不透明度に変換するコンバータ。
    /// マージ済み（true）の場合は1.0（完全不透明）、未マージ（false）の場合は0.65（半透明）を返す。
    /// </summary>
    public static readonly FuncValueConverter<bool, double> IsMergedToOpacity =
        new(x => x ? 1 : 0.65);

    /// <summary>
    /// ブール値をブラシに変換するコンバータ。
    /// 警告状態（true）の場合はDarkGoldenrod色、通常（false）の場合はテーマの前景色ブラシを返す。
    /// </summary>
    public static readonly FuncValueConverter<bool, IBrush> IsWarningToBrush =
        new(x => x ? Brushes.DarkGoldenrod : Application.Current?.FindResource("Brush.FG1") as IBrush);
}
