using Avalonia.Data.Converters;

namespace Komorebi.Converters;

/// <summary>
///     long値を他の型に変換するコンバータのコレクション。
///     XAMLバインディングで使用される。
/// </summary>
public static class LongConverters
{
    /// <summary>
    ///     バイト数を人間が読みやすいファイルサイズ文字列に変換するコンバータ。
    ///     B、KB、MB、GBの単位で自動的にフォーマットする。
    ///     例: 1536 → "1.50 KB (1,536)"
    /// </summary>
    public static readonly FuncValueConverter<long, string> ToFileSize = new(bytes =>
    {
        // 1KB未満はバイト単位で表示
        if (bytes < KB)
            return $"{bytes:N0} B";

        // 1MB未満はKB単位で表示（元のバイト数付き）
        if (bytes < MB)
            return $"{(bytes / KB):G3} KB ({bytes:N0})";

        // 1GB未満はMB単位で表示（元のバイト数付き）
        if (bytes < GB)
            return $"{(bytes / MB):G3} MB ({bytes:N0})";

        // 1GB以上はGB単位で表示（元のバイト数付き）
        return $"{(bytes / GB):G3} GB ({bytes:N0})";
    });

    /// <summary>
    ///     1キロバイトのバイト数定数。
    /// </summary>
    private const double KB = 1024;

    /// <summary>
    ///     1メガバイトのバイト数定数。
    /// </summary>
    private const double MB = 1024 * 1024;

    /// <summary>
    ///     1ギガバイトのバイト数定数。
    /// </summary>
    private const double GB = 1024 * 1024 * 1024;
}
