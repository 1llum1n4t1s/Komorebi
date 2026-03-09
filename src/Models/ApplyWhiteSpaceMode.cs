namespace Komorebi.Models
{
    /// <summary>
    ///     パッチ適用時の空白文字処理モードを表すクラス。
    ///     git applyコマンドの--whitespaceオプションに対応する。
    /// </summary>
    public class ApplyWhiteSpaceMode(string n, string d, string a)
    {
        /// <summary>
        ///     サポートされている空白文字処理モードの一覧。
        /// </summary>
        public static readonly ApplyWhiteSpaceMode[] Supported =
        [
            new ApplyWhiteSpaceMode("No Warn", "Turns off the trailing whitespace warning", "nowarn"),
            new ApplyWhiteSpaceMode("Warn", "Outputs warnings for a few such errors, but applies", "warn"),
            new ApplyWhiteSpaceMode("Error", "Raise errors and refuses to apply the patch", "error"),
            new ApplyWhiteSpaceMode("Error All", "Similar to 'error', but shows more", "error-all"),
        ];

        /// <summary>
        ///     モードの表示名。
        /// </summary>
        public string Name { get; set; } = n;

        /// <summary>
        ///     モードの説明文。
        /// </summary>
        public string Desc { get; set; } = d;

        /// <summary>
        ///     gitコマンドに渡す引数文字列。
        /// </summary>
        public string Arg { get; set; } = a;
    }
}
