using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// LFSオブジェクトのフェッチ（ダウンロード）ダイアログのViewModel。
/// 指定リモートからLFSオブジェクトを取得する。
/// </summary>
public class LFSFetch : Popup
{
    /// <summary>リポジトリのリモート一覧。</summary>
    public List<Models.Remote> Remotes => _repo.Remotes;

    /// <summary>フェッチ対象のリモート。</summary>
    public Models.Remote SelectedRemote
    {
        get;
        set;
    }

    /// <summary>コンストラクタ。最初のリモートをデフォルト選択する。</summary>
    public LFSFetch(Repository repo)
    {
        _repo = repo;
        SelectedRemote = _repo.Remotes[0];
    }

    /// <summary>確認ボタン押下時の処理。LFS fetchコマンドを実行する。</summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.LFSFetch");

        var log = _repo.CreateLog("LFS Fetch");
        Use(log);

        await new Commands.LFS(_repo.FullPath)
            .Use(log)
            .FetchAsync(SelectedRemote.Name);

        log.Complete();
        return true;
    }

    private readonly Repository _repo = null;
}
