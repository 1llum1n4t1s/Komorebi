using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// スタッシュ全消去ダイアログのViewModel。
/// git stash clearコマンドで全てのスタッシュを削除する。
/// </summary>
public class ClearStashes : Popup
{
    /// <summary>
    /// コンストラクタ。対象リポジトリを受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    public ClearStashes(Repository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// 確定処理。git stash clearコマンドを実行して全スタッシュを削除する。
    /// </summary>
    /// <returns>常にtrue</returns>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.ClearAllStashes");

        // コマンドログを作成してスタッシュクリアを実行する
        var log = _repo.CreateLog("Clear Stashes");
        Use(log);

        // git stash clearコマンドで全スタッシュを削除する
        await new Commands.Stash(_repo.FullPath)
            .Use(log)
            .ClearAsync();

        log.Complete();
        // スタッシュ一覧の更新を通知する
        _repo.MarkStashesDirtyManually();
        return true;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo = null;
}
