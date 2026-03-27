using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
///     サブモジュール追加ダイアログのViewModel。
///     リポジトリにgitサブモジュールを追加する操作を担当する。
/// </summary>
public class AddSubmodule : Popup
{
    /// <summary>
    ///     サブモジュールのリポジトリURL。必須入力でURL形式のバリデーション付き。
    /// </summary>
    [Required(ErrorMessage = "Url is required!!!")]
    [CustomValidation(typeof(AddSubmodule), nameof(ValidateURL))]
    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value, true);
    }

    /// <summary>
    ///     サブモジュールの相対パス。空の場合はURLからパス名を自動生成する。
    /// </summary>
    public string RelativePath
    {
        get => _relativePath;
        set => SetProperty(ref _relativePath, value);
    }

    /// <summary>
    ///     サブモジュールを再帰的にクローンするかどうかのフラグ。
    /// </summary>
    public bool Recursive
    {
        get;
        set;
    }

    /// <summary>
    ///     コンストラクタ。対象リポジトリを受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    public AddSubmodule(Repository repo)
    {
        _repo = repo;
    }

    /// <summary>
    ///     サブモジュールURLの形式を検証するバリデーションメソッド。
    /// </summary>
    /// <param name="url">検証するURL</param>
    /// <param name="ctx">バリデーションコンテキスト</param>
    /// <returns>バリデーション結果</returns>
    public static ValidationResult ValidateURL(string url, ValidationContext ctx)
    {
        if (!Models.Remote.IsValidURL(url))
            return new ValidationResult("Invalid repository URL format");

        return ValidationResult.Success;
    }

    /// <summary>
    ///     確定処理。git submodule addコマンドを実行してサブモジュールを追加する。
    /// </summary>
    /// <returns>成功した場合はtrue</returns>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.AddingSubmodule");

        // コマンドログを作成する
        var log = _repo.CreateLog("Add Submodule");
        Use(log);

        // 相対パスが未指定の場合、URLから自動推定する
        var relativePath = _relativePath;
        if (string.IsNullOrEmpty(relativePath))
        {
            // URLの末尾パターンに応じてディレクトリ名を決定する
            if (_url.EndsWith("/.git", StringComparison.Ordinal))
                relativePath = Path.GetFileName(Path.GetDirectoryName(_url));
            else if (_url.EndsWith(".git", StringComparison.Ordinal))
                relativePath = Path.GetFileNameWithoutExtension(_url);
            else
                relativePath = Path.GetFileName(_url);
        }

        // git submodule addコマンドを実行する
        var succ = await new Commands.Submodule(_repo.FullPath)
            .Use(log)
            .AddAsync(_url, relativePath, Recursive);

        log.Complete();
        return succ;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo = null;
    /// <summary>入力されたサブモジュールURL</summary>
    private string _url = string.Empty;
    /// <summary>入力された相対パス</summary>
    private string _relativePath = string.Empty;
}
