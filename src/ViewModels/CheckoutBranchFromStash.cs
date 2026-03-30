using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// 指定したスタッシュから新しいブランチを作成（チェックアウト）するダイアログViewModel。
/// 作成後、スタッシュは適用され削除される。
/// </summary>
public class CheckoutBranchFromStash : Popup
{
    /// <summary>
    /// チェックアウト元のスタッシュ。
    /// </summary>
    public Models.Stash Target
    {
        get;
    }

    /// <summary>
    /// 作成するブランチ名。
    /// </summary>
    [Required(ErrorMessage = "Branch name is required!")]
    [RegularExpression(@"^[\w\-/\.#\+]+$", ErrorMessage = "Bad branch name format!")]
    [CustomValidation(typeof(CheckoutBranchFromStash), nameof(ValidateBranchName))]
    public string BranchName
    {
        get => _branchName;
        set => SetProperty(ref _branchName, value, true);
    }

    /// <summary>
    /// コンストラクタ。対象リポジトリと元スタッシュを指定する。
    /// </summary>
    public CheckoutBranchFromStash(Repository repo, Models.Stash stash)
    {
        _repo = repo;
        Target = stash;
    }

    /// <summary>
    /// ブランチ名の検証。既存ブランチと衝突しないことを確認する。
    /// </summary>
    public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is CheckoutBranchFromStash caller)
        {
            foreach (var b in caller._repo.Branches)
            {
                if (b.FriendlyName.Equals(name, StringComparison.Ordinal))
                    return new ValidationResult("A branch with same name already exists!");
            }

            return ValidationResult.Success;
        }

        return new ValidationResult("Missing runtime context to create branch!");
    }

    /// <summary>
    /// git stash branch を実行してブランチを作成する。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = "Checkout branch from stash...";

        var log = _repo.CreateLog($"Checkout Branch '{_branchName}'");
        Use(log);

        var succ = await new Commands.Stash(_repo.FullPath)
            .Use(log)
            .CheckoutBranchAsync(Target.Name, _branchName);

        if (succ)
        {
            _repo.MarkWorkingCopyDirtyManually();
            _repo.MarkStashesDirtyManually();
        }

        log.Complete();
        return true;
    }

    private readonly Repository _repo;
    private string _branchName = string.Empty;
}
