using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// チェックアウトとファストフォワードを同時に行うダイアログのViewModel。
/// ローカルブランチをリモートブランチの最新にファストフォワードしてからチェックアウトする。
/// </summary>
public class CheckoutAndFastForward : Popup
{
    /// <summary>
    /// チェックアウト対象のローカルブランチ。
    /// </summary>
    public Models.Branch LocalBranch
    {
        get;
    }

    /// <summary>
    /// ファストフォワード元のリモートブランチ。
    /// </summary>
    public Models.Branch RemoteBranch
    {
        get;
    }

    /// <summary>
    /// ローカル変更があるかどうか。
    /// </summary>
    public bool HasLocalChanges
    {
        get => _repo.LocalChangesCount > 0;
    }

    /// <summary>
    /// ローカル変更の扱い方。
    /// </summary>
    public Models.DealWithLocalChanges DealWithLocalChanges
    {
        get;
        set;
    }

    /// <summary>
    /// コンストラクタ。リポジトリ・ローカルブランチ・リモートブランチを受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    /// <param name="localBranch">チェックアウト対象のローカルブランチ</param>
    /// <param name="remoteBranch">ファストフォワード元のリモートブランチ</param>
    public CheckoutAndFastForward(Repository repo, Models.Branch localBranch, Models.Branch remoteBranch)
    {
        _repo = repo;
        LocalBranch = localBranch;
        RemoteBranch = remoteBranch;
        DealWithLocalChanges = Models.DealWithLocalChanges.DoNothing;
    }

    /// <summary>
    /// 確定処理。チェックアウトとファストフォワードを実行する。
    /// 必要に応じてスタッシュの保存・復元、サブモジュール更新を行う。
    /// </summary>
    /// <returns>成功した場合はtrue</returns>
    public override async Task<bool> Sure()
    {
        ProgressDescription = App.Text("Progress.CheckoutAndFastForward", LocalBranch.Name);

        var log = _repo.CreateLog($"Checkout and Fast-Forward '{LocalBranch.Name}' ...");
        Use(log);

        // DetachedHEAD状態の場合、到達不能コミットの警告を表示する
        if (!await _repo.WarnIfDetachedHeadLosesCommitsAsync())
            return true;

        var succ = false;
        var needPopStash = false;
        var stashFailed = false;

        // LockWatcher は git コマンド実行中だけ保持する（ブロック構文）。
        // MarkWorkingCopyDirtyManually / MarkBranchesDirtyManually はロック解除後に呼ぶ
        // （Discard.cs パターン準拠）。
        using (_repo.LockWatcher())
        {
            if (DealWithLocalChanges == Models.DealWithLocalChanges.DoNothing)
            {
                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .BranchAsync(LocalBranch.Name, RemoteBranch.Head, false, true);
            }
            else if (DealWithLocalChanges == Models.DealWithLocalChanges.StashAndReapply)
            {
                var changes = await new Commands.CountLocalChanges(_repo.FullPath, false).GetResultAsync();
                if (changes > 0)
                {
                    succ = await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PushAsync("CHECKOUT_AND_FASTFORWARD_AUTO_STASH", false);
                    if (!succ)
                        stashFailed = true;
                    else
                        needPopStash = true;
                }

                if (!stashFailed)
                {
                    succ = await new Commands.Checkout(_repo.FullPath)
                        .Use(log)
                        .BranchAsync(LocalBranch.Name, RemoteBranch.Head, false, true);
                }
            }
            else
            {
                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .BranchAsync(LocalBranch.Name, RemoteBranch.Head, true, true);
            }

            if (succ && !stashFailed)
            {
                // サブモジュールを自動更新する
                await _repo.AutoUpdateSubmodulesAsync(log);

                // 自動スタッシュを行った場合はポップして復元する
                if (needPopStash)
                    await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PopAsync("stash@{0}");
            }
        }

        log.Complete();

        if (stashFailed)
        {
            _repo.MarkWorkingCopyDirtyManually();
            return false;
        }

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
