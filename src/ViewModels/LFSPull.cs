using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// LFSオブジェクトのプル（ダウンロード＋チェックアウト）ダイアログのViewModel。
/// </summary>
public class LFSPull : Popup
{
    /// <summary>リポジトリのリモート一覧。</summary>
    public List<Models.Remote> Remotes => _repo.Remotes;

    /// <summary>プル対象のリモート。</summary>
    public Models.Remote SelectedRemote
    {
        get;
        set;
    }

    /// <summary>コンストラクタ。最初のリモートをデフォルト選択する。</summary>
    public LFSPull(Repository repo)
    {
        _repo = repo;
        SelectedRemote = _repo.Remotes[0];
    }

    /// <summary>確認ボタン押下時の処理。LFS pullコマンドを実行する。</summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.LFSPull");

        var log = _repo.CreateLog("LFS Pull");
        Use(log);

        await new Commands.LFS(_repo.FullPath)
            .Use(log)
            .PullAsync(SelectedRemote.Name);

        log.Complete();
        return true;
    }

    private readonly Repository _repo = null;
}
