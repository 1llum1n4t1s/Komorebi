using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// ブランチチェックアウトダイアログのViewModel。
/// git checkoutコマンドで指定ブランチに切り替える。
/// ローカル変更がある場合は自動スタッシュを行う。
/// </summary>
public class Checkout : Popup
{
    /// <summary>
    /// チェックアウト対象のブランチ名。
    /// </summary>
    public string BranchName
    {
        get => _branch.Name;
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
    /// コンストラクタ。リポジトリとブランチを受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    /// <param name="branch">チェックアウト対象のブランチ</param>
    public Checkout(Repository repo, Models.Branch branch)
    {
        _repo = repo;
        _branch = branch;
        DealWithLocalChanges = Models.DealWithLocalChanges.DoNothing;
    }

    /// <summary>
    /// 確定処理。ブランチのチェックアウトを実行する。
    /// 必要に応じてスタッシュの保存・復元、サブモジュール更新を行う。
    /// </summary>
    /// <returns>成功した場合はtrue</returns>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        var branchName = BranchName;
        ProgressDescription = $"Checkout '{branchName}' ...";

        var log = _repo.CreateLog($"Checkout '{branchName}'");
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

        if (DealWithLocalChanges == Models.DealWithLocalChanges.DoNothing)
        {
            succ = await new Commands.Checkout(_repo.FullPath)
                .Use(log)
                .BranchAsync(branchName, false);
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

                needPopStash = true;
            }

            succ = await new Commands.Checkout(_repo.FullPath)
                .Use(log)
                .BranchAsync(branchName, false);
        }
        else
        {
            succ = await new Commands.Checkout(_repo.FullPath)
                .Use(log)
                .BranchAsync(branchName, true);
        }

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
        var b = _repo.Branches.Find(x => x.IsLocal && x.Name == branchName);
        if (b is not null && _repo.HistoryFilterMode == Models.FilterMode.Included)
            _repo.SetBranchFilterMode(b, Models.FilterMode.Included, false, false);

        // ブランチ一覧の更新を通知する
        _repo.MarkBranchesDirtyManually();

        // ブランチ更新を待つ
        ProgressDescription = App.Text("Progress.WaitingBranchUpdate");
        await Task.Delay(400);
        return succ;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo = null;
    private readonly Models.Branch _branch = null;
}
