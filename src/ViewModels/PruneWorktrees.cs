using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    /// 不要なワークツリー情報を削除（プルーン）するダイアログのViewModel。
    /// git worktree pruneコマンドを実行する。
    /// </summary>
    public class PruneWorktrees : Popup
    {
        /// <summary>
        /// リポジトリを指定してダイアログを初期化する。
        /// </summary>
        public PruneWorktrees(Repository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// ワークツリーのプルーンを実行する。
        /// ディスク上に存在しないワークツリーの管理情報を削除する。
        /// </summary>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = App.Text("Progress.PruneWorktrees");

            var log = _repo.CreateLog("Prune Worktrees");
            Use(log);

            // git worktree prune コマンドを実行
            await new Commands.Worktree(_repo.FullPath)
                .Use(log)
                .PruneAsync();

            log.Complete();
            return true;
        }

        /// <summary>対象リポジトリ</summary>
        private readonly Repository _repo = null;
    }
}
