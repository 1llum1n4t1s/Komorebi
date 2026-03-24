using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     チェックアウトとファストフォワードを同時に行うダイアログのViewModel。
    ///     ローカルブランチをリモートブランチの最新にファストフォワードしてからチェックアウトする。
    /// </summary>
    public class CheckoutAndFastForward : Popup
    {
        /// <summary>
        ///     チェックアウト対象のローカルブランチ。
        /// </summary>
        public Models.Branch LocalBranch
        {
            get;
        }

        /// <summary>
        ///     ファストフォワード元のリモートブランチ。
        /// </summary>
        public Models.Branch RemoteBranch
        {
            get;
        }

        /// <summary>
        ///     ローカル変更を破棄するかどうかのフラグ。
        /// </summary>
        public bool DiscardLocalChanges
        {
            get;
            set;
        }

        /// <summary>
        ///     コンストラクタ。リポジトリ・ローカルブランチ・リモートブランチを受け取って初期化する。
        /// </summary>
        /// <param name="repo">対象のリポジトリViewModel</param>
        /// <param name="localBranch">チェックアウト対象のローカルブランチ</param>
        /// <param name="remoteBranch">ファストフォワード元のリモートブランチ</param>
        public CheckoutAndFastForward(Repository repo, Models.Branch localBranch, Models.Branch remoteBranch)
        {
            _repo = repo;
            LocalBranch = localBranch;
            RemoteBranch = remoteBranch;
        }

        /// <summary>
        ///     確定処理。チェックアウトとファストフォワードを実行する。
        ///     必要に応じてスタッシュの保存・復元、サブモジュール更新を行う。
        /// </summary>
        /// <returns>成功した場合はtrue</returns>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = App.Text("Progress.CheckoutAndFastForward", LocalBranch.Name);

            var log = _repo.CreateLog($"Checkout and Fast-Forward '{LocalBranch.Name}' ...");
            Use(log);

            // DetachedHEAD状態の場合、到達不能コミットの警告を表示する
            if (_repo.CurrentBranch is { IsDetachedHead: true })
            {
                var refs = await new Commands.QueryRefsContainsCommit(_repo.FullPath, _repo.CurrentBranch.Head).GetResultAsync();
                if (refs.Count == 0)
                {
                    var msg = App.Text("Checkout.WarnLostCommits");
                    var shouldContinue = await App.AskConfirmAsync(msg);
                    if (!shouldContinue)
                        return true;
                }
            }

            var succ = false;
            var needPopStash = false;

            // ローカル変更を破棄しない場合、自動スタッシュを行う
            if (!DiscardLocalChanges)
            {
                var changes = await new Commands.CountLocalChanges(_repo.FullPath, false).GetResultAsync();
                if (changes > 0)
                {
                    // ローカル変更を一時的にスタッシュに保存する
                    succ = await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PushAsync("CHECKOUT_AND_FASTFORWARD_AUTO_STASH", false);
                    if (!succ)
                    {
                        log.Complete();
                        return false;
                    }

                    needPopStash = true;
                }
            }

            // git checkoutコマンドでブランチ切り替えとファストフォワードを同時実行する
            succ = await new Commands.Checkout(_repo.FullPath)
                .Use(log)
                .BranchAsync(LocalBranch.Name, RemoteBranch.Head, DiscardLocalChanges, true);

            if (succ)
            {
                // サブモジュールを自動更新する
                await _repo.AutoUpdateSubmodulesAsync(log);

                // 自動スタッシュを行った場合はポップして復元する
                if (needPopStash)
                    await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PopAsync("stash@{0}");
            }

            log.Complete();

            // フィルタモードでチェックアウトしたブランチをIncludedに設定する
            if (_repo.HistoryFilterMode == Models.FilterMode.Included)
                _repo.SetBranchFilterMode(LocalBranch, Models.FilterMode.Included, false, false);

            // ブランチ一覧の更新を通知する
            _repo.MarkBranchesDirtyManually();

            // ブランチ更新を待つ
            ProgressDescription = App.Text("Progress.WaitingBranchUpdate");
            await Task.Delay(400);
            return succ;
        }

        /// <summary>対象リポジトリへの参照</summary>
        private Repository _repo;
    }
}
