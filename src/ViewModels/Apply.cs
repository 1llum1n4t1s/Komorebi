using System;
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
    /// 適用するパッチファイルのパス。クリップボード適用時以外はファイル存在チェック付き。
    /// </summary>
    [CustomValidation(typeof(Apply), nameof(ValidatePatchFile))]
    public string PatchFile
    {
        get => _patchFile;
        set => SetProperty(ref _patchFile, value, true);
    }

    /// <summary>
    /// クリップボードの内容をパッチとして適用するかどうか。
    /// 切替時にPatchFileのバリデーションを再評価する。
    /// </summary>
    public bool FromClipboard
    {
        get => _fromClipboard;
        set
        {
            if (SetProperty(ref _fromClipboard, value))
                ValidateProperty(_patchFile, nameof(PatchFile));
        }
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
    /// 3-wayマージを使用するかどうか。
    /// </summary>
    public bool ThreeWayMerge
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
    /// クリップボードからの適用時はファイル指定を不要とする。
    /// </summary>
    /// <param name="file">検証するファイルパス</param>
    /// <param name="ctx">バリデーションコンテキスト</param>
    /// <returns>バリデーション結果</returns>
    public static ValidationResult ValidatePatchFile(string file, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is not Apply apply)
            return new ValidationResult("Invalid object instance!!!");

        if (apply.FromClipboard || File.Exists(file))
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

        // クリップボードから適用する場合は内容を検証して一時ファイルに書き出す
        // （upstream は IClipboard を View から注入するが、Komorebi は App ファサードを使用）
        var finalPatchFile = _patchFile;
        if (_fromClipboard)
        {
            var content = await App.GetClipboardTextAsync();
            if (string.IsNullOrEmpty(content) || !content.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                App.RaiseException(_repo.FullPath, "There's no valid patch content in clipboard!!!");
                return false;
            }

            finalPatchFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(finalPatchFile, content);
        }

        // コマンドログを作成する
        var log = _repo.CreateLog("Apply Patch");
        Use(log);

        // git applyコマンドを実行してパッチを適用する
        var extra = ThreeWayMerge ? "--3way" : string.Empty;
        var succ = await new Commands.Apply(_repo.FullPath, finalPatchFile, _ignoreWhiteSpace, SelectedWhiteSpaceMode.Arg, extra)
            .Use(log)
            .ExecAsync();

        log.Complete();

        // クリップボード適用用の一時ファイルを削除する
        if (_fromClipboard)
            File.Delete(finalPatchFile);

        return succ;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo = null;
    /// <summary>パッチファイルのパス</summary>
    private string _patchFile = string.Empty;
    /// <summary>クリップボードから適用するかどうか</summary>
    private bool _fromClipboard = false;
    /// <summary>空白を無視するかどうか</summary>
    private bool _ignoreWhiteSpace = true;
}
