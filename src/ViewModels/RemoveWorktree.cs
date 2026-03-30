using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// ワークツリーを削除するダイアログのViewModel。
/// 通常削除と強制削除に対応する。
/// </summary>
public class RemoveWorktree : Popup
{
    /// <summary>
    /// 削除対象のワークツリー。
    /// </summary>
    public Worktree Target
    {
        get;
    }

    /// <summary>
    /// 強制削除を行うかどうか。未コミットの変更がある場合に使用する。
    /// </summary>
    public bool Force
    {
        get;
        set;
    } = false;

    /// <summary>
    /// リポジトリと削除対象のワークツリーを指定してダイアログを初期化する。
    /// </summary>
    public RemoveWorktree(Repository repo, Worktree target)
    {
        _repo = repo;
        Target = target;
    }

    /// <summary>
    /// ワークツリーの削除を実行する。
    /// Forceオプションが有効な場合は未コミットの変更があっても強制削除する。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.RemoveWorktree");

        var log = _repo.CreateLog("Remove worktree");
        Use(log);

        // git worktree remove コマンドを実行
        var succ = await new Commands.Worktree(_repo.FullPath)
            .Use(log)
            .RemoveAsync(Target.FullPath, Force);

        log.Complete();
        return succ;
    }

    /// <summary>対象リポジトリ</summary>
    private readonly Repository _repo = null;
}
