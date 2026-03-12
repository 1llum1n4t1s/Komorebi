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
        ///     Aggressiveモード（git gc --aggressive）を有効にするかどうか。
        ///     デルタ圧縮をゼロからやり直し、より強力に最適化する。
        ///     通常より大幅に時間がかかる。
        /// </summary>
        public bool Aggressive
        {
            get => _aggressive;
            set => SetProperty(ref _aggressive, value);
        }

        /// <summary>
        ///     確定処理。git gcコマンドを実行してリポジトリを最適化する。
        ///     不要なオブジェクトの削除とパックファイルの再圧縮を行う。
        /// </summary>
        /// <returns>常にtrue</returns>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            var desc = Aggressive ? "Cleanup (GC aggressive & prune) ..." : "Cleanup (GC & prune) ...";
            ProgressDescription = desc;

            // コマンドログを作成してGCを実行する
            var log = _repo.CreateLog(desc);
            Use(log);

            // git gcコマンドでガベージコレクションとpruneを実行する
            await new Commands.GC(_repo.FullPath, Aggressive)
                .Use(log)
                .ExecAsync();

            log.Complete();
            return true;
        }

        /// <summary>対象リポジトリへの参照</summary>
        private readonly Repository _repo = null;
        private bool _aggressive = false;
    }
}
