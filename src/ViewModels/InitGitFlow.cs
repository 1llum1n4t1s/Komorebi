// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// Git Flowの初期化ダイアログのViewModel。
/// master/develop/feature/release/hotfix/tagの各プレフィックスを設定してGit Flowを構成する。
/// </summary>
public partial class InitGitFlow : Popup
{
    /// <summary>タグプレフィックスのバリデーション用正規表現。</summary>
    [GeneratedRegex(@"^[\w\-/\.]+$")]
    private static partial Regex REG_TAG_PREFIX();

    /// <summary>masterブランチ名。バリデーション付き。</summary>
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.MasterBranchRequired))]
    [CustomValidation(typeof(InitGitFlow), nameof(ValidateBaseBranch))]
    public string Master
    {
        get => _master;
        set
        {
            if (SetProperty(ref _master, value, true))
            {
                // upstreamとの差分: 名前の重複は両方の入力に依存する。
                ValidateProperty(_develop, nameof(Develop));
            }
        }
    }

    /// <summary>developブランチ名。バリデーション付き。</summary>
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.DevelopBranchRequired))]
    [CustomValidation(typeof(InitGitFlow), nameof(ValidateBaseBranch))]
    public string Develop
    {
        get => _develop;
        set
        {
            if (SetProperty(ref _develop, value, true))
                ValidateProperty(_master, nameof(Master));
        }
    }

    /// <summary>featureブランチのプレフィックス（例: "feature/"）。</summary>
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FeaturePrefixRequired))]
    [RegularExpression(@"^[\w\-\.]+/$", ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FeaturePrefixInvalid))]
    public string FeaturePrefix
    {
        get => _featurePrefix;
        set => SetProperty(ref _featurePrefix, value, true);
    }

    /// <summary>releaseブランチのプレフィックス（例: "release/"）。</summary>
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.ReleasePrefixRequired))]
    [RegularExpression(@"^[\w\-\.]+/$", ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.ReleasePrefixInvalid))]
    public string ReleasePrefix
    {
        get => _releasePrefix;
        set => SetProperty(ref _releasePrefix, value, true);
    }

    /// <summary>hotfixブランチのプレフィックス（例: "hotfix/"）。</summary>
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.HotfixPrefixRequired))]
    [RegularExpression(@"^[\w\-\.]+/$", ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.HotfixPrefixInvalid))]
    public string HotfixPrefix
    {
        get => _hotfixPrefix;
        set => SetProperty(ref _hotfixPrefix, value, true);
    }

    /// <summary>タグのプレフィックス（任意）。</summary>
    [CustomValidation(typeof(InitGitFlow), nameof(ValidateTagPrefix))]
    public string TagPrefix
    {
        get => _tagPrefix;
        set => SetProperty(ref _tagPrefix, value, true);
    }

    /// <summary>
    /// コンストラクタ。ローカルブランチからmasterブランチ名の初期値を自動判定する。
    /// </summary>
    public InitGitFlow(Repository repo)
    {
        _repo = repo;

        List<string> localBranches = [];
        foreach (var branch in repo.Branches)
        {
            if (branch.IsLocal)
                localBranches.Add(branch.Name);
        }

        if (localBranches.Contains("master"))
            _master = "master";
        else if (localBranches.Contains("main"))
            _master = "main";
        else if (localBranches.Count > 0)
            _master = localBranches[0];
        else
            _master = "master";
    }

    /// <summary>
    /// master/developブランチ名の重複チェックバリデーション。
    /// </summary>
    public static ValidationResult ValidateBaseBranch(string name, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is InitGitFlow initializer)
        {
            if (!Models.RefName.IsValidBranchName(name))
                return new ValidationResult(App.Text("Validation.BranchNameInvalid"));

            if (initializer._master == initializer._develop)
                return new ValidationResult(App.Text("Validation.GitFlowBranchesEqual"));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// タグプレフィックスの形式チェックバリデーション（空の場合は許可）。
    /// </summary>
    public static ValidationResult ValidateTagPrefix(string tagPrefix, ValidationContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(tagPrefix) && !REG_TAG_PREFIX().IsMatch(tagPrefix))
            return new ValidationResult(App.Text("Validation.TagPrefixInvalid"));

        return ValidationResult.Success;
    }

    /// <summary>
    /// 確認ボタン押下時の処理。必要なブランチを作成してからGit Flowを初期化する。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.InitGitFlow");

        var log = _repo.CreateLog("Gitflow - Init");
        Use(log);

        bool succ;
        var current = _repo.CurrentBranch;

        // masterブランチが存在しなければ現在のHEADから作成
        var masterBranch = _repo.FindLocalBranchByName(_master);
        if (masterBranch is null)
        {
            succ = await new Commands.Branch(_repo.FullPath, _master)
                .Use(log)
                .CreateAsync(current.Head, true);
            if (!succ)
            {
                log.Complete();
                return false;
            }
        }

        // developブランチが存在しなければ現在のHEADから作成
        var developBranch = _repo.FindLocalBranchByName(_develop);
        if (developBranch is null)
        {
            succ = await new Commands.Branch(_repo.FullPath, _develop)
                .Use(log)
                .CreateAsync(current.Head, true);
            if (!succ)
            {
                log.Complete();
                return false;
            }
        }

        succ = await Commands.GitFlow.InitAsync(
            _repo.FullPath,
            _master,
            _develop,
            _featurePrefix,
            _releasePrefix,
            _hotfixPrefix,
            _tagPrefix,
            log);

        log.Complete();

        if (succ)
        {
            var gitflow = new Models.GitFlow();
            gitflow.Master = _master;
            gitflow.Develop = _develop;
            gitflow.FeaturePrefix = _featurePrefix;
            gitflow.ReleasePrefix = _releasePrefix;
            gitflow.HotfixPrefix = _hotfixPrefix;
            _repo.GitFlow = gitflow;
        }

        return succ;
    }

    private readonly Repository _repo; // 対象リポジトリ
    private string _master; // masterブランチ名
    private string _develop = "develop"; // developブランチ名
    private string _featurePrefix = "feature/"; // featureプレフィックス
    private string _releasePrefix = "release/"; // releaseプレフィックス
    private string _hotfixPrefix = "hotfix/"; // hotfixプレフィックス
    private string _tagPrefix = string.Empty; // タグプレフィックス
}
