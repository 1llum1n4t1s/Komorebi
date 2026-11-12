// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
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
        set
        {
            if (SetProperty(ref _patchFile, value, true))
                CanCreateCommit = DetectMailFormatPatch(value);
        }
    }

    /// <summary>
    /// 選択中のパッチファイルがメール形式（git format-patch の出力）で、コミットとして適用できるかどうか。
    /// upstream ef017624。上流はバリデーター内で更新するが、Komorebi は副作用を避けて setter で判定する。
    /// </summary>
    public bool CanCreateCommit
    {
        get => _canCreateCommit;
        private set => SetProperty(ref _canCreateCommit, value);
    }

    /// <summary>
    /// メール形式のパッチを git apply ではなく git am でコミットとして適用するかどうか。
    /// </summary>
    public bool CommitChanges
    {
        get;
        set;
    } = true;

    /// <summary>
    /// クリップボードの内容をパッチとして適用するかどうか。
    /// </summary>
    public bool FromClipboard
    {
        get => _fromClipboard;
        set
        {
            if (SetProperty(ref _fromClipboard, value))
            {
                // upstreamとの差分: 入力元の変更で不要になったファイルエラーを解除する。
                ValidateProperty(_patchFile, nameof(PatchFile));
            }
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
            return new ValidationResult(ValidationMessages.ContextInvalid);

        if (apply.FromClipboard)
            return ValidationResult.Success;

        if (string.IsNullOrEmpty(file))
            return new ValidationResult(ValidationMessages.PatchFileRequired);

        if (!File.Exists(file))
            return new ValidationResult(App.Text("Error.FolderNotFound", file));

        return ValidationResult.Success;
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

        // 一時ファイルは TempFileScope で RAII 削除する。書き込みや適用が例外で抜けても
        // %TEMP% にパッチが残らないようにする（docs/PITFALLS.md「一時ファイルは try/finally」）。
        TempFileScope clipboardPatch = null;
        try
        {
            if (_fromClipboard)
            {
                var content = await App.GetClipboardTextAsync();
                if (string.IsNullOrEmpty(content) || !content.StartsWith("diff --git ", StringComparison.Ordinal))
                {
                    App.RaiseException(_repo.FullPath, App.Text("Error.InvalidClipboardPatch"));
                    return false;
                }

                clipboardPatch = new TempFileScope();
                finalPatchFile = clipboardPatch.Path;
                await File.WriteAllTextAsync(finalPatchFile, content);
            }

            // コマンドログを作成する
            var log = _repo.CreateLog("Apply Patch");
            Use(log);

            var extra = ThreeWayMerge ? "--3way" : string.Empty;
            bool succ;
            if (!_fromClipboard && _canCreateCommit && CommitChanges)
            {
                // upstream ef017624: メール形式のパッチは git am でコミットとして適用する。
                // 入力中のコミットメッセージは am のメッセージ（rebase-apply/final-commit）と混ざらないよう消す。
                _repo.ClearCommitMessage();

                succ = await new Commands.Am(_repo.FullPath, finalPatchFile, _ignoreWhiteSpace, SelectedWhiteSpaceMode.Arg, extra)
                    .Use(log)
                    .ExecAsync();
            }
            else
            {
                // git applyコマンドを実行してパッチを作業ツリーへ適用する
                succ = await new Commands.Apply(_repo.FullPath, finalPatchFile, _ignoreWhiteSpace, SelectedWhiteSpaceMode.Arg, extra)
                    .Use(log)
                    .ExecAsync();
            }

            log.Complete();

            return succ;
        }
        finally
        {
            clipboardPatch?.Dispose();
        }
    }

    /// <summary>
    /// パッチファイルの先頭 4 行が git format-patch のメールヘッダー（From / From: / Date: / Subject:）かを判定する。
    /// 読めないファイルや存在しないファイルは false（通常の git apply を使う）。
    /// </summary>
    internal static bool DetectMailFormatPatch(string file)
    {
        if (string.IsNullOrEmpty(file) || !File.Exists(file))
            return false;

        try
        {
            using var reader = new StreamReader(file);
            var lines = new string[4];
            for (var i = 0; i < lines.Length; i++)
            {
                lines[i] = reader.ReadLine();
                if (lines[i] is null)
                    return false;
            }

            return lines[0].StartsWith("From ", StringComparison.Ordinal) &&
                lines[1].StartsWith("From: ", StringComparison.Ordinal) &&
                lines[2].StartsWith("Date: ", StringComparison.Ordinal) &&
                lines[3].StartsWith("Subject: ", StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo = null;
    /// <summary>メール形式のパッチかどうか</summary>
    private bool _canCreateCommit = false;
    /// <summary>パッチファイルのパス</summary>
    private string _patchFile = string.Empty;
    /// <summary>クリップボードから適用するかどうか</summary>
    private bool _fromClipboard = false;
    /// <summary>空白を無視するかどうか</summary>
    private bool _ignoreWhiteSpace = true;
}
