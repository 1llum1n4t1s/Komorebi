using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    /// リモートリポジトリの不要な追跡ブランチを削除（プルーン）するダイアログのViewModel。
    /// git remote pruneコマンドを実行する。
    /// </summary>
    public class PruneRemote : Popup
    {
        /// <summary>
        /// プルーン対象のリモート。
        /// </summary>
        public Models.Remote Remote
        {
            get;
        }

        /// <summary>
        /// リポジトリとリモートを指定してダイアログを初期化する。
        /// </summary>
        public PruneRemote(Repository repo, Models.Remote remote)
        {
            _repo = repo;
            Remote = remote;
        }

        /// <summary>
        /// リモートのプルーンを実行する。
        /// リモート上で既に削除されたブランチの追跡参照をローカルから削除する。
        /// </summary>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Run `prune` on remote ...";

            var log = _repo.CreateLog($"Prune Remote '{Remote.Name}'");
            Use(log);

            // git remote prune コマンドを実行
            var succ = await new Commands.Remote(_repo.FullPath)
                .Use(log)
                .PruneAsync(Remote.Name);

            log.Complete();
            return succ;
        }

        /// <summary>対象リポジトリ</summary>
        private readonly Repository _repo = null;
    }
}
