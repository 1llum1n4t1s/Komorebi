namespace Komorebi.Models;

/// <summary>
/// リポジトリの各種データ更新メソッドを定義するインターフェース
/// </summary>
public interface IRepository
{
    /// <summary>サブモジュールが存在する可能性があるかを返す</summary>
    bool MayHaveSubmodules();

    /// <summary>ブランチ一覧を更新する</summary>
    void RefreshBranches();
    /// <summary>ワークツリー一覧を更新する</summary>
    void RefreshWorktrees();
    /// <summary>タグ一覧を更新する</summary>
    void RefreshTags();
    /// <summary>コミット履歴を更新する</summary>
    void RefreshCommits();
    /// <summary>サブモジュール一覧を更新する</summary>
    void RefreshSubmodules();
    /// <summary>ワーキングコピーの変更状態を更新する</summary>
    void RefreshWorkingCopyChanges();
    /// <summary>スタッシュ一覧を更新する</summary>
    void RefreshStashes();
}
