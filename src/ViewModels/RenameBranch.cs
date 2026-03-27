using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// ブランチ名を変更するダイアログのViewModel。
/// 新しいブランチ名のバリデーション（必須入力、形式チェック、重複チェック）を行う。
/// </summary>
public class RenameBranch : Popup
{
    /// <summary>
    /// リネーム対象のブランチ。
    /// </summary>
    public Models.Branch Target
    {
        get;
    }

    /// <summary>
    /// 新しいブランチ名（必須、正規表現による形式チェック、重複チェック付き）。
    /// </summary>
    [Required(ErrorMessage = "Branch name is required!!!")]
    [RegularExpression(@"^[\w\-/\.#\+]+$", ErrorMessage = "Bad branch name format!")]
    [CustomValidation(typeof(RenameBranch), nameof(ValidateBranchName))]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, true);
    }

    /// <summary>
    /// リポジトリとリネーム対象のブランチを指定してダイアログを初期化する。
    /// 現在のブランチ名をデフォルト値として設定する。
    /// </summary>
    public RenameBranch(Repository repo, Models.Branch target)
    {
        _repo = repo;
        _name = target.Name;
        Target = target;
    }

    /// <summary>
    /// ブランチ名の重複をバリデーションする。
    /// 同名のローカルブランチが既に存在する場合はエラーを返す。
    /// </summary>
    public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is RenameBranch rename)
        {
            // 同名のローカルブランチが存在するかチェック（自分自身は除外）
            foreach (var b in rename._repo.Branches)
            {
                if (b.IsLocal && b != rename.Target && b.Name.Equals(name, StringComparison.Ordinal))
                    return new ValidationResult("A branch with same name already exists!!!");
            }
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// ブランチのリネームを実行する。
    /// 名前が変更されていない場合は何もせず成功を返す。
    /// リネーム後はUIフィルタの更新とブランチ一覧の再読み込みを行う。
    /// </summary>
    public override async Task<bool> Sure()
    {
        // 名前が変更されていない場合は即座に成功
        if (Target.Name.Equals(_name, StringComparison.Ordinal))
            return true;

        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.RenamingBranch", Target.Name);

        var log = _repo.CreateLog($"Rename Branch '{Target.Name}'");
        Use(log);

        var isCurrent = Target.IsCurrent;
        var oldName = Target.FullName;

        // git branch -m コマンドでリネーム実行
        var succ = await new Commands.Branch(_repo.FullPath, Target.Name)
            .Use(log)
            .RenameAsync(_name);

        // リネーム成功時はUIフィルタのブランチ名を更新
        if (succ)
            _repo.UIStates.RenameBranchFilter(Target.FullName, _name);

        log.Complete();
        // ブランチ一覧の再読み込みをトリガー
        _repo.MarkBranchesDirtyManually();

        // 現在のブランチをリネームした場合は更新完了を待機
        if (isCurrent)
        {
            ProgressDescription = App.Text("Progress.WaitingBranchUpdate");
            await Task.Delay(400);
        }

        return succ;
    }

    /// <summary>対象リポジトリ</summary>
    private readonly Repository _repo;
    /// <summary>新しいブランチ名</summary>
    private string _name;
}
