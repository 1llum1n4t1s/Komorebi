using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
///     サブモジュールURL変更ダイアログのViewModel。
///     既存サブモジュールのリモートURLを変更する。
/// </summary>
public class ChangeSubmoduleUrl : Popup
{
    /// <summary>
    ///     対象のサブモジュール。
    /// </summary>
    public Models.Submodule Submodule
    {
        get;
    }

    /// <summary>
    ///     新しいサブモジュールURL。必須入力で形式バリデーション付き。
    /// </summary>
    [Required(ErrorMessage = "Url is required!!!")]
    [CustomValidation(typeof(ChangeSubmoduleUrl), nameof(ValidateUrl))]
    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value, true);
    }

    /// <summary>
    ///     コンストラクタ。リポジトリとサブモジュールを受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    /// <param name="submodule">URL変更対象のサブモジュール</param>
    public ChangeSubmoduleUrl(Repository repo, Models.Submodule submodule)
    {
        _repo = repo;
        _url = submodule.URL;
        Submodule = submodule;
    }

    /// <summary>
    ///     URLの形式を検証するバリデーションメソッド。
    /// </summary>
    /// <param name="url">検証するURL</param>
    /// <param name="ctx">バリデーションコンテキスト</param>
    /// <returns>バリデーション結果</returns>
    public static ValidationResult ValidateUrl(string url, ValidationContext ctx)
    {
        if (!Models.Remote.IsValidURL(url))
            return new ValidationResult("Invalid repository URL format");

        return ValidationResult.Success;
    }

    /// <summary>
    ///     確定処理。サブモジュールのURLが変更されていれば、git submodule set-urlを実行する。
    /// </summary>
    /// <returns>成功した場合はtrue</returns>
    public override async Task<bool> Sure()
    {
        // URLが変更されていなければ何もしない
        if (_url.Equals(Submodule.URL, StringComparison.Ordinal))
            return true;

        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.ChangeSubmoduleUrl");

        // コマンドログを作成する
        var log = _repo.CreateLog("Change Submodule's URL");
        Use(log);

        // git submodule set-urlコマンドを実行する
        var succ = await new Commands.Submodule(_repo.FullPath)
            .Use(log)
            .SetURLAsync(Submodule.Path, _url);

        log.Complete();
        return succ;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo;
    /// <summary>新しいURL</summary>
    private string _url;
}
