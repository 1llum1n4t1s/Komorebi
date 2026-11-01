namespace Komorebi.ViewModels;

/// <summary>入力検証のメッセージを現在の言語から取得する。</summary>
public static class ValidationMessages
{
    public static string BranchExists => App.Text("Validation.BranchExists");
    public static string BranchNameInvalid => App.Text("Validation.BranchNameInvalid");
    public static string BranchNameRequired => App.Text("Validation.BranchNameRequired");
    public static string CommitDescriptionRequired => App.Text("Validation.CommitDescriptionRequired");
    public static string CommitMessageRequired => App.Text("Validation.CommitMessageRequired");
    public static string CommitTypeRequired => App.Text("Validation.CommitTypeRequired");
    public static string ContextInvalid => App.Text("Validation.ContextInvalid");
    public static string DevelopBranchRequired => App.Text("Validation.DevelopBranchRequired");
    public static string EmailInvalid => App.Text("Validation.EmailInvalid");
    public static string EmailRequired => App.Text("Validation.EmailRequired");
    public static string FeaturePrefixInvalid => App.Text("Validation.FeaturePrefixInvalid");
    public static string FeaturePrefixRequired => App.Text("Validation.FeaturePrefixRequired");
    public static string GitFlowBranchesEqual => App.Text("Validation.GitFlowBranchesEqual");
    public static string GroupNameRequired => App.Text("Validation.GroupNameRequired");
    public static string HotfixPrefixInvalid => App.Text("Validation.HotfixPrefixInvalid");
    public static string HotfixPrefixRequired => App.Text("Validation.HotfixPrefixRequired");
    public static string IgnorePatternRequired => App.Text("Validation.IgnorePatternRequired");
    public static string LFSPatternRequired => App.Text("Validation.LFSPatternRequired");
    public static string LocalBranchRequired => App.Text("Validation.LocalBranchRequired");
    public static string MasterBranchRequired => App.Text("Validation.MasterBranchRequired");
    public static string NameRequired => App.Text("Validation.NameRequired");
    public static string OutputFileRequired => App.Text("Validation.OutputFileRequired");
    public static string ParentFolderRequired => App.Text("Validation.ParentFolderRequired");
    public static string PassphraseMismatch => App.Text("Validation.PassphraseMismatch");
    public static string PassphraseRequired => App.Text("Validation.PassphraseRequired");
    public static string PatchFileRequired => App.Text("Validation.PatchFileRequired");
    public static string PathNotEmpty => App.Text("Validation.PathNotEmpty");
    public static string PathNotFound => App.Text("Validation.PathNotFound");
    public static string PathRequired => App.Text("Validation.PathRequired");
    public static string ReleasePrefixInvalid => App.Text("Validation.ReleasePrefixInvalid");
    public static string ReleasePrefixRequired => App.Text("Validation.ReleasePrefixRequired");
    public static string RemoteBranchRequired => App.Text("Validation.RemoteBranchRequired");
    public static string RemoteNameExists => App.Text("Validation.RemoteNameExists");
    public static string RemoteNameInvalid => App.Text("Validation.RemoteNameInvalid");
    public static string RemoteNameRequired => App.Text("Validation.RemoteNameRequired");
    public static string RemoteRequired => App.Text("Validation.RemoteRequired");
    public static string RemoteUrlExists => App.Text("Validation.RemoteUrlExists");
    public static string RemoteUrlInvalid => App.Text("Validation.RemoteUrlInvalid");
    public static string RemoteUrlRequired => App.Text("Validation.RemoteUrlRequired");
    public static string RepositoryFolderRequired => App.Text("Validation.RepositoryFolderRequired");
    public static string SSHKeyNameInvalid => App.Text("Validation.SSHKeyNameInvalid");
    public static string SSHKeyNotFound => App.Text("Validation.SSHKeyNotFound");
    public static string StorageFileRequired => App.Text("Validation.StorageFileRequired");
    public static string TagExists => App.Text("Validation.TagExists");
    public static string TagNameInvalid => App.Text("Validation.TagNameInvalid");
    public static string TagNameRequired => App.Text("Validation.TagNameRequired");
    public static string TagPrefixInvalid => App.Text("Validation.TagPrefixInvalid");
    public static string UrlRequired => App.Text("Validation.UrlRequired");
    public static string WorktreePathRequired => App.Text("Validation.WorktreePathRequired");
}
