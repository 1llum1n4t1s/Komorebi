namespace Komorebi.Models;

/// <summary>
/// 空コミット確認ダイアログの選択結果を表す列挙型。
/// </summary>
public enum ConfirmEmptyCommitResult
{
    /// <summary>操作をキャンセル。</summary>
    Cancel = 0,
    /// <summary>選択中のファイルをステージしてコミット。</summary>
    StageSelectedAndCommit,
    /// <summary>全ファイルをステージしてコミット。</summary>
    StageAllAndCommit,
    /// <summary>空コミットを作成（--allow-empty）。</summary>
    CreateEmptyCommit,
}
