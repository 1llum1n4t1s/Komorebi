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
    /// Windows/macOS のケースインセンシティブな FS を想定し、大小文字無視で比較する。
    /// </summary>
    public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is CheckoutBranchFromStash caller)
        {
            foreach (var b in caller._repo.Branches)
            {
                if (b.FriendlyName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return new ValidationResult("A branch with same name already exists!");
            }

            return ValidationResult.Success;
        }

        return new ValidationResult("Missing runtime context to create branch!");
    }

    /// <summary>
    /// git stash branch を実行してブランチを作成する。
    /// 成功時はブランチ/スタッシュ/ワーキングコピーの再読込をマーク、失敗時は false を返してダイアログを残す。
    /// </summary>
    public override async Task<bool> Sure()
    {
        ProgressDescription = App.Text("Progress.CheckoutBranchFromStash");

        var log = _repo.CreateLog($"Checkout Branch '{_branchName}'");
        Use(log);

        // Watcher ロックはコマンド実行中のみに限定。
        // ロック中に発生した FS イベントが解除後にバッファ配送されて
        // Mark*DirtyManually() の更新を打ち消すのを防ぐため、
        // using スコープを細かく区切ってから Mark*DirtyManually() を呼ぶ。
        bool succ;
        using (_repo.LockWatcher())
        {
            succ = await new Commands.Stash(_repo.FullPath)
                .Use(log)
                .CheckoutBranchAsync(Target.Name, _branchName);
        }

        if (succ)
        {
            // git stash branch は新ブランチ作成 + チェックアウト + スタッシュ適用/削除を行うため
            // ブランチリスト・ワーキングコピー・スタッシュ全てを再読込させる。
            _repo.MarkBranchesDirtyManually();
            _repo.MarkWorkingCopyDirtyManually();
            _repo.MarkStashesDirtyManually();
        }

        log.Complete();
        return succ;
    }

    private readonly Repository _repo;
    private string _branchName = string.Empty;
}
