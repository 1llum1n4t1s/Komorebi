using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
///     ブランチチェックアウトダイアログのViewModel。
///     git checkoutコマンドで指定ブランチに切り替える。
///     ローカル変更がある場合は自動スタッシュを行う。
/// </summary>
public class Checkout : Popup
{
    /// <summary>
    ///     チェックアウト対象のブランチ名。
    /// </summary>
    public string Branch
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
    ///     コンストラクタ。リポジトリとブランチ名を受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    /// <param name="branch">チェックアウト対象のブランチ名</param>
    public Checkout(Repository repo, string branch)
    {
        _repo = repo;
        Branch = branch;
        DiscardLocalChanges = false;
    }

    /// <summary>
    ///     ローカル変更がない場合はダイアログを表示せず直接実行可能かどうかを判定する。
    /// </summary>
    /// <returns>ローカル変更がなければtrue</returns>
    public override bool CanStartDirectly()
    {
        return _repo.LocalChangesCount == 0;
    }

    /// <summary>
    ///     確定処理。ブランチのチェックアウトを実行する。
    ///     必要に応じてスタッシュの保存・復元、サブモジュール更新を行う。
    /// </summary>
    /// <returns>成功した場合はtrue</returns>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.Checkout", Branch);

        var log = _repo.CreateLog($"Checkout '{Branch}'");
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
                    .PushAsync("CHECKOUT_AUTO_STASH", false);
                if (!succ)
                {
                    log.Complete();
                    return false;
                }

                needPopStash = true;
            }
        }

        // git checkoutコマンドでブランチを切り替える
        succ = await new Commands.Checkout(_repo.FullPath)
            .Use(log)
            .BranchAsync(Branch, DiscardLocalChanges);

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
        var b = _repo.Branches.Find(x => x.IsLocal && x.Name == Branch);
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
}
