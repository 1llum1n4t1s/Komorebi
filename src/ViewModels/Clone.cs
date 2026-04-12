using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Komorebi.ViewModels;

/// <summary>
/// リポジトリクローンダイアログのViewModel。
/// git cloneコマンドでリモートリポジトリをローカルに複製する。
/// クリップボードからのURL自動検出、SSH鍵対応、サブモジュール初期化に対応する。
/// </summary>
public class Clone : Popup
{
    /// <summary>
    /// リモートリポジトリのURL。バリデーション付き。
    /// SSH URLが設定された場合はUseSSHフラグを自動的にtrueにする。
    /// </summary>
    [Required(ErrorMessage = "Remote URL is required")]
    [CustomValidation(typeof(Clone), nameof(ValidateRemote))]
    public string Remote
    {
        get => _remote;
        set
        {
            if (SetProperty(ref _remote, value, true))
                // SSH URLかどうかを判定してフラグを更新する
                UseSSH = Models.Remote.IsSSH(value);
        }
    }

    /// <summary>
    /// SSH接続を使用するかどうかのフラグ。
    /// </summary>
    public bool UseSSH
    {
        get => _useSSH;
        set => SetProperty(ref _useSSH, value);
    }

    /// <summary>
    /// SSH秘密鍵のファイルパス。ファイル存在チェックと公開鍵誤選択チェックのバリデーション付き。
    /// </summary>
    [CustomValidation(typeof(Clone), nameof(ValidateSSHKey))]
    public string SSHKey
    {
        get => _sshKey;
        set => SetProperty(ref _sshKey, value, true);
    }

    /// <summary>
    /// クローン先の親フォルダパス。バリデーション付き。
    /// </summary>
    [Required(ErrorMessage = "Parent folder is required")]
    [CustomValidation(typeof(Clone), nameof(ValidateParentFolder))]
    public string ParentFolder
    {
        get => _parentFolder;
        set => SetProperty(ref _parentFolder, value, true);
    }

    /// <summary>
    /// ローカルフォルダ名（カスタム名を指定する場合）。
    /// </summary>
    public string Local
    {
        get => _local;
        set => SetProperty(ref _local, value);
    }

    /// <summary>
    /// git cloneコマンドの追加引数。
    /// </summary>
    public string ExtraArgs
    {
        get => _extraArgs;
        set => SetProperty(ref _extraArgs, value);
    }

    /// <summary>
    /// クローン後にサブモジュールを初期化・更新するかどうか。
    /// </summary>
    public bool InitAndUpdateSubmodules
    {
        get;
        set;
    } = true;

    /// <summary>
    /// コンストラクタ。ページIDを受け取って初期化する。
    /// ワークスペースのデフォルトクローンディレクトリを設定し、
    /// クリップボードからリモートURLを自動検出する。
    /// </summary>
    /// <param name="pageId">クローン操作を行うページのID</param>
    public Clone(string pageId)
    {
        _pageId = pageId;

        // アクティブワークスペースのデフォルトクローンディレクトリを取得する
        var activeWorkspace = Preferences.Instance.GetActiveWorkspace();
        _parentFolder = activeWorkspace?.DefaultCloneDir;
        if (string.IsNullOrEmpty(ParentFolder))
            _parentFolder = Preferences.Instance.GitDefaultCloneDir;

        // クリップボードからURLを検出する
        // クリップボードアクセスはUIスレッドが必須のためDispatcher経由で実行する
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var text = await App.GetClipboardTextAsync();
                if (Models.Remote.IsValidURL(text))
                    Remote = text;
            }
            catch
            {
                // クリップボードアクセスエラーは無視する
            }
        });
    }

    /// <summary>
    /// リモートURLのカスタムバリデーション。
    /// 有効なリモートリポジトリURL形式かどうかを検証する。
    /// </summary>
    /// <param name="remote">検証対象のリモートURL</param>
    /// <param name="_">バリデーションコンテキスト（未使用）</param>
    /// <returns>バリデーション結果</returns>
    public static ValidationResult ValidateRemote(string remote, ValidationContext _)
    {
        if (!Models.Remote.IsValidURL(remote))
            return new ValidationResult("Invalid remote repository URL format");
        return ValidationResult.Success;
    }

    /// <summary>
    /// 親フォルダのカスタムバリデーション。
    /// 指定パスが存在するディレクトリかどうかを検証する。
    /// </summary>
    /// <param name="folder">検証対象のフォルダパス</param>
    /// <param name="_">バリデーションコンテキスト（未使用）</param>
    /// <returns>バリデーション結果</returns>
    public static ValidationResult ValidateParentFolder(string folder, ValidationContext _)
    {
        if (!Directory.Exists(folder))
            return new ValidationResult("Given path can NOT be found");
        return ValidationResult.Success;
    }

    /// <summary>
    /// SSH秘密鍵ファイルの存在と公開鍵誤選択を検証するバリデーションメソッド。
    /// </summary>
    public static ValidationResult ValidateSSHKey(string sshkey, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is Clone { _useSSH: true } && !string.IsNullOrEmpty(sshkey))
        {
            // センチネル値（「指定なし」選択）はファイルパスではないのでバリデーションをスキップ
            if (sshkey == Commands.Command.SSHKeyNoneSentinel)
                return ValidationResult.Success;

            if (!File.Exists(sshkey))
                return new ValidationResult("Given SSH private key can NOT be found!");

            if (sshkey.EndsWith(".pub", System.StringComparison.OrdinalIgnoreCase))
                return new ValidationResult(App.Text("SSHKey.PublicKeySelected"));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// 確定処理。git cloneコマンドを実行してリポジトリを複製する。
    /// クローン後はSSH鍵の設定、サブモジュール更新、リポジトリノード登録を行う。
    /// </summary>
    /// <returns>成功した場合はtrue</returns>
    public override async Task<bool> Sure()
    {
        ProgressDescription = App.Text("Progress.Cloning");

        // クローン専用のコマンドログを作成する
        var log = new CommandLog("Clone");
        Use(log);

        // SSHキーの特殊値を実際のパスに解決する
        var resolvedSSHKey = string.Empty;
        if (_useSSH)
        {
            if (_sshKey == Commands.Command.SSHKeyNoneSentinel)
                resolvedSSHKey = string.Empty; // システムデフォルトを使用
            else if (string.IsNullOrEmpty(_sshKey))
                resolvedSSHKey = Preferences.Instance?.GlobalSSHKey ?? string.Empty; // グローバルフォールバック
            else
                resolvedSSHKey = _sshKey; // 明示的に選択されたキー
        }

        // git cloneコマンドを実行する
        var succ = await new Commands.Clone(_pageId, _parentFolder, _remote, _local, resolvedSSHKey, _extraArgs)
            .Use(log)
            .ExecAsync();
        if (!succ)
            return false;

        // クローン先のフルパスを決定する
        var path = _parentFolder;
        if (!string.IsNullOrEmpty(_local))
        {
            // カスタムフォルダ名が指定されている場合はそれを使用する
            path = Path.GetFullPath(Path.Combine(path, _local));
        }
        else
        {
            // リモートURLからリポジトリ名を抽出する
            string name;
            if (Models.Remote.TryParseCodeCommitGRC(_remote, out _, out _, out var ccRepoName))
            {
                name = ccRepoName;
            }
            else
            {
                name = Path.GetFileName(_remote)!;
                if (name.EndsWith(".git", StringComparison.Ordinal))
                    name = name[..^4];
                else if (name.EndsWith(".bundle", StringComparison.Ordinal))
                    name = name[..^7];
            }

            path = Path.GetFullPath(Path.Combine(path, name));
        }

        // クローン先フォルダが存在するか確認する
        if (!Directory.Exists(path))
        {
            App.RaiseException(_pageId, App.Text("Error.FolderNotFound", path));
            return false;
        }

        // SSH鍵の設定をリポジトリのgit configに保存する
        // __NONE__ = システムデフォルト明示選択、具体パス = そのキーを使用、空文字 = グローバルフォールバック
        if (_useSSH && !string.IsNullOrEmpty(_sshKey))
        {
            await new Commands.Config(path)
                .Use(log)
                .SetAsync("remote.origin.sshkey", _sshKey);
        }

        // サブモジュールの初期化と更新を行う
        if (InitAndUpdateSubmodules)
        {
            var submodules = await new Commands.QueryUpdatableSubmodules(path, true).GetResultAsync();
            if (submodules.Count > 0)
                await new Commands.Submodule(path)
                    .Use(log)
                    .UpdateAsync(submodules, true);
        }

        log.Complete();

        // リポジトリノードを設定に登録する
        var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(path, null, true);
        await node.UpdateStatusAsync(false, null);

        // クローン元のページを検索する
        var launcher = App.GetLauncher();
        LauncherPage page = null;
        foreach (var one in launcher.Pages)
        {
            if (one.Node.Id == _pageId)
            {
                page = one;
                break;
            }
        }

        // ウェルカムページを更新し、リポジトリをタブで開く
        Welcome.Instance.Refresh();
        launcher.OpenRepositoryInTab(node, page);
        return true;
    }

    /// <summary>クローン操作を行うページのID</summary>
    private string _pageId = string.Empty;
    /// <summary>リモートリポジトリのURL</summary>
    private string _remote = string.Empty;
    /// <summary>SSH接続使用フラグ</summary>
    private bool _useSSH = false;
    /// <summary>SSH秘密鍵のファイルパス（空文字 = グローバル設定へフォールバック）</summary>
    private string _sshKey = string.Empty;
    /// <summary>クローン先の親フォルダパス</summary>
    private string _parentFolder = string.Empty;
    /// <summary>ローカルフォルダ名</summary>
    private string _local = string.Empty;
    /// <summary>git cloneの追加引数</summary>
    private string _extraArgs = string.Empty;
}
