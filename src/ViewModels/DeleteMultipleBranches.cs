using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// 複数のブランチを一括削除するためのダイアログViewModel。
/// ローカルブランチとリモートブランチの両方に対応する。
/// </summary>
public class DeleteMultipleBranches : Popup
{
    /// <summary>
    /// 削除対象のブランチリスト。
    /// </summary>
    public List<Models.Branch> Targets
    {
        get;
    }

    /// <summary>
    /// コンストラクタ。対象リポジトリ、ブランチリスト、ローカルかどうかを指定する。
    /// </summary>
    public DeleteMultipleBranches(Repository repo, List<Models.Branch> branches, bool isLocal)
    {
        _repo = repo;
        _isLocal = isLocal;
        Targets = branches;
    }

    /// <summary>
    /// 複数ブランチの一括削除を実行する確認アクション。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.DeletingMultipleBranches");

        var log = _repo.CreateLog("Delete Multiple Branches");
        Use(log);

        // ローカルブランチの削除（独立した操作なので並列実行）
        if (_isLocal)
        {
            List<Task> tasks = [];
            foreach (var target in Targets)
                tasks.Add(new Commands.Branch(_repo.FullPath, target.Name)
                    .Use(log)
                    .DeleteLocalAsync());
            await Task.WhenAll(tasks);
        }
        else
        {
            List<Task> tasks = [];
            foreach (var target in Targets)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var exists = await new Commands.Remote(_repo.FullPath).HasBranchAsync(target.Remote, target.Name);
                    if (exists)
                        await new Commands.Push(_repo.FullPath, target.Remote, $"refs/heads/{target.Name}", true)
                            .Use(log)
                            .RunAsync();
                    else
                        await new Commands.Branch(_repo.FullPath, target.Name)
                            .Use(log)
                            .DeleteRemoteAsync(target.Remote);
                }));
            }
            await Task.WhenAll(tasks);
        }

        log.Complete();
        _repo.MarkBranchesDirtyManually();
        return true;
    }

    private Repository _repo = null;
    private bool _isLocal = false;
}
