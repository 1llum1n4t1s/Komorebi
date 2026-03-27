using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Komorebi.Converters;

/// <summary>
///     整数値を他の型に変換するコンバータのコレクション。
///     XAMLバインディングで使用される。
/// </summary>
public static class IntConverters
{
    /// <summary>
    ///     整数値が0より大きいかを判定するコンバータ。
    ///     0より大きい場合はtrue、それ以外はfalseを返す。
    /// </summary>
    public static readonly FuncValueConverter<int, bool> IsGreaterThanZero =
        new(v => v > 0);

    /// <summary>
    ///     整数値が4より大きいかを判定するコンバータ。
    ///     4より大きい場合はtrue、それ以外はfalseを返す。
    /// </summary>
    public static readonly FuncValueConverter<int, bool> IsGreaterThanFour =
        new(v => v > 4);

    /// <summary>
    ///     整数値が0であるかを判定するコンバータ。
    ///     0の場合はtrue、それ以外はfalseを返す。
    /// </summary>
    public static readonly FuncValueConverter<int, bool> IsZero =
        new(v => v == 0);

    /// <summary>
    ///     整数値が1でないかを判定するコンバータ。
    ///     1でない場合はtrue、1の場合はfalseを返す。
    /// </summary>
    public static readonly FuncValueConverter<int, bool> IsNotOne =
        new(v => v != 1);

    /// <summary>
    ///     整数値をツリービューのインデントマージンに変換するコンバータ。
    ///     各レベルに16ピクセルの左マージンを設定する。
    /// </summary>
    public static readonly FuncValueConverter<int, Thickness> ToTreeMargin =
        new(v => new Thickness(v * 16, 0, 0, 0));

    /// <summary>
    ///     ブックマークインデックスをブラシ色に変換するコンバータ。
    ///     対応するブックマーク色が見つからない場合はテーマの前景色を返す。
    /// </summary>
    public static readonly FuncValueConverter<int, IBrush> ToBookmarkBrush =
        new(v => Models.Bookmarks.Get(v) ?? Application.Current?.FindResource("Brush.FG1") as IBrush);

    /// <summary>
    ///     未解決のコンフリクト数をローカライズされた説明文字列に変換するコンバータ。
    ///     0の場合は「全て解決済み」、それ以外は残りのコンフリクト数を表示する。
    /// </summary>
    public static readonly FuncValueConverter<int, string> ToUnsolvedDesc =
        new(v => v == 0 ? App.Text("MergeConflictEditor.AllResolved") : App.Text("MergeConflictEditor.ConflictsRemaining", v));
}
