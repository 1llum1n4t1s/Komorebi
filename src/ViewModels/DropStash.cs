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
        ProgressDescription = App.Text("Progress.DroppingStash", Stash.Name);

        var log = _repo.CreateLog("Drop Stash");
        Use(log);

        // Watcher ロックは git コマンド実行範囲に限定し、Mark*DirtyManually はロック解除後に呼ぶ
        // （DeleteBranch.cs と同じ。ロック中に呼ぶと解除後に届く FS イベントが Refresh をキャンセルする。PITFALLS 参照）。
        using (_repo.LockWatcher())
        {
            await new Commands.Stash(_repo.FullPath)
                .Use(log)
                .DropAsync(Stash.Name);
        }

        log.Complete();
        _repo.MarkStashesDirtyManually();
        return true;
    }

    private readonly Repository _repo;
}
