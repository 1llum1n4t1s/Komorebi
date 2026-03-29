using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// コミットのリバート操作を行うポップアップダイアログのViewModel。
/// 指定されたコミットの変更を打ち消す新しいコミットを作成する。
/// </summary>
public class Revert : Popup
{
    /// <summary>
    /// リバート対象のコミット。
    /// </summary>
    public Models.Commit Target
    {
        get;
    }

    /// <summary>
    /// リバート後に自動的にコミットするかどうか。
    /// </summary>
    public bool AutoCommit
    {
        get;
        set;
    }

    /// <summary>
    /// コンストラクタ。リポジトリとリバート対象コミットを受け取る。
    /// </summary>
    /// <param name="repo">対象リポジトリ</param>
    /// <param name="target">リバート対象のコミット</param>
    public Revert(Repository repo, Models.Commit target)
    {
        _repo = repo;
        Target = target;
        AutoCommit = true;
    }

    /// <summary>
    /// リバート操作を実行する。git revertコマンドを非同期で実行する。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        _repo.ClearCommitMessage();
        ProgressDescription = App.Text("Progress.Reverting", Target.SHA);

        var log = _repo.CreateLog($"Revert '{Target.SHA}'");
        Use(log);

        await new Commands.Revert(_repo.FullPath, Target.SHA, AutoCommit)
            .Use(log)
            .ExecAsync();

        log.Complete();
        return true;
    }

    /// <summary>対象リポジトリ</summary>
    private readonly Repository _repo = null;
}
