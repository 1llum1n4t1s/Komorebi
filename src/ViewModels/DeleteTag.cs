using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// タグを削除するためのダイアログViewModel。
/// ローカル削除後にリモートからも削除するオプションを提供する。
/// </summary>
public class DeleteTag : Popup
{
    /// <summary>
    /// 削除対象のタグ。
    /// </summary>
    public Models.Tag Target
    {
        get;
        private set;
    }

    /// <summary>
    /// 削除をリモートにもプッシュするかどうか。UI状態に永続化される。
    /// </summary>
    public bool PushToRemotes
    {
        get => _repo.UIStates.PushToRemoteWhenDeleteTag;
        set => _repo.UIStates.PushToRemoteWhenDeleteTag = value;
    }

    /// <summary>
    /// コンストラクタ。対象リポジトリと削除するタグを指定する。
    /// </summary>
    public DeleteTag(Repository repo, Models.Tag tag)
    {
        _repo = repo;
        Target = tag;
    }

    /// <summary>
    /// タグ削除を実行する確認アクション。
    /// ローカルタグ削除後、リモートへの削除プッシュと履歴フィルタの除去を行う。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.DeletingTag", Target.Name);

        // プッシュ先のリモート一覧を取得
        var remotes = PushToRemotes ? _repo.Remotes : [];
        var log = _repo.CreateLog("Delete Tag");
        Use(log);

        var succ = await new Commands.Tag(_repo.FullPath, Target.Name)
            .Use(log)
            .DeleteAsync();

        if (succ)
        {
            foreach (var r in remotes)
                await new Commands.Push(_repo.FullPath, r.Name, $"refs/tags/{Target.Name}", true)
                    .Use(log)
                    .RunAsync();
        }

        log.Complete();
        _repo.UIStates.RemoveHistoryFilter(Target.Name, Models.FilterType.Tag);
        _repo.MarkTagsDirtyManually();
        return succ;
    }

    private readonly Repository _repo = null;
}
