using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     リポジトリクリーンアップダイアログのViewModel。
    ///     git gc（ガベージコレクション）とpruneコマンドでリポジトリを最適化する。
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
        ///     確定処理。git gcコマンドを実行してリポジトリを最適化する。
        ///     不要なオブジェクトの削除とパックファイルの再圧縮を行う。
        /// </summary>
        /// <returns>常にtrue</returns>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Cleanup (GC & prune) ...";

            // コマンドログを作成してGCを実行する
            var log = _repo.CreateLog("Cleanup (GC & prune)");
            Use(log);

            // git gcコマンドでガベージコレクションとpruneを実行する
            await new Commands.GC(_repo.FullPath)
                .Use(log)
                .ExecAsync();

            log.Complete();
            return true;
        }

        /// <summary>対象リポジトリへの参照</summary>
        private readonly Repository _repo = null;
    }
}
