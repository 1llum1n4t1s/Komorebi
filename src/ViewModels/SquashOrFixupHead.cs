using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// HEADコミットのSquashまたはFixup操作を行うポップアップダイアログのViewModel。
/// 対象コミットまでソフトリセットし、新しいメッセージでコミットし直す。
/// </summary>
public class SquashOrFixupHead : Popup
{
    /// <summary>
    /// Fixupモードかどうか。falseの場合はSquashモード。
    /// </summary>
    public bool IsFixupMode
    {
        get;
    }

    /// <summary>
    /// Squash/Fixup対象のコミット。
    /// </summary>
    public Models.Commit Target
    {
        get;
    }

    /// <summary>
    /// 新しいコミットメッセージ。
    /// </summary>
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.CommitMessageRequired))]
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value, true);
    }

    /// <summary>
    /// 現在のブランチにupstreamが設定されているかどうか。
    /// </summary>
    public bool HasUpstream
    {
        get;
    }

    /// <summary>
    /// 操作完了後に強制プッシュするかどうか。
    /// </summary>
    public bool ForcePushAfterDone
    {
        get;
        set;
    }

    /// <summary>
    /// コンストラクタ。リポジトリ、対象コミット、メッセージ、モードを受け取る。
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
    /// Squash/Fixup操作を実行する。
    /// ステージされた変更がある場合は自動スタッシュし、ソフトリセット後にamendコミットを行う。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        // DropHead/Reword と同様に進捗文言をローカライズする（旧来は英語の直書きだった）。
        ProgressDescription = App.Text(IsFixupMode ? "Progress.Fixup" : "Progress.Squashing");

        var log = _repo.CreateLog(IsFixupMode ? "Fixup" : "Squash");
        Use(log);
        var autoStash = new Commands.Stash(_repo.FullPath).Use(log);

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
            succ = await autoStash.PushAutoAsync(IsFixupMode ? "FIXUP_AUTO_STASH" : "SQUASH_AUTO_STASH");
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

        if (needAutoStash)
            await autoStash.RestoreAutoAsync(_repo.GitDir);

        if (succ && ForcePushAfterDone)
        {
            var branch = _repo.CurrentBranch;
            var target = branch is null ? null : await ResolveForcePushTargetAsync(_repo.FullPath, branch.Name, branch.Upstream);
            if (branch is not null && target is { } t)
            {
                succ = await new Commands.Push(_repo.FullPath, branch.Name, t.Remote, t.Branch, false, false, false, true)
                    .Use(log)
                    .ExecAsync();
            }
            else
            {
                // squash/fixup 自体は完了しているので Popup は閉じる（succ は true のまま）。
                // 推測した先へ force push しないことを利用者に伝える。
                App.RaiseException(_repo.FullPath, App.Text("SquashOrFixup.ForcePushSkipped", branch?.Name ?? "HEAD"));
            }
        }

        log.Complete();
        return succ;
    }

    /// <summary>
    /// force push の送信先 remote / ブランチ名を git config から確定する。
    /// upstream の参照名（refs/remotes/foo/bar/baz）を最初の '/' で分割すると、remote 名に '/' を含む場合
    /// （remote "foo/bar" のブランチ "baz"）に別の remote "foo" へ force push してしまう。
    /// そこで Push.cs と同様に branch.&lt;name&gt;.remote と branch.&lt;name&gt;.merge を読み、
    /// それから組み立てた refs/remotes/{remote}/{branch} が upstream と一致する場合だけ採用する。
    /// 一致しない・読めない場合は null（push しない）。force push は破壊的なので推測では送らない。
    /// </summary>
    internal static async Task<(string Remote, string Branch)?> ResolveForcePushTargetAsync(string repo, string branchName, string upstreamRef)
    {
        if (string.IsNullOrEmpty(branchName) || string.IsNullOrEmpty(upstreamRef))
            return null;

        var remote = await new Commands.Config(repo).GetAsync($"branch.{branchName}.remote");
        var merge = await new Commands.Config(repo).GetAsync($"branch.{branchName}.merge");
        const string headsPrefix = "refs/heads/";
        if (string.IsNullOrEmpty(remote) || remote == "." || string.IsNullOrEmpty(merge) ||
            !merge.StartsWith(headsPrefix, System.StringComparison.Ordinal))
            return null;

        var remoteBranch = merge[headsPrefix.Length..];
        if (string.IsNullOrEmpty(remoteBranch))
            return null;

        if (!string.Equals($"refs/remotes/{remote}/{remoteBranch}", upstreamRef, System.StringComparison.Ordinal))
            return null;

        return (remote, remoteBranch);
    }

    private readonly Repository _repo;
    private string _message;
}
