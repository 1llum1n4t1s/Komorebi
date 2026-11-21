using Avalonia.Data.Converters;

namespace Komorebi.Converters;

/// <summary>
/// long値を他の型に変換するコンバータのコレクション。
/// XAMLバインディングで使用される。
/// </summary>
public static class LongConverters
{
    /// <summary>
    /// バイト数を人間が読みやすいファイルサイズ文字列に変換するコンバータ。
    /// B、KB、MB、GBの単位で自動的にフォーマットする。
    /// 例: 1536 → "1.5 KB (1,536)"
    /// </summary>
    /// <remarks>
    /// 上流との差分: 書式 G3 は 999.5 以上で指数表記（"1E+03 KB"）になるため、
    /// 丸めて 999.5 以上になる値は次の単位へ繰り上げ、最大単位（GB）では整数表記にして指数表記を避ける。
    /// </remarks>
    public static readonly FuncValueConverter<long, string> ToFileSize = new(bytes =>
    {
        // 1KB未満はバイト単位で表示
        if (bytes < KB)
            return $"{bytes:N0} B";

        // KB単位で表示（元のバイト数付き）。G3 で 999.5 以上は指数表記になるので次の単位へ回す
        var kb = bytes / KB;
        if (kb < ExponentLimit)
            return $"{kb:G3} KB ({bytes:N0})";

        // MB単位で表示（元のバイト数付き）
        var mb = bytes / MB;
        if (mb < ExponentLimit)
            return $"{mb:G3} MB ({bytes:N0})";

        // GB単位で表示（元のバイト数付き）。1000GB以上は指数表記を避けて整数表記にする
        var gb = bytes / GB;
        return gb < ExponentLimit ? $"{gb:G3} GB ({bytes:N0})" : $"{gb:N0} GB ({bytes:N0})";
    });

    /// <summary>
    /// 書式 G3 が指数表記に切り替わる境界（G3 で丸めると 1000 になる最小の値）。
    /// </summary>
    private const double ExponentLimit = 999.5;

    /// <summary>
    /// 1キロバイトのバイト数定数。
    /// </summary>
    private const double KB = 1024;

    /// <summary>
    /// 1メガバイトのバイト数定数。
    /// </summary>
    private const double MB = 1024 * 1024;

    /// <summary>
    /// 1ギガバイトのバイト数定数。
    /// </summary>
    private const double GB = 1024 * 1024 * 1024;
}
