namespace Komorebi.Models
{
    /// <summary>
    ///     空のコミット確認ダイアログの結果を表す列挙型。
    ///     ステージングされた変更がない場合のユーザー選択肢。
    /// </summary>
    public enum ConfirmEmptyCommitResult
    {
        /// <summary>
        ///     操作をキャンセルする。
        /// </summary>
        Cancel = 0,

        /// <summary>
        ///     すべての変更をステージングしてコミットする。
        /// </summary>
        StageAllAndCommit,

        /// <summary>
        ///     空のコミットを作成する（--allow-empty）。
        /// </summary>
        CreateEmptyCommit,
    }
}
