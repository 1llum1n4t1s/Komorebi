using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     リモートブランチをローカルブランチにフェッチ（fast-forward）するダイアログViewModel。
    /// </summary>
    public class FetchInto : Popup
    {
        /// <summary>
        ///     フェッチ先のローカルブランチ。
        /// </summary>
        public Models.Branch Local
        {
            get;
        }

        /// <summary>
        ///     フェッチ元のアップストリーム（リモート）ブランチ。
        /// </summary>
        public Models.Branch Upstream
        {
            get;
        }

        /// <summary>
        ///     コンストラクタ。対象リポジトリ、ローカルブランチ、アップストリームブランチを指定する。
        /// </summary>
        public FetchInto(Repository repo, Models.Branch local, Models.Branch upstream)
        {
            _repo = repo;
            Local = local;
            Upstream = upstream;
        }

        /// <summary>
        ///     フェッチを実行する確認アクション。
        ///     フェッチ後、履歴ビュー表示中の場合は新しいHEADへナビゲートする。
        /// </summary>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Fast-Forward ...";

            var log = _repo.CreateLog($"Fetch Into '{Local.FriendlyName}'");
            Use(log);

            await new Commands.Fetch(_repo.FullPath, Local, Upstream)
                .Use(log)
                .RunAsync();

            log.Complete();

            // 履歴ビュー表示中の場合、更新後のHEADへナビゲート
            if (_repo.SelectedViewIndex == 0)
            {
                var newHead = await new Commands.QueryRevisionByRefName(_repo.FullPath, Local.Name).GetResultAsync();
                _repo.NavigateToCommit(newHead, true);
            }

            return true;
        }

        private readonly Repository _repo = null;
    }
}
