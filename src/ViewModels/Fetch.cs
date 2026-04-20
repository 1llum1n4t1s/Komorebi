using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// git fetchを実行するダイアログViewModel。
/// 単一リモートまたは全リモートからのフェッチに対応し、タグ除外と強制フェッチのオプションを提供する。
/// </summary>
public class Fetch : Popup
{
    /// <summary>
    /// リポジトリに設定されているリモート一覧。
    /// </summary>
    public List<Models.Remote> Remotes
    {
        get => _repo.Remotes;
    }

    /// <summary>
    /// 「全リモートをフェッチ」オプションを表示するかどうか（リモートが2つ以上の場合）。
    /// </summary>
    public bool IsFetchAllRemoteVisible
    {
        get;
    }

    /// <summary>
    /// 全リモートからフェッチするかどうか。UI状態に永続化される。
    /// </summary>
    public bool FetchAllRemotes
    {
        get => _fetchAllRemotes;
        set
        {
            if (SetProperty(ref _fetchAllRemotes, value) && IsFetchAllRemoteVisible)
                _repo.UIStates.FetchAllRemotes = value;
        }
    }

    /// <summary>
    /// フェッチ対象として選択されたリモート。
    /// </summary>
    public Models.Remote SelectedRemote
    {
        get;
        set;
    }

    /// <summary>
    /// タグをフェッチしないかどうか。UI状態に永続化される。
    /// </summary>
    public bool NoTags
    {
        get => _repo.UIStates.FetchWithoutTags;
        set => _repo.UIStates.FetchWithoutTags = value;
    }

    /// <summary>
    /// 強制フェッチを有効にするかどうか。UI状態に永続化される。
    /// </summary>
    public bool Force
    {
        get => _repo.UIStates.EnableForceOnFetch;
        set => _repo.UIStates.EnableForceOnFetch = value;
    }

    /// <summary>
    /// コンストラクタ。指定リモートまたはデフォルトリモートを初期選択する。
    /// </summary>
    public Fetch(Repository repo, Models.Remote preferredRemote = null)
    {
        _repo = repo;
        IsFetchAllRemoteVisible = repo.Remotes.Count > 1 && preferredRemote is null;
        _fetchAllRemotes = IsFetchAllRemoteVisible && _repo.UIStates.FetchAllRemotes;

        if (preferredRemote is not null)
        {
            SelectedRemote = preferredRemote;
        }
        else if (!string.IsNullOrEmpty(_repo.Settings.DefaultRemote))
        {
            var def = _repo.FindRemoteByName(_repo.Settings.DefaultRemote);
            SelectedRemote = def ?? _repo.Remotes[0];
        }
        else
        {
            SelectedRemote = _repo.Remotes[0];
        }
    }

    /// <summary>
    /// フェッチを実行する確認アクション。
    /// フェッチ後、現在HEADを表示中の場合はアップストリームHEADへナビゲートする。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();

        // 現在のHEADコミットを表示中かどうかを記憶
        // Histories タブがアクティブかつ現在のHEADコミットを選択中の場合のみ自動ナビゲート
        var navigateToUpstreamHEAD = _repo.SelectedViewIndex == 0
            && _repo.HistoriesVM?.SelectedCommit?.IsCurrentHead == true;
        var notags = _repo.UIStates.FetchWithoutTags;
        var force = _repo.UIStates.EnableForceOnFetch;
        var log = _repo.CreateLog("Fetch");
        Use(log);

        if (FetchAllRemotes)
        {
            foreach (var remote in _repo.Remotes)
                await new Commands.Fetch(_repo.FullPath, remote.Name, notags, force)
                    .Use(log)
                    .RunAsync();
        }
        else
        {
            await new Commands.Fetch(_repo.FullPath, SelectedRemote.Name, notags, force)
                .Use(log)
                .RunAsync();
        }

        log.Complete();

        // フェッチ後、アップストリームHEADへ自動ナビゲート
        if (navigateToUpstreamHEAD)
        {
            var upstream = _repo.CurrentBranch?.Upstream;
            if (!string.IsNullOrEmpty(upstream))
            {
                var upstreamHead = await new Commands.QueryRevisionByRefName(_repo.FullPath, upstream[13..]).GetResultAsync();
                _repo.NavigateToCommit(upstreamHead, true);
            }
        }

        return true;
    }

    private readonly Repository _repo = null; // 対象リポジトリ
    private bool _fetchAllRemotes = false; // 全リモートフェッチフラグ
}
