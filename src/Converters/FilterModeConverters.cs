using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Komorebi.Converters;

/// <summary>
/// フィルターモードを表示用の値に変換するコンバータのコレクション。
/// ブランチフィルタリング等のXAMLバインディングで使用される。
/// </summary>
public static class FilterModeConverters
{
    /// <summary>
    /// フィルターモードをボーダーブラシ色に変換するコンバータ。
    /// Included（含む） → 緑、Excluded（除外） → 赤、それ以外 → 透明を返す。
    /// </summary>
    public static readonly FuncValueConverter<Models.FilterMode, IBrush> ToBorderBrush =
        new FuncValueConverter<Models.FilterMode, IBrush>(v =>
        {
            return v switch
            {
                // 含むフィルターは緑色のボーダー
                Models.FilterMode.Included => Brushes.Green,
                // 除外フィルターは赤色のボーダー
                Models.FilterMode.Excluded => Brushes.Red,
                // フィルターなしは透明
                _ => Brushes.Transparent,
            };
        });
}
