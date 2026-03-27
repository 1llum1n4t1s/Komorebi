using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
///     HEADコミットのSquashまたはFixup操作を行うポップアップダイアログのViewModel。
///     対象コミットまでソフトリセットし、新しいメッセージでコミットし直す。
/// </summary>
public class SquashOrFixupHead : Popup
{
    /// <summary>
    ///     Fixupモードかどうか。falseの場合はSquashモード。
    /// </summary>
    public bool IsFixupMode
    {
        get;
    }

    /// <summary>
    ///     Squash/Fixup対象のコミット。
    /// </summary>
    public Models.Commit Target
    {
        get;
    }

    /// <summary>
    ///     新しいコミットメッセージ。
    /// </summary>
    [Required(ErrorMessage = "Commit message is required!!!")]
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value, true);
    }

    /// <summary>
    ///     現在のブランチにupstreamが設定されているかどうか。
    /// </summary>
    public bool HasUpstream
    {
        get;
    }

    /// <summary>
    ///     操作完了後に強制プッシュするかどうか。
    /// </summary>
    public bool ForcePushAfterDone
    {
        get;
        set;
    }

    /// <summary>
    ///     コンストラクタ。リポジトリ、対象コミット、メッセージ、モードを受け取る。
    /// </summary>
    public SquashOrFixupHead(Repository repo, Models.Commit target, string message, bool fixup)
    {
        IsFixupMode = fixup;
        Target = target;

        _repo = repo;
        _message = message;

        var branch = repo.CurrentBranch;
        HasUpstream = branch is not null && !string.IsNullOrEmpty(branch.Upstream);
    }

    /// <summary>
    ///     Squash/Fixup操作を実行する。
    ///     ステージされた変更がある場合は自動スタッシュし、ソフトリセット後にamendコミットを行う。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = IsFixupMode ? "Fixup ..." : "Squashing ...";

        var log = _repo.CreateLog(IsFixupMode ? "Fixup" : "Squash");
        Use(log);

        var changes = await new Commands.QueryLocalChanges(_repo.FullPath, false).GetResultAsync();
        var signOff = _repo.UIStates.EnableSignOffForCommit;
        var noVerify = _repo.UIStates.NoVerifyOnCommit;
        var needAutoStash = false;
        var succ = false;

        foreach (var c in changes)
        {
            if (c.Index != Models.ChangeState.None)
            {
                needAutoStash = true;
                break;
            }
        }

        if (needAutoStash)
        {
            succ = await new Commands.Stash(_repo.FullPath)
                .Use(log)
                .PushAsync(IsFixupMode ? "FIXUP_AUTO_STASH" : "SQUASH_AUTO_STASH", false);
            if (!succ)
            {
                log.Complete();
                return false;
            }
        }

        succ = await new Commands.Reset(_repo.FullPath, Target.SHA, "--soft")
            .Use(log)
            .ExecAsync();

        if (succ)
            succ = await new Commands.Commit(_repo.FullPath, _message, signOff, noVerify, true, false)
                .Use(log)
                .RunAsync();

        if (succ && needAutoStash)
            await new Commands.Stash(_repo.FullPath)
                .Use(log)
                .PopAsync("stash@{0}");

        if (succ && ForcePushAfterDone)
        {
            var branch = _repo.CurrentBranch;
            if (branch is not null && !string.IsNullOrEmpty(branch.Upstream))
            {
                var upstream = branch.Upstream.Substring(13); // "refs/remotes/" を除去
                var separatorIdx = upstream.IndexOf('/');
                if (separatorIdx > 0)
                {
                    var remote = upstream.Substring(0, separatorIdx);
                    var remoteBranch = upstream.Substring(separatorIdx + 1);
                    await new Commands.Push(_repo.FullPath, branch.Name, remote, remoteBranch, false, false, false, true)
                        .Use(log)
                        .ExecAsync();
                }
            }
        }

        log.Complete();
        return succ;
    }

    private readonly Repository _repo;
    private string _message;
}
