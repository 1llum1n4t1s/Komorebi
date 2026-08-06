// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// ブランチを削除するためのダイアログViewModel。
/// ローカルブランチの削除に成功した後、同名の追跡リモートブランチが見つかった場合は
/// 事後確認ダイアログでリモートブランチも削除するかどうかを尋ねる。
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
    /// 未マージのコミットが含まれていても強制削除するかどうか（ローカルブランチのみ）。
    /// </summary>
    public bool Force
    {
        get;
        set;
    }

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    public DeleteBranch(Repository repo, Models.Branch branch)
    {
        _repo = repo;
        Target = branch;
    }

    /// <summary>
    /// ブランチ削除を実行する確認アクション。
    /// ローカル/リモートブランチに応じた削除処理と履歴フィルタの除去を行う。
    /// ローカルブランチ削除に成功した場合、同名の追跡リモートブランチが見つかれば削除するかどうかを事後確認する。
    /// </summary>
    public override async Task<bool> Sure()
    {
        ProgressDescription = App.Text("Progress.DeletingBranch");

        var log = _repo.CreateLog("Delete Branch");
        Use(log);

        var succ = false;

        // Watcher ロックは git コマンド実行範囲に限定し、MarkBranchesDirtyManually は
        // ロック解除後に呼ぶ。ロック中に呼ぶと、ロック解除後に届く FS イベントが Refresh をキャンセルする。
        using (_repo.LockWatcher())
        {
            // ローカルブランチの場合
            if (Target.IsLocal)
            {
                succ = await new Commands.Branch(_repo.FullPath, Target.Name)
                    .Use(log)
                    .DeleteLocalAsync(Force);

                if (succ)
                {
                    _repo.UIStates.RemoveHistoryFilter(Target.FullName, Models.FilterType.LocalBranch);

                    var upstream = Target.Upstream ?? string.Empty;
                    var tracking = _repo.Branches.Find(x => x.FullName.Equals(upstream, StringComparison.Ordinal));
                    if (tracking is not null && tracking.Name.Equals(Target.Name, StringComparison.Ordinal))
                    {
                        var msgBuilder = new StringBuilder();
                        msgBuilder
                            .AppendLine(App.Text("DeleteBranch.AskForRemote"))
                            .AppendLine()
                            .Append("• ").Append(tracking.FriendlyName);

                        var deleteTracking = await App.AskConfirmAsync(msgBuilder.ToString(), Models.ConfirmButtonType.YesNo);
                        if (deleteTracking)
                        {
                            succ = await DeleteRemoteBranchAsync(tracking, log);
                            if (succ)
                                _repo.UIStates.RemoveHistoryFilter(tracking.FullName, Models.FilterType.RemoteBranch);
                        }
                    }
                }
            }
            else
            {
                succ = await DeleteRemoteBranchAsync(Target, log);
                _repo.UIStates.RemoveHistoryFilter(Target.FullName, Models.FilterType.RemoteBranch);
            }
        }

        log.Complete();
        _repo.MarkBranchesDirtyManually();
        return succ;
    }

    /// <summary>
    /// リモートブランチを削除する内部メソッド。
    /// リモートにブランチが存在する場合はpush --deleteで、存在しない場合はローカルの追跡参照を削除する。
    /// </summary>
    private async Task<bool> DeleteRemoteBranchAsync(Models.Branch branch, CommandLog log)
    {
        var exists = await new Commands.Remote(_repo.FullPath)
            .HasBranchAsync(branch.Remote, branch.Name)
            .ConfigureAwait(false);

        if (exists)
            return await new Commands.Push(_repo.FullPath, branch.Remote, $"refs/heads/{branch.Name}", true)
                .Use(log)
                .RunAsync()
                .ConfigureAwait(false);
        else
            return await new Commands.Branch(_repo.FullPath, branch.Name)
                .Use(log)
                .DeleteRemoteAsync(branch.Remote)
                .ConfigureAwait(false);
    }

    private readonly Repository _repo = null;
}
