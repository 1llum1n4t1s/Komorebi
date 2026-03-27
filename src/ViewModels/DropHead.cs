using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
///     HEADコミットを破棄（ドロップ）するダイアログViewModel。
///     ローカル変更がある場合は自動スタッシュを行い、ハードリセット後に復元する。
/// </summary>
public class DropHead : Popup
{
    /// <summary>
    ///     破棄対象のコミット（現在のHEAD）。
    /// </summary>
    public Models.Commit Target
    {
        get;
    }

    /// <summary>
    ///     新しいHEADとなる親コミット。
    /// </summary>
    public Models.Commit NewHead
    {
        get;
    }

    /// <summary>
    ///     コンストラクタ。対象リポジトリ、破棄するコミット、新しいHEADを指定する。
    /// </summary>
    public DropHead(Repository repo, Models.Commit target, Models.Commit parent)
    {
        _repo = repo;
        Target = target;
        NewHead = parent;
    }

    /// <summary>
    ///     HEADコミットの破棄を実行する確認アクション。
    ///     ローカル変更がある場合は自動スタッシュ→ハードリセット→スタッシュ復元を行う。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.DropHead", Target.SHA);

        var log = _repo.CreateLog($"Drop '{Target.SHA}'");
        Use(log);

        // ローカル変更の有無を確認
        var changes = await new Commands.QueryLocalChanges(_repo.FullPath, true).GetResultAsync();
        var needAutoStash = changes.Count > 0;
        var succ = false;

        // ローカル変更がある場合は自動スタッシュ
        if (needAutoStash)
        {
            succ = await new Commands.Stash(_repo.FullPath)
                .Use(log)
                .PushAsync("DROP_HEAD_AUTO_STASH", false);
            if (!succ)
            {
                log.Complete();
                return false;
            }
        }

        // 親コミットへハードリセット
        succ = await new Commands.Reset(_repo.FullPath, NewHead.SHA, "--hard")
            .Use(log)
            .ExecAsync();

        // リセット成功後、自動スタッシュを復元
        if (succ && needAutoStash)
            await new Commands.Stash(_repo.FullPath)
                .Use(log)
                .PopAsync("stash@{0}");

        log.Complete();
        return succ;
    }

    private readonly Repository _repo;
}
