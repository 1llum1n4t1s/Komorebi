namespace Komorebi.Models
{
    /// <summary>
    ///     git worktree（ワークツリー）を表すクラス。
    ///     リポジトリに関連付けられた作業ディレクトリの情報を保持する。
    /// </summary>
    public class Worktree
    {
        /// <summary>
        ///     ワークツリーに関連付けられたブランチ名。
        /// </summary>
        public string Branch { get; set; } = string.Empty;

        /// <summary>
        ///     ワークツリーのフルパス。
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        ///     ワークツリーのHEADコミットSHA。
        /// </summary>
        public string Head { get; set; } = string.Empty;

        /// <summary>
        ///     ベアリポジトリかどうか。
        /// </summary>
        public bool IsBare { get; set; } = false;

        /// <summary>
        ///     HEADがデタッチド状態かどうか。
        /// </summary>
        public bool IsDetached { get; set; } = false;

        /// <summary>
        ///     ワークツリーがロックされているかどうか。
        /// </summary>
        public bool IsLocked { get; set; } = false;
    }
}
