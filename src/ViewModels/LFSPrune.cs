using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// LFSのプルーニング（不要オブジェクト削除）ダイアログのViewModel。
/// </summary>
public class LFSPrune : Popup
{
    /// <summary>コンストラクタ。対象リポジトリを設定する。</summary>
    public LFSPrune(Repository repo)
    {
        _repo = repo;
    }

    /// <summary>確認ボタン押下時の処理。LFS pruneコマンドを実行する。</summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.LFSPrune");

        var log = _repo.CreateLog("LFS Prune");
        Use(log);

        await new Commands.LFS(_repo.FullPath)
            .Use(log)
            .PruneAsync();

        log.Complete();
        return true;
    }

    private readonly Repository _repo = null;
}
