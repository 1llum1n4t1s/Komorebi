// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// コミット/タグチェックアウトダイアログのViewModel。
/// git checkoutコマンドで特定のコミットまたはタグに切り替える（DetachedHEAD状態になる）。
/// </summary>
public class CheckoutDetached : Popup
{
    /// <summary>
    /// チェックアウト対象（Models.CommitまたはModels.Tag）。
    /// </summary>
    public object Target
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
    public CheckoutDetached(Repository repo, Models.Commit commit)
    {
        _repo = repo;
        _revision = commit.SHA;

        Target = commit;
        // 設定でデフォルトを Stash & Reapply にできる (upstream d4ce0b97)
        DealWithLocalChanges = Preferences.Instance.UseStashAndReapplyByDefault ?
            Models.DealWithLocalChanges.StashAndReapply :
            Models.DealWithLocalChanges.DoNothing;
    }

    /// <summary>
    /// コンストラクタ。リポジトリとタグを受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    /// <param name="tag">チェックアウト対象のタグ</param>
    public CheckoutDetached(Repository repo, Models.Tag tag)
    {
        _repo = repo;
        _revision = tag.SHA;

        Target = tag;
        DealWithLocalChanges = Preferences.Instance.UseStashAndReapplyByDefault ?
            Models.DealWithLocalChanges.StashAndReapply :
            Models.DealWithLocalChanges.DoNothing;
    }

    /// <summary>
    /// 確定処理。指定コミット/タグへのチェックアウトを実行する。
    /// 必要に応じてスタッシュの保存・復元、サブモジュール更新を行う。
    /// </summary>
    /// <returns>成功した場合はtrue</returns>
    public override async Task<bool> Sure()
    {
        ProgressDescription = App.Text("Progress.CheckoutCommit", _revision);

        var log = _repo.CreateLog("Checkout Commit");
        Use(log);

        // DetachedHEAD状態の場合、到達不能コミットの警告を表示する
        if (!await _repo.WarnIfDetachedHeadLosesCommitsAsync())
            return true;

        var succ = false;
        var needPop = false;
        var stashFailed = false;

        // LockWatcher は git コマンド実行中だけ保持する（ブロック構文）。
        // MarkWorkingCopyDirtyManually はロック解除後に呼ぶ（Discard.cs パターン準拠）。
        using (_repo.LockWatcher())
        {
            if (DealWithLocalChanges == Models.DealWithLocalChanges.DoNothing)
            {
                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .CommitAsync(_revision, false);
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
                        stashFailed = true;
                    else
                        needPop = true;
                }

                if (!stashFailed)
                {
                    succ = await new Commands.Checkout(_repo.FullPath)
                        .Use(log)
                        .CommitAsync(_revision, false);
                }
            }
            else
            {
                succ = await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .CommitAsync(_revision, true);
            }

            if (succ && !stashFailed)
            {
                // サブモジュールを自動更新する
                await _repo.AutoUpdateSubmodulesAsync(log);

                // 自動スタッシュを行った場合はポップして復元する
                if (needPop)
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

        return succ;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo = null;

    /// <summary>チェックアウト対象のSHA（コミットまたはタグのSHA）</summary>
    private readonly string _revision = string.Empty;
}
