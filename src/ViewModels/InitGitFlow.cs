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
    [Required(ErrorMessage = "Master branch name is required!!!")]
    [RegularExpression(@"^[\w\-/\.]+$", ErrorMessage = "Bad branch name format!")]
    [CustomValidation(typeof(InitGitFlow), nameof(ValidateBaseBranch))]
    public string Master
    {
        get => _master;
        set => SetProperty(ref _master, value, true);
    }

    /// <summary>developブランチ名。バリデーション付き。</summary>
    [Required(ErrorMessage = "Develop branch name is required!!!")]
    [RegularExpression(@"^[\w\-/\.]+$", ErrorMessage = "Bad branch name format!")]
    [CustomValidation(typeof(InitGitFlow), nameof(ValidateBaseBranch))]
    public string Develop
    {
        get => _develop;
        set => SetProperty(ref _develop, value, true);
    }

    /// <summary>featureブランチのプレフィックス（例: "feature/"）。</summary>
    [Required(ErrorMessage = "Feature prefix is required!!!")]
    [RegularExpression(@"^[\w\-\.]+/$", ErrorMessage = "Bad feature prefix format!")]
    public string FeaturePrefix
    {
        get => _featurePrefix;
        set => SetProperty(ref _featurePrefix, value, true);
    }

    /// <summary>releaseブランチのプレフィックス（例: "release/"）。</summary>
    [Required(ErrorMessage = "Release prefix is required!!!")]
    [RegularExpression(@"^[\w\-\.]+/$", ErrorMessage = "Bad release prefix format!")]
    public string ReleasePrefix
    {
        get => _releasePrefix;
        set => SetProperty(ref _releasePrefix, value, true);
    }

    /// <summary>hotfixブランチのプレフィックス（例: "hotfix/"）。</summary>
    [Required(ErrorMessage = "Hotfix prefix is required!!!")]
    [RegularExpression(@"^[\w\-\.]+/$", ErrorMessage = "Bad hotfix prefix format!")]
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
    public static ValidationResult ValidateBaseBranch(string _, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is InitGitFlow initializer)
        {
            if (initializer._master == initializer._develop)
                return new ValidationResult("Develop branch has the same name with master branch!");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// タグプレフィックスの形式チェックバリデーション（空の場合は許可）。
    /// </summary>
    public static ValidationResult ValidateTagPrefix(string tagPrefix, ValidationContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(tagPrefix) && !REG_TAG_PREFIX().IsMatch(tagPrefix))
            return new ValidationResult("Bad tag prefix format!");

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
