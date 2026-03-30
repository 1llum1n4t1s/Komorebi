using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// パッチ適用ダイアログのViewModel。
/// git applyコマンドでパッチファイルをリポジトリに適用する。
/// </summary>
public class Apply : Popup
{
    /// <summary>
    /// 適用するパッチファイルのパス。必須入力でファイル存在チェック付き。
    /// </summary>
    [Required(ErrorMessage = "Patch file is required!!!")]
    [CustomValidation(typeof(Apply), nameof(ValidatePatchFile))]
    public string PatchFile
    {
        get => _patchFile;
        set => SetProperty(ref _patchFile, value, true);
    }

    /// <summary>
    /// 空白の違いを無視するかどうかのフラグ。
    /// </summary>
    public bool IgnoreWhiteSpace
    {
        get => _ignoreWhiteSpace;
        set => SetProperty(ref _ignoreWhiteSpace, value);
    }

    /// <summary>
    /// 選択された空白処理モード。
    /// </summary>
    public Models.ApplyWhiteSpaceMode SelectedWhiteSpaceMode
    {
        get;
        set;
    }

    /// <summary>
    /// コンストラクタ。リポジトリを受け取り、デフォルトの空白処理モードを設定する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    public Apply(Repository repo)
    {
        _repo = repo;

        // デフォルトの空白処理モードを設定する
        SelectedWhiteSpaceMode = Models.ApplyWhiteSpaceMode.Supported[0];
    }

    /// <summary>
    /// パッチファイルの存在を検証するバリデーションメソッド。
    /// </summary>
    /// <param name="file">検証するファイルパス</param>
    /// <param name="_">バリデーションコンテキスト（未使用）</param>
    /// <returns>バリデーション結果</returns>
    public static ValidationResult ValidatePatchFile(string file, ValidationContext _)
    {
        if (File.Exists(file))
            return ValidationResult.Success;

        return new ValidationResult($"File '{file}' can NOT be found!!!");
    }

    /// <summary>
    /// 確定処理。git applyコマンドを実行してパッチを適用する。
    /// </summary>
    /// <returns>成功した場合はtrue</returns>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.ApplyPatch");

        // コマンドログを作成する
        var log = _repo.CreateLog("Apply Patch");
        Use(log);

        // git applyコマンドを実行してパッチを適用する
        var succ = await new Commands.Apply(_repo.FullPath, _patchFile, _ignoreWhiteSpace, SelectedWhiteSpaceMode.Arg, null)
            .Use(log)
            .ExecAsync();

        log.Complete();
        return succ;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo = null;
    /// <summary>パッチファイルのパス</summary>
    private string _patchFile = string.Empty;
    /// <summary>空白を無視するかどうか</summary>
    private bool _ignoreWhiteSpace = true;
}
