using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// 新しいブランチを作成するためのダイアログViewModel。
/// ブランチ名のバリデーション、作成後のチェックアウト、ローカル変更の自動スタッシュなどを管理する。
/// </summary>
public class CreateBranch : Popup
{
    /// <summary>
    /// 作成するブランチの名前。
    /// 必須入力で、正規表現による書式チェックと既存ブランチとの重複チェックを行う。
    /// </summary>
    [Required(ErrorMessage = "Branch name is required!")]
    [RegularExpression(@"^[\w\-/\.#\+]+$", ErrorMessage = "Bad branch name format!")]
    [CustomValidation(typeof(CreateBranch), nameof(ValidateBranchName))]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, true);
    }

    /// <summary>
    /// ブランチ作成の基点となるオブジェクト（ブランチ、コミット、またはタグ）。
    /// </summary>
    public object BasedOn
    {
        get;
    }

    /// <summary>
    /// ローカル変更があるかどうか。
    /// </summary>
    public bool HasLocalChanges
    {
        get => _repo.LocalChangesCount > 0;
    }

    /// <summary>
    /// ローカル変更の扱い方。
    /// </summary>
    public Models.DealWithLocalChanges DealWithLocalChanges
    {
        get;
        set;
    }

    /// <summary>
    /// ブランチ作成後に自動的にチェックアウトするかどうか。
    /// リポジトリのUI状態に永続化される。
    /// </summary>
    public bool CheckoutAfterCreated
    {
        get => _repo.UIStates.CheckoutBranchOnCreateBranch;
        set
        {
            if (_repo.UIStates.CheckoutBranchOnCreateBranch != value)
            {
                _repo.UIStates.CheckoutBranchOnCreateBranch = value;
                OnPropertyChanged();
                UpdateOverrideTip();
            }
        }
    }

    /// <summary>
    /// ブランチ作成後にリモートへプッシュするかどうか。
    /// リポジトリのUI状態に永続化される。
    /// </summary>
    public bool PushAfterCreated
    {
        get => _repo.UIStates.PushToRemoteWhenCreateBranch;
        set
        {
            if (_repo.UIStates.PushToRemoteWhenCreateBranch != value)
            {
                _repo.UIStates.PushToRemoteWhenCreateBranch = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// ベアリポジトリかどうか。ベアリポジトリではチェックアウト不可。
    /// </summary>
    public bool IsBareRepository
    {
        get => _repo.IsBare;
    }

    /// <summary>
    /// 上書き時のgitコマンドのヒント表示。
    /// </summary>
    public string OverrideTip
    {
        get => _overrideTip;
        private set => SetProperty(ref _overrideTip, value);
    }

    /// <summary>
    /// 既存の同名ブランチの上書きを許可するかどうか。
    /// 変更時にブランチ名のバリデーションを再実行する。
    /// </summary>
    public bool AllowOverwrite
    {
        get => _allowOverwrite;
        set
        {
            if (SetProperty(ref _allowOverwrite, value))
                ValidateProperty(_name, nameof(Name));
        }
    }

    /// <summary>
    /// ブランチを基点として新しいブランチを作成するコンストラクタ。
    /// リモートブランチの場合、その名前をデフォルト名として設定する。
    /// </summary>
    public CreateBranch(Repository repo, Models.Branch branch)
    {
        _repo = repo;
        _baseOnRevision = branch.Head;

        if (!branch.IsLocal)
            Name = branch.Name;

        BasedOn = branch;
        DealWithLocalChanges = Models.DealWithLocalChanges.DoNothing;
        UpdateOverrideTip();
    }

    /// <summary>
    /// コミットを基点として新しいブランチを作成するコンストラクタ。
    /// </summary>
    public CreateBranch(Repository repo, Models.Commit commit)
    {
        _repo = repo;
        _baseOnRevision = commit.SHA;

        BasedOn = commit;
        DealWithLocalChanges = Models.DealWithLocalChanges.DoNothing;
        UpdateOverrideTip();
    }

    /// <summary>
    /// タグを基点として新しいブランチを作成するコンストラクタ。
    /// </summary>
    public CreateBranch(Repository repo, Models.Tag tag)
    {
        _repo = repo;
        _baseOnRevision = tag.SHA;

        BasedOn = tag;
        DealWithLocalChanges = Models.DealWithLocalChanges.DoNothing;
        UpdateOverrideTip();
    }

    /// <summary>
    /// ブランチ名の重複を検証するカスタムバリデーション。
    /// 上書きが許可されていない場合、同名ブランチの存在をチェックする。
    /// </summary>
    public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is CreateBranch creator)
        {
            if (!creator._allowOverwrite)
            {
                foreach (var b in creator._repo.Branches)
                {
                    if (b.FriendlyName.Equals(name, StringComparison.Ordinal))
                        return new ValidationResult("A branch with same name already exists!");
                }
            }

            return ValidationResult.Success;
        }

        return new ValidationResult("Missing runtime context to create branch!");
    }

    /// <summary>
    /// ブランチ作成を実行する確認アクション。
    /// チェックアウトが有効な場合、ローカル変更の自動スタッシュ、サブモジュール更新、上流ブランチ設定も行う。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();

        var log = _repo.CreateLog($"Create Branch '{_name}'");
        Use(log);

        // チェックアウト予定の場合、デタッチHEAD状態でコミットが失われないか確認
        if (CheckoutAfterCreated)
        {
            if (_repo.CurrentBranch is { IsDetachedHead: true } && !_repo.CurrentBranch.Head.Equals(_baseOnRevision, StringComparison.Ordinal))
            {
                var refs = await new Commands.QueryRefsContainsCommit(_repo.FullPath, _repo.CurrentBranch.Head).GetResultAsync();
                if (refs.Count == 0)
                {
                    var msg = App.Text("Checkout.WarnLostCommits");
                    var shouldContinue = await App.AskConfirmAsync(msg);
                    if (!shouldContinue)
                        return true;
                }
            }
        }

        bool succ;
        // チェックアウト付きブランチ作成（ベアリポジトリ以外）
        if (CheckoutAfterCreated && !_repo.IsBare)
        {
            // ローカル変更がある場合の処理
            var needPopStash = false;
            if (DealWithLocalChanges == Models.DealWithLocalChanges.StashAndReapply)
            {
                var changes = await new Commands.CountLocalChanges(_repo.FullPath, false).GetResultAsync();
                if (changes > 0)
                {
                    succ = await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PushAsync("CREATE_BRANCH_AUTO_STASH", false);
                    if (!succ)
                    {
                        log.Complete();
                        return false;
                    }

                    needPopStash = true;
                }
            }

            var forceDiscard = DealWithLocalChanges == Models.DealWithLocalChanges.Discard;
            succ = await new Commands.Checkout(_repo.FullPath)
                .Use(log)
                .BranchAsync(_name, _baseOnRevision, forceDiscard, _allowOverwrite);

            if (succ)
            {
                await _repo.AutoUpdateSubmodulesAsync(log);

                if (needPopStash)
                    await new Commands.Stash(_repo.FullPath)
                        .Use(log)
                        .PopAsync("stash@{0}");
            }
        }
        else
        {
            succ = await new Commands.Branch(_repo.FullPath, _name)
                .Use(log)
                .CreateAsync(_baseOnRevision, _allowOverwrite);
        }

        // リモートブランチと同名のローカルブランチを作成した場合、上流ブランチを自動設定
        if (succ && BasedOn is Models.Branch { IsLocal: false } basedOn && _name.Equals(basedOn.Name, StringComparison.Ordinal))
        {
            await new Commands.Branch(_repo.FullPath, _name)
                    .Use(log)
                    .SetUpstreamAsync(basedOn);
        }

        // 作成したブランチをリモートにプッシュ
        if (succ && PushAfterCreated && _repo.Remotes.Count > 0)
        {
            Models.Remote remote = null;
            if (!string.IsNullOrEmpty(_repo.Settings.DefaultRemote))
                remote = _repo.Remotes.Find(x => x.Name == _repo.Settings.DefaultRemote);
            remote ??= _repo.Remotes[0];

            await new Commands.Push(_repo.FullPath, _name, remote.Name, _name, false, false, true, false)
                .Use(log)
                .RunAsync();
        }

        log.Complete();

        // チェックアウト後、サイドバーのブランチツリーノードを展開しフィルタを設定
        if (succ && CheckoutAfterCreated)
        {
            var fake = new Models.Branch() { IsLocal = true, FullName = $"refs/heads/{_name}" };
            if (BasedOn is Models.Branch { IsLocal: false } based)
                fake.Upstream = based.FullName;

            var folderEndIdx = fake.FullName.LastIndexOf('/');
            if (folderEndIdx > 10)
                _repo.UIStates.ExpandedBranchNodesInSideBar.Add(fake.FullName[..folderEndIdx]);

            if (_repo.HistoryFilterMode == Models.FilterMode.Included)
                _repo.SetBranchFilterMode(fake, Models.FilterMode.Included, false, false);
        }

        _repo.MarkBranchesDirtyManually();

        if (CheckoutAfterCreated)
        {
            ProgressDescription = App.Text("Progress.WaitingBranchUpdate");
            await Task.Delay(400);
        }

        return true;
    }

    /// <summary>
    /// 上書き時のヒント文字列を更新する。
    /// </summary>
    private void UpdateOverrideTip()
    {
        OverrideTip = CheckoutAfterCreated ? "-B in `git checkout`" : "-f in `git branch`";
    }

    private readonly Repository _repo = null;       // 対象リポジトリ
    private string _name = null;                     // 新しいブランチ名
    private readonly string _baseOnRevision = null;  // 基点となるリビジョンSHA
    private bool _allowOverwrite = false;            // 上書き許可フラグ
    private string _overrideTip = "-B";              // 上書きヒント文字列
}
