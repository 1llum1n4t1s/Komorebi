namespace Komorebi.Models
{
    /// <summary>
    ///     コミットグラフに表示されるデコレータ（ラベル）の種類を表す列挙型。
    /// </summary>
    public enum DecoratorType
    {
        /// <summary>
        ///     デコレータなし。
        /// </summary>
        None,

        /// <summary>
        ///     現在チェックアウト中のブランチのHEAD。
        /// </summary>
        CurrentBranchHead,

        /// <summary>
        ///     ローカルブランチのHEAD。
        /// </summary>
        LocalBranchHead,

        /// <summary>
        ///     現在のコミットのHEAD（デタッチド状態）。
        /// </summary>
        CurrentCommitHead,

        /// <summary>
        ///     リモートブランチのHEAD。
        /// </summary>
        RemoteBranchHead,

        /// <summary>
        ///     タグ。
        /// </summary>
        Tag,
    }

    /// <summary>
    ///     コミットに付与されるデコレータ（ブランチ名、タグ名等）を表すクラス。
    /// </summary>
    public class Decorator
    {
        /// <summary>
        ///     デコレータの種類。
        /// </summary>
        public DecoratorType Type { get; set; } = DecoratorType.None;

        /// <summary>
        ///     デコレータの表示名（ブランチ名やタグ名）。
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        ///     このデコレータがタグかどうかを判定する。
        /// </summary>
        public bool IsTag => Type == DecoratorType.Tag;
    }
}
