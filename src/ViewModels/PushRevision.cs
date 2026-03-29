using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// 特定のリビジョン（コミット）をリモートブランチにプッシュするダイアログのViewModel。
/// コミットSHAを直接指定してプッシュする。
/// </summary>
public class PushRevision : Popup
{
    /// <summary>
    /// プッシュ対象のコミット。
    /// </summary>
    public Models.Commit Revision
    {
        get;
    }

    /// <summary>
    /// プッシュ先のリモートブランチ。
    /// </summary>
    public Models.Branch RemoteBranch
    {
        get;
    }

    /// <summary>
    /// 強制プッシュを行うかどうか。
    /// </summary>
    public bool Force
    {
        get;
        set;
    }

    /// <summary>
    /// リポジトリ、コミット、リモートブランチを指定してダイアログを初期化する。
    /// </summary>
    public PushRevision(Repository repo, Models.Commit revision, Models.Branch remoteBranch)
    {
        _repo = repo;
        Revision = revision;
        RemoteBranch = remoteBranch;
        Force = false;
    }

    /// <summary>
    /// 指定されたコミットをリモートブランチにプッシュする。
    /// コミットSHAをソースとしてgit pushを実行する。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.PushRevision", Revision.SHA[..10], RemoteBranch.FriendlyName);

        var log = _repo.CreateLog("Push Revision");
        Use(log);

        // コミットSHAを直接指定してプッシュ
        var succ = await new Commands.Push(
            _repo.FullPath,
            Revision.SHA,
            RemoteBranch.Remote,
            RemoteBranch.Name,
            false,
            false,
            false,
            Force).Use(log).RunAsync();

        log.Complete();
        return succ;
    }

    /// <summary>対象リポジトリ</summary>
    private readonly Repository _repo;
}
