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
    /// ローカル変更を破棄するかどうかのフラグ。
    /// </summary>
    public bool DiscardLocalChanges
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
        DiscardLocalChanges = false;
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

        // ローカル変更を破棄しない場合、自動スタッシュを行う
        if (!DiscardLocalChanges)
        {
            var changes = await new Commands.CountLocalChanges(_repo.FullPath, false).GetResultAsync();
            if (changes > 0)
            {
                // ローカル変更を一時的にスタッシュに保存する
                succ = await new Commands.Stash(_repo.FullPath)
                    .Use(log)
                    .PushAsync("CHECKOUT_AUTO_STASH", false);
                if (!succ)
                {
                    log.Complete();
                    return false;
                }

                needPop = true;
            }
        }

        // git checkoutコマンドでコミットに切り替える
        succ = await new Commands.Checkout(_repo.FullPath)
            .Use(log)
            .CommitAsync(Commit.SHA, DiscardLocalChanges);

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
