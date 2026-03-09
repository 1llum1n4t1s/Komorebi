using System;
using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Styling;

namespace Komorebi.Converters
{
    /// <summary>
    ///     文字列を他の型に変換するコンバータのコレクション。
    ///     XAMLバインディングで使用される。
    /// </summary>
    public static class StringConverters
    {
        /// <summary>
        ///     ロケールキー文字列をLocaleオブジェクトに変換するコンバータ。
        ///     双方向変換をサポートし、設定画面の言語ドロップダウンで使用される。
        /// </summary>
        public class ToLocaleConverter : IValueConverter
        {
            /// <summary>
            ///     ロケールキー文字列からLocaleオブジェクトを検索して返す。
            /// </summary>
            /// <param name="value">ロケールキー文字列（例: "ja_JP"）。</param>
            /// <param name="targetType">ターゲット型（未使用）。</param>
            /// <param name="parameter">パラメータ（未使用）。</param>
            /// <param name="culture">カルチャ情報（未使用）。</param>
            /// <returns>対応するLocaleオブジェクト。</returns>
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                // サポート済みロケールリストからキーが一致するものを検索
                return Models.Locale.Supported.Find(x => x.Key == value as string);
            }

            /// <summary>
            ///     Localeオブジェクトからロケールキー文字列に逆変換する。
            /// </summary>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return (value as Models.Locale)?.Key;
            }
        }

        /// <summary>
        ///     ToLocaleConverterのシングルトンインスタンス。
        /// </summary>
        public static readonly ToLocaleConverter ToLocale = new ToLocaleConverter();

        /// <summary>
        ///     テーマ名文字列をThemeVariantに変換するコンバータ。
        ///     双方向変換をサポートし、設定画面のテーマドロップダウンで使用される。
        /// </summary>
        public class ToThemeConverter : IValueConverter
        {
            /// <summary>
            ///     テーマ名文字列からThemeVariantオブジェクトに変換する。
            /// </summary>
            /// <param name="value">テーマ名文字列（例: "Dark", "Light"）。</param>
            /// <param name="targetType">ターゲット型（未使用）。</param>
            /// <param name="parameter">パラメータ（未使用）。</param>
            /// <param name="culture">カルチャ情報（未使用）。</param>
            /// <returns>対応するThemeVariantオブジェクト。</returns>
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return App.ParseThemeVariant(value as string);
            }

            /// <summary>
            ///     ThemeVariantからテーマ名文字列に逆変換する。
            /// </summary>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return (value as ThemeVariant)?.Key;
            }
        }

        /// <summary>
        ///     ToThemeConverterのシングルトンインスタンス。
        /// </summary>
        public static readonly ToThemeConverter ToTheme = new ToThemeConverter();

        /// <summary>
        ///     リソースキーを使用してフォーマットされた文字列を生成するコンバータ。
        ///     パラメータにリソースキーを指定し、値をフォーマット引数として使用する。
        /// </summary>
        public class FormatByResourceKeyConverter : IValueConverter
        {
            /// <summary>
            ///     リソースキーと値を組み合わせてフォーマット済み文字列を生成する。
            /// </summary>
            /// <param name="value">フォーマット引数となる値。</param>
            /// <param name="targetType">ターゲット型（未使用）。</param>
            /// <param name="parameter">リソースキー文字列。</param>
            /// <param name="culture">カルチャ情報（未使用）。</param>
            /// <returns>フォーマット済みのローカライズされた文字列。</returns>
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                // パラメータからリソースキーを取得し、値でフォーマット
                var key = parameter as string;
                return App.Text(key, value);
            }

            /// <summary>
            ///     逆変換は未実装。
            /// </summary>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        ///     FormatByResourceKeyConverterのシングルトンインスタンス。
        /// </summary>
        public static readonly FormatByResourceKeyConverter FormatByResourceKey = new FormatByResourceKeyConverter();

        /// <summary>
        ///     SHAハッシュ文字列を短縮形（先頭10文字）に変換するコンバータ。
        ///     例: "abc123def456..." → "abc123def4"
        /// </summary>
        public static readonly FuncValueConverter<string, string> ToShortSHA =
            new FuncValueConverter<string, string>(v => v == null ? string.Empty : (v.Length > 10 ? v.Substring(0, 10) : v));

        /// <summary>
        ///     refs/プレフィックスを除去してブランチ名のみを取得するコンバータ。
        ///     "refs/heads/" → ローカルブランチ名、"refs/remotes/" → リモートブランチ名に変換する。
        /// </summary>
        public static readonly FuncValueConverter<string, string> TrimRefsPrefix =
            new FuncValueConverter<string, string>(v =>
            {
                if (v == null)
                    return string.Empty;

                // ローカルブランチのプレフィックスを除去
                if (v.StartsWith("refs/heads/", StringComparison.Ordinal))
                    return v.Substring(11);

                // リモートブランチのプレフィックスを除去
                if (v.StartsWith("refs/remotes/", StringComparison.Ordinal))
                    return v.Substring(13);

                return v;
            });

        /// <summary>
        ///     文字列にスペースが含まれているかを判定するコンバータ。
        ///     スペースを含む場合はtrue、それ以外はfalseを返す。
        /// </summary>
        public static readonly FuncValueConverter<string, bool> ContainsSpaces =
            new FuncValueConverter<string, bool>(v => v != null && v.Contains(' '));

        /// <summary>
        ///     文字列がnullでなく空白のみでもないかを判定するコンバータ。
        ///     有効な文字が含まれている場合はtrueを返す。
        /// </summary>
        public static readonly FuncValueConverter<string, bool> IsNotNullOrWhitespace =
            new FuncValueConverter<string, bool>(v => v != null && v.Trim().Length > 0);

        /// <summary>
        ///     上流ブランチ参照を親しみやすい表示名に変換するコンバータ。
        ///     "refs/remotes/"プレフィックス（13文字）を除去してリモートブランチ名のみを返す。
        /// </summary>
        public static readonly FuncValueConverter<string, string> ToFriendlyUpstream =
            new FuncValueConverter<string, string>(v => v is { Length: > 13 } ? v.Substring(13) : string.Empty);

        /// <summary>
        ///     KeyGestureオブジェクトをプラットフォーム表記の文字列に変換するコンバータ。
        ///     例: Ctrl+S → "Ctrl+S"（プラットフォームに応じた表記）
        /// </summary>
        public static readonly FuncValueConverter<KeyGesture, string> FromKeyGesture =
            new FuncValueConverter<KeyGesture, string>(v => v?.ToString("p", null) ?? string.Empty);
    }
}
