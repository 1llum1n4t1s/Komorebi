namespace Komorebi.Models
{
    /// <summary>
    ///     git mergeコマンドのマージモードを表すクラス。
    ///     マージ時のfast-forward動作やコミット生成の挙動を定義する。
    /// </summary>
    public class MergeMode(string n, string d, string a)
    {
        /// <summary>
        ///     デフォルトモード（git設定に従う）。
        /// </summary>
        public static readonly MergeMode Default =
            new MergeMode("Default", "Use git configuration", "");

        /// <summary>
        ///     fast-forwardのみ許可するモード。
        /// </summary>
        public static readonly MergeMode FastForward =
            new MergeMode("Fast-forward", "Refuse to merge when fast-forward is not possible", "--ff-only");

        /// <summary>
        ///     常にマージコミットを作成するモード。
        /// </summary>
        public static readonly MergeMode NoFastForward =
            new MergeMode("No Fast-forward", "Always create a merge commit", "--no-ff");

        /// <summary>
        ///     スカッシュマージモード。
        /// </summary>
        public static readonly MergeMode Squash =
            new MergeMode("Squash", "Squash merge", "--squash");

        /// <summary>
        ///     コミットなしマージモード。
        /// </summary>
        public static readonly MergeMode DontCommit
            = new MergeMode("Don't commit", "Merge without commit", "--no-ff --no-commit");

        /// <summary>
        ///     サポートされているマージモードの一覧。
        /// </summary>
        public static readonly MergeMode[] Supported =
        [
            Default,
            FastForward,
            NoFastForward,
            Squash,
            DontCommit,
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
