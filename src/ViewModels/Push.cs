using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// ローカルブランチをリモートにプッシュするダイアログのViewModel。
/// ローカルブランチ、リモート、リモートブランチの選択、追跡設定、
/// 強制プッシュ、タグプッシュ、サブモジュールチェックなどのオプションを管理する。
/// </summary>
public class Push : Popup
{
    /// <summary>
    /// 特定のローカルブランチが指定されて開かれたかどうか。
    /// </summary>
    public bool HasSpecifiedLocalBranch
    {
        get;
        private set;
    }

    /// <summary>
    /// プッシュ元のローカルブランチ（必須入力）。変更時にリモートブランチを自動選択する。
    /// </summary>
    [Required(ErrorMessage = "Local branch is required!!!")]
    public Models.Branch SelectedLocalBranch
    {
        get => _selectedLocalBranch;
        set
        {
            if (SetProperty(ref _selectedLocalBranch, value, true))
                AutoSelectBranchByRemote();
        }
    }

    /// <summary>
    /// ローカルブランチ一覧。
    /// </summary>
    public List<Models.Branch> LocalBranches
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
    /// プッシュ先のリモート（必須入力）。変更時にリモートブランチを自動選択する。
    /// </summary>
    [Required(ErrorMessage = "Remote is required!!!")]
    public Models.Remote SelectedRemote
    {
        get => _selectedRemote;
        set
        {
            if (SetProperty(ref _selectedRemote, value, true))
                AutoSelectBranchByRemote();
        }
    }

    /// <summary>
    /// 選択されたリモートに属するブランチ一覧。
    /// </summary>
    public List<Models.Branch> RemoteBranches
    {
        get => _remoteBranches;
        private set => SetProperty(ref _remoteBranches, value);
    }

    /// <summary>
    /// プッシュ先のリモートブランチ（必須入力）。変更時に追跡設定の表示を更新する。
    /// </summary>
    [Required(ErrorMessage = "Remote branch is required!!!")]
    public Models.Branch SelectedRemoteBranch
    {
        get => _selectedRemoteBranch;
        set
        {
            if (SetProperty(ref _selectedRemoteBranch, value, true))
                IsSetTrackOptionVisible = value is not null && _selectedLocalBranch.Upstream != value.FullName;
        }
    }

    /// <summary>
    /// 追跡設定オプションを表示するかどうか。
    /// 選択されたリモートブランチがアップストリームと異なる場合に表示する。
    /// </summary>
    public bool IsSetTrackOptionVisible
    {
        get => _isSetTrackOptionVisible;
        private set => SetProperty(ref _isSetTrackOptionVisible, value);
    }

    /// <summary>
    /// プッシュ後にリモートブランチを追跡ブランチとして設定するかどうか。
    /// </summary>
    public bool Tracking
    {
        get => _tracking;
        set => SetProperty(ref _tracking, value);
    }

    /// <summary>
    /// サブモジュールチェックオプションを表示するかどうか。サブモジュールが存在する場合にtrue。
    /// </summary>
    public bool IsCheckSubmodulesVisible
    {
        get => _repo.Submodules.Count > 0;
    }

    /// <summary>
    /// プッシュ前にサブモジュールをチェックするかどうか。
    /// </summary>
    public bool CheckSubmodules
    {
        get;
        set;
    } = true;

    /// <summary>
    /// すべてのタグもプッシュするかどうか。リポジトリのUI状態と連動する。
    /// </summary>
    public bool PushAllTags
    {
        get => _repo.UIStates.PushAllTags;
        set => _repo.UIStates.PushAllTags = value;
    }

    /// <summary>
    /// 強制プッシュを行うかどうか。
    /// </summary>
    public bool ForcePush
    {
        get;
        set;
    }

    /// <summary>
    /// リポジトリとオプションのローカルブランチを指定してダイアログを初期化する。
    /// ローカルブランチ一覧を構築し、アップストリームに基づいてリモートとブランチを自動選択する。
    /// </summary>
    public Push(Repository repo, Models.Branch localBranch)
    {
        _repo = repo;

        // すべてのローカルブランチを収集し、現在のブランチを特定
        LocalBranches = new List<Models.Branch>();
        Models.Branch current = null;
        foreach (var branch in _repo.Branches)
        {
            if (branch.IsLocal)
                LocalBranches.Add(branch);
            if (branch.IsCurrent)
                current = branch;
        }

        // Set default selected local branch.
        if (localBranch is not null)
        {
            if (LocalBranches.Count == 0)
                LocalBranches.Add(localBranch);

            _selectedLocalBranch = localBranch;
            HasSpecifiedLocalBranch = true;
        }
        else
        {
            _selectedLocalBranch = current;
            HasSpecifiedLocalBranch = false;
        }

        // Find preferred remote if selected local branch has upstream.
        if (!string.IsNullOrEmpty(_selectedLocalBranch?.Upstream) && !_selectedLocalBranch.IsUpstreamGone)
        {
            _tracking = false;

            foreach (var branch in repo.Branches)
            {
                if (!branch.IsLocal && _selectedLocalBranch.Upstream == branch.FullName)
                {
                    _selectedRemote = repo.Remotes.Find(x => x.Name == branch.Remote);
                    break;
                }
            }
        }
        else
        {
            _tracking = true;
        }

        // Set default remote to the first if it has not been set.
        if (_selectedRemote is null)
        {
            Models.Remote remote = null;
            if (!string.IsNullOrEmpty(_repo.Settings.DefaultRemote))
                remote = repo.Remotes.Find(x => x.Name == _repo.Settings.DefaultRemote);

            _selectedRemote = remote ?? repo.Remotes[0];
        }

        // Auto select preferred remote branch.
        AutoSelectBranchByRemote();
    }

    /// <summary>
    /// 指定された名前の新しいリモートブランチにプッシュする。
    /// 既存のブランチが見つかればそれを選択し、なければ仮のブランチを作成して選択する。
    /// </summary>
    public void PushToNewBranch(string name)
    {
        var exist = _remoteBranches.Find(x => x.Name.Equals(name, StringComparison.Ordinal));
        if (exist is not null)
        {
            SelectedRemoteBranch = exist;
            return;
        }

        var fake = new Models.Branch()
        {
            Name = name,
            Remote = _selectedRemote.Name,
        };
        var collection = new List<Models.Branch>();
        collection.AddRange(_remoteBranches);
        collection.Add(fake);
        RemoteBranches = collection;
        SelectedRemoteBranch = fake;
    }

    /// <summary>
    /// リモートブランチが既存（HEADあり）の場合にのみ直接実行可能とする。
    /// 新規ブランチの場合は確認ダイアログを表示する。
    /// </summary>
    public override bool CanStartDirectly()
    {
        return !string.IsNullOrEmpty(_selectedRemoteBranch?.Head);
    }

    /// <summary>
    /// プッシュを実行する。
    /// ローカルブランチをリモートブランチにプッシュし、オプションで追跡設定も行う。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();

        var remoteBranchName = _selectedRemoteBranch.Name;
        ProgressDescription = App.Text("Progress.Pushing", _selectedLocalBranch.Name, _selectedRemote.Name, remoteBranchName);

        var log = _repo.CreateLog("Push");
        Use(log);

        var succ = await new Commands.Push(
            _repo.FullPath,
            _selectedLocalBranch.Name,
            _selectedRemote.Name,
            remoteBranchName,
            PushAllTags,
            _repo.Submodules.Count > 0 && CheckSubmodules,
            _isSetTrackOptionVisible && _tracking,
            ForcePush).Use(log).RunAsync();

        log.Complete();
        return succ;
    }

    /// <summary>
    /// 選択されたリモートに基づいてリモートブランチを自動選択する。
    /// アップストリーム、同名ブランチの順に検索し、見つからない場合は仮のブランチを作成する。
    /// </summary>
    private void AutoSelectBranchByRemote()
    {
        // 選択されたリモートに属するブランチを収集
        var branches = new List<Models.Branch>();
        foreach (var branch in _repo.Branches)
        {
            if (branch.Remote == _selectedRemote.Name)
                branches.Add(branch);
        }

        // If selected local branch has upstream. Try to find it in current remote branches.
        if (!string.IsNullOrEmpty(_selectedLocalBranch.Upstream))
        {
            foreach (var branch in branches)
            {
                if (_selectedLocalBranch.Upstream == branch.FullName)
                {
                    RemoteBranches = branches;
                    SelectedRemoteBranch = branch;
                    return;
                }
            }
        }

        // Try to find a remote branch with the same name of selected local branch.
        foreach (var branch in branches)
        {
            if (_selectedLocalBranch.Name == branch.Name)
            {
                RemoteBranches = branches;
                SelectedRemoteBranch = branch;
                return;
            }
        }

        // Add a fake new branch.
        var fake = new Models.Branch()
        {
            Name = _selectedLocalBranch.Name,
            Remote = _selectedRemote.Name,
        };
        branches.Add(fake);
        RemoteBranches = branches;
        SelectedRemoteBranch = fake;
    }

    /// <summary>対象リポジトリ</summary>
    private readonly Repository _repo = null;
    /// <summary>選択されたローカルブランチ</summary>
    private Models.Branch _selectedLocalBranch = null;
    /// <summary>選択されたリモート</summary>
    private Models.Remote _selectedRemote = null;
    /// <summary>リモートブランチ一覧</summary>
    private List<Models.Branch> _remoteBranches = [];
    /// <summary>選択されたリモートブランチ</summary>
    private Models.Branch _selectedRemoteBranch = null;
    /// <summary>追跡設定オプションの表示フラグ</summary>
    private bool _isSetTrackOptionVisible = false;
    /// <summary>追跡設定を行うかどうか</summary>
    private bool _tracking = true;
}
