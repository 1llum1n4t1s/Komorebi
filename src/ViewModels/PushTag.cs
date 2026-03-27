using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// タグをリモートにプッシュするダイアログのViewModel。
/// 単一リモートまたはすべてのリモートへのプッシュに対応する。
/// </summary>
public class PushTag : Popup
{
    /// <summary>
    /// プッシュ対象のタグ。
    /// </summary>
    public Models.Tag Target
    {
        get;
    }

    /// <summary>
    /// リモート一覧。
    /// </summary>
    public List<Models.Remote> Remotes
    {
        get => _repo.Remotes;
    }

    /// <summary>
    /// 選択されたプッシュ先リモート。
    /// </summary>
    public Models.Remote SelectedRemote
    {
        get;
        set;
    }

    /// <summary>
    /// すべてのリモートにプッシュするかどうか。
    /// </summary>
    public bool PushAllRemotes
    {
        get => _pushAllRemotes;
        set => SetProperty(ref _pushAllRemotes, value);
    }

    /// <summary>
    /// リポジトリとタグを指定してダイアログを初期化する。
    /// デフォルトのリモートとして最初のリモートを選択する。
    /// </summary>
    public PushTag(Repository repo, Models.Tag target)
    {
        _repo = repo;
        Target = target;
        SelectedRemote = _repo.Remotes[0];
    }

    /// <summary>
    /// タグのプッシュを実行する。
    /// すべてのリモートへのプッシュが選択されている場合は順次実行し、
    /// いずれかが失敗した時点で中断する。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.PushingTag");

        var log = _repo.CreateLog("Push Tag");
        Use(log);

        var succ = true;
        var tag = $"refs/tags/{Target.Name}";
        if (_pushAllRemotes)
        {
            // すべてのリモートに順次プッシュ
            foreach (var remote in _repo.Remotes)
            {
                succ = await new Commands.Push(_repo.FullPath, remote.Name, tag, false)
                    .Use(log)
                    .RunAsync();
                if (!succ)
                    break;
            }
        }
        else
        {
            // 選択されたリモートのみにプッシュ
            succ = await new Commands.Push(_repo.FullPath, SelectedRemote.Name, tag, false)
                .Use(log)
                .RunAsync();
        }

        log.Complete();
        return succ;
    }

    /// <summary>対象リポジトリ</summary>
    private readonly Repository _repo = null;
    /// <summary>すべてのリモートにプッシュするフラグ</summary>
    private bool _pushAllRemotes = false;
}
