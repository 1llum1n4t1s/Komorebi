using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// ブランチを削除するためのダイアログViewModel。
/// ローカルブランチの削除時に、追跡リモートブランチも同時に削除するオプションを提供する。
/// </summary>
public class DeleteBranch : Popup
{
    /// <summary>
    /// 削除対象のブランチ。
    /// </summary>
    public Models.Branch Target
    {
        get;
    }

    /// <summary>
    /// 追跡中のリモートブランチ（存在する場合）。
    /// </summary>
    public Models.Branch TrackingRemoteBranch
    {
        get;
    }

    /// <summary>
    /// 追跡リモートブランチ削除オプションのツールチップテキスト。
    /// </summary>
    public string DeleteTrackingRemoteTip
    {
        get;
        private set;
    }

    /// <summary>
    /// 追跡リモートブランチも同時に削除するかどうか。
    /// </summary>
    public bool AlsoDeleteTrackingRemote
    {
        get => _alsoDeleteTrackingRemote;
        set => SetProperty(ref _alsoDeleteTrackingRemote, value);
    }

    /// <summary>
    /// コンストラクタ。ローカルブランチの場合、上流の追跡リモートブランチを自動検出する。
    /// </summary>
    public DeleteBranch(Repository repo, Models.Branch branch)
    {
        _repo = repo;
        Target = branch;

        if (branch.IsLocal && !string.IsNullOrEmpty(branch.Upstream))
        {
            TrackingRemoteBranch = repo.Branches.Find(x => x.FullName == branch.Upstream);
            if (TrackingRemoteBranch is not null)
                DeleteTrackingRemoteTip = App.Text("DeleteBranch.WithTrackingRemote", TrackingRemoteBranch.FriendlyName);
        }
    }

    /// <summary>
    /// ブランチ削除を実行する確認アクション。
    /// ローカル/リモートブランチに応じた削除処理と履歴フィルタの除去を行う。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.DeletingBranch");

        var log = _repo.CreateLog("Delete Branch");
        Use(log);

        // ローカルブランチの場合
        if (Target.IsLocal)
        {
            await new Commands.Branch(_repo.FullPath, Target.Name)
                .Use(log)
                .DeleteLocalAsync();
            _repo.UIStates.RemoveHistoryFilter(Target.FullName, Models.FilterType.LocalBranch);

            if (_alsoDeleteTrackingRemote && TrackingRemoteBranch is not null)
            {
                await DeleteRemoteBranchAsync(TrackingRemoteBranch, log);
                _repo.UIStates.RemoveHistoryFilter(TrackingRemoteBranch.FullName, Models.FilterType.RemoteBranch);
            }
        }
        else
        {
            await DeleteRemoteBranchAsync(Target, log);
            _repo.UIStates.RemoveHistoryFilter(Target.FullName, Models.FilterType.RemoteBranch);
        }

        log.Complete();
        _repo.MarkBranchesDirtyManually();
        return true;
    }

    /// <summary>
    /// リモートブランチを削除する内部メソッド。
    /// リモートにブランチが存在する場合はpush --deleteで、存在しない場合はローカルの追跡参照を削除する。
    /// </summary>
    private async Task DeleteRemoteBranchAsync(Models.Branch branch, CommandLog log)
    {
        var exists = await new Commands.Remote(_repo.FullPath)
            .HasBranchAsync(branch.Remote, branch.Name)
            .ConfigureAwait(false);

        if (exists)
            await new Commands.Push(_repo.FullPath, branch.Remote, $"refs/heads/{branch.Name}", true)
                .Use(log)
                .RunAsync()
                .ConfigureAwait(false);
        else
            await new Commands.Branch(_repo.FullPath, branch.Name)
                .Use(log)
                .DeleteRemoteAsync(branch.Remote)
                .ConfigureAwait(false);
    }

    private readonly Repository _repo = null;
    private bool _alsoDeleteTrackingRemote = false;
}
