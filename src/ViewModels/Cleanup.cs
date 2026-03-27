using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
///     リポジトリクリーンアップダイアログのViewModel。
///     git gc --aggressive とpruneコマンドでリポジトリを最適化する。
///     ツールバーから即時実行され、進捗はダイアログで表示される。
/// </summary>
public class Cleanup : Popup
{
    /// <summary>
    ///     コンストラクタ。対象リポジトリを受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    public Cleanup(Repository repo)
    {
        _repo = repo;
    }

    /// <summary>
    ///     確定処理。git gc --aggressiveコマンドを実行してリポジトリを最適化する。
    ///     不要なオブジェクトの削除とパックファイルの再圧縮を行う。
    /// </summary>
    /// <returns>常にtrue</returns>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.Cleanup");

        // コマンドログを作成してGCを実行する
        var log = _repo.CreateLog("Cleanup (GC aggressive & prune)");
        Use(log);

        // git gc --aggressiveコマンドでガベージコレクションとpruneを実行する
        await new Commands.GC(_repo.FullPath, true)
            .Use(log)
            .ExecAsync();

        log.Complete();
        return true;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo = null;
}
