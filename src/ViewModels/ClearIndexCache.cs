using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     インデックスキャッシュクリアダイアログのViewModel。
    ///     git rm -r --cached . と git add . を実行してインデックスを再構築する。
    ///     .gitignoreの変更を反映させる場合などに使用する。
    /// </summary>
    public class ClearIndexCache : Popup
    {
        /// <summary>
        ///     コンストラクタ。対象リポジトリを受け取って初期化する。
        /// </summary>
        /// <param name="repo">対象のリポジトリViewModel</param>
        public ClearIndexCache(Repository repo)
        {
            _repo = repo;
        }

        /// <summary>
        ///     確定処理。インデックスキャッシュをクリアして再追加する。
        ///     git rm -r --cached . で全ファイルをインデックスから削除し、
        ///     git add . で再追加することでインデックスを再構築する。
        /// </summary>
        /// <returns>常にtrue</returns>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Clear index cache ...";

            // コマンドログを作成する
            var log = _repo.CreateLog("Clear index cache");
            Use(log);

            // git rm -r --cached . でインデックスから全ファイルを削除する（作業ツリーには影響しない）
            await new Commands.Command
            {
                WorkingDirectory = _repo.FullPath,
                Context = _repo.FullPath,
                Args = "rm -r --cached .",
            }.Use(log).ExecAsync();

            // git add . で全ファイルをインデックスに再追加する
            await new Commands.Command
            {
                WorkingDirectory = _repo.FullPath,
                Context = _repo.FullPath,
                Args = "add .",
            }.Use(log).ExecAsync();

            log.Complete();
            return true;
        }

        /// <summary>対象リポジトリへの参照</summary>
        private readonly Repository _repo;
    }
}
