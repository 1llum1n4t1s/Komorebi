using System.Collections;
using System.Collections.Generic;

using Avalonia.Data.Converters;

namespace Komorebi.Converters;

/// <summary>
/// リスト・コレクションを他の型に変換するコンバータのコレクション。
/// XAMLバインディングで使用される。
/// </summary>
public static class ListConverters
{
    /// <summary>
    /// リストの要素数をカウント文字列に変換するコンバータ。
    /// 例: 5要素のリスト → "(5)"、null → "(0)"
    /// </summary>
    public static readonly FuncValueConverter<IList, string> ToCount =
        new FuncValueConverter<IList, string>(v => v is null ? "(0)" : $"({v.Count})");

    /// <summary>
    /// リストがnullまたは空であるかを判定するコンバータ。
    /// nullまたは要素数0の場合はtrue、それ以外はfalseを返す。
    /// </summary>
    public static readonly FuncValueConverter<IList, bool> IsNullOrEmpty =
        new FuncValueConverter<IList, bool>(v => v is null || v.Count == 0);

    /// <summary>
    /// リストがnullでなく空でもないかを判定するコンバータ。
    /// 要素が1つ以上ある場合はtrue、それ以外はfalseを返す。
    /// </summary>
    public static readonly FuncValueConverter<IList, bool> IsNotNullOrEmpty =
        new FuncValueConverter<IList, bool>(v => v is not null && v.Count > 0);

    /// <summary>
    /// 変更リストの先頭100件のみを取得するコンバータ。
    /// 100件以下の場合はそのまま返し、超過時は先頭100件を返す。
    /// パフォーマンス向上のためUI表示を制限する。
    /// </summary>
    public static readonly FuncValueConverter<List<Models.Change>, List<Models.Change>> Top100Changes =
        new FuncValueConverter<List<Models.Change>, List<Models.Change>>(v => (v is null || v.Count <= 100) ? v : v.GetRange(0, 100));

    /// <summary>
    /// リストが100件を超えているかを判定するコンバータ。
    /// 100件超過時にUI上で「先頭100件のみ表示」メッセージを出すために使用する。
    /// </summary>
    public static readonly FuncValueConverter<IList, bool> IsOnlyTop100Shows =
        new FuncValueConverter<IList, bool>(v => v is not null && v.Count > 100);
}
