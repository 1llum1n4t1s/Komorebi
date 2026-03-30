using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// スタッシュを削除するダイアログViewModel。
/// </summary>
public class DropStash : Popup
{
    /// <summary>
    /// 削除対象のスタッシュ。
    /// </summary>
    public Models.Stash Stash { get; }

    /// <summary>
    /// コンストラクタ。対象リポジトリと削除するスタッシュを指定する。
    /// </summary>
    public DropStash(Repository repo, Models.Stash stash)
    {
        _repo = repo;
        Stash = stash;
    }

    /// <summary>
    /// スタッシュ削除を実行する確認アクション。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.DroppingStash", Stash.Name);

        var log = _repo.CreateLog("Drop Stash");
        Use(log);

        await new Commands.Stash(_repo.FullPath)
            .Use(log)
            .DropAsync(Stash.Name);

        log.Complete();
        _repo.MarkStashesDirtyManually();
        return true;
    }

    private readonly Repository _repo;
}
