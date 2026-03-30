using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// コミットチェックアウトダイアログのViewModel。
/// git checkoutコマンドで特定のコミットに切り替える（DetachedHEAD状態になる）。
/// </summary>
public class CheckoutCommit : Popup
{
    /// <summary>
    /// チェックアウト対象のコミット。
    /// </summary>
    public Models.Commit Commit
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
    /// コンストラクタ。リポジトリとコミットを受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    /// <param name="commit">チェックアウト対象のコミット</param>
    public CheckoutCommit(Repository repo, Models.Commit commit)
    {
        _repo = repo;
        Commit = commit;
        DealWithLocalChanges = Models.DealWithLocalChanges.DoNothing;
    }

    /// <summary>
    /// 確定処理。指定コミットへのチェックアウトを実行する。
    /// 必要に応じてスタッシュの保存・復元、サブモジュール更新を行う。
    /// </summary>
    /// <returns>成功した場合はtrue</returns>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.CheckoutCommit", Commit.SHA);

        var log = _repo.CreateLog("Checkout Commit");
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
        var needPop = false;

        if (DealWithLocalChanges == Models.DealWithLocalChanges.DoNothing)
        {
            succ = await new Commands.Checkout(_repo.FullPath)
                .Use(log)
                .CommitAsync(Commit.SHA, false);
        }
        else if (DealWithLocalChanges == Models.DealWithLocalChanges.StashAndReapply)
        {
            var changes = await new Commands.CountLocalChanges(_repo.FullPath, false).GetResultAsync();
            if (changes > 0)
            {
                succ = await new Commands.Stash(_repo.FullPath)
                    .Use(log)
                    .PushAsync("CHECKOUT_AUTO_STASH", false);
                if (!succ)
                {
                    log.Complete();
                    _repo.MarkWorkingCopyDirtyManually();
                    return false;
                }

                needPop = true;
            }

            succ = await new Commands.Checkout(_repo.FullPath)
                .Use(log)
                .CommitAsync(Commit.SHA, false);
        }
        else
        {
            succ = await new Commands.Checkout(_repo.FullPath)
                .Use(log)
                .CommitAsync(Commit.SHA, true);
        }

        if (succ)
        {
            // サブモジュールを自動更新する
            await _repo.AutoUpdateSubmodulesAsync(log);

            // 自動スタッシュを行った場合はポップして復元する
            if (needPop)
                await new Commands.Stash(_repo.FullPath)
                    .Use(log)
                    .PopAsync("stash@{0}");
        }

        log.Complete();
        return succ;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo = null;
}
