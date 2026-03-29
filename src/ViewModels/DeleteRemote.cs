using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// リモートリポジトリの設定を削除するためのダイアログViewModel。
/// </summary>
public class DeleteRemote : Popup
{
    /// <summary>
    /// 削除対象のリモート。
    /// </summary>
    public Models.Remote Remote
    {
        get;
        private set;
    }

    /// <summary>
    /// コンストラクタ。対象リポジトリと削除するリモートを指定する。
    /// </summary>
    public DeleteRemote(Repository repo, Models.Remote remote)
    {
        _repo = repo;
        Remote = remote;
    }

    /// <summary>
    /// リモート削除を実行する確認アクション。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.DeletingRemote");

        var log = _repo.CreateLog("Delete Remote");
        Use(log);

        var succ = await new Commands.Remote(_repo.FullPath)
            .Use(log)
            .DeleteAsync(Remote.Name);

        log.Complete();
        _repo.MarkBranchesDirtyManually();
        return succ;
    }

    private readonly Repository _repo = null;
}
