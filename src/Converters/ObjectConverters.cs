using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Komorebi.Converters;

/// <summary>
///     オブジェクトの型判定などを行うコンバータのコレクション。
///     XAMLバインディングで使用される。
/// </summary>
public static class ObjectConverters
{
    /// <summary>
    ///     オブジェクトが指定された型に割り当て可能かを判定するコンバータ。
    ///     パラメータにType型を受け取り、値がその型に代入可能であればtrueを返す。
    /// </summary>
    public class IsTypeOfConverter : IValueConverter
    {
        /// <summary>
        ///     値が指定された型に割り当て可能かを判定する。
        /// </summary>
        /// <param name="value">判定対象のオブジェクト。</param>
        /// <param name="targetType">ターゲット型（未使用）。</param>
        /// <param name="parameter">比較対象のType。</param>
        /// <param name="culture">カルチャ情報（未使用）。</param>
        /// <returns>割り当て可能な場合はtrue、それ以外はfalse。</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 値またはパラメータがnullの場合はfalse
            if (value is null || parameter is null)
                return false;

            // 値の型がパラメータの型に割り当て可能かチェック
            return value.GetType().IsAssignableTo((Type)parameter);
        }

        /// <summary>
        ///     逆変換は未実装。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new NotImplementedException();
        }
    }

    /// <summary>
    ///     IsTypeOfConverterのシングルトンインスタンス。
    ///     XAMLから直接参照して使用する。
    /// </summary>
    public static readonly IsTypeOfConverter IsTypeOf = new IsTypeOfConverter();
}
