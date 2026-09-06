using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>新しいリモートブランチの入力と参照名検証を管理する。</summary>
public class PushToNewBranch : ObservableValidator
{
    public string Remote { get; }

    [Required(ErrorMessage = "Branch name is required!")]
    [CustomValidation(typeof(PushToNewBranch), nameof(ValidateBranchName))]
    public string BranchName
    {
        get => _branchName;
        set => SetProperty(ref _branchName, value, true);
    }

    public PushToNewBranch(string remote)
    {
        Remote = remote;
    }

    public static ValidationResult? ValidateBranchName(string name, ValidationContext ctx)
    {
        return Models.RefName.IsValidBranchName(name)
            ? ValidationResult.Success
            : new ValidationResult("Bad branch name format!");
    }

    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026:RequiresUnreferencedCode")]
    public bool Check()
    {
        ValidateAllProperties();
        return !HasErrors;
    }

    private string _branchName = string.Empty;
}
