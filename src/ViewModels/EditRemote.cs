using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// リモートリポジトリの設定を編集するダイアログViewModel。
/// 名前、URL、SSH鍵の変更に対応し、バリデーション付き。
/// </summary>
public class EditRemote : Popup
{
    /// <summary>
    /// リモート名。一意性とフォーマットのバリデーション付き。
    /// </summary>
    [Required(ErrorMessage = "Remote name is required!!!")]
    [RegularExpression(@"^[\w\-\.]+$", ErrorMessage = "Bad remote name format!!!")]
    [CustomValidation(typeof(EditRemote), nameof(ValidateRemoteName))]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, true);
    }

    /// <summary>
    /// リモートURL。変更時にSSH判定を自動更新する。
    /// </summary>
    [Required(ErrorMessage = "Remote URL is required!!!")]
    [CustomValidation(typeof(EditRemote), nameof(ValidateRemoteURL))]
    public string Url
    {
        get => _url;
        set
        {
            if (SetProperty(ref _url, value, true))
                UseSSH = Models.Remote.IsSSH(value);
        }
    }

    /// <summary>
    /// SSH接続を使用するかどうか。変更時にSSH鍵のバリデーションを再実行する。
    /// </summary>
    public bool UseSSH
    {
        get => _useSSH;
        set
        {
            if (SetProperty(ref _useSSH, value))
                ValidateProperty(_sshkey, nameof(SSHKey));
        }
    }

    /// <summary>
    /// SSH秘密鍵のファイルパス。ファイル存在チェックのバリデーション付き。
    /// </summary>
    [CustomValidation(typeof(EditRemote), nameof(ValidateSSHKey))]
    public string SSHKey
    {
        get => _sshkey;
        set => SetProperty(ref _sshkey, value, true);
    }

    /// <summary>
    /// コンストラクタ。対象リポジトリとリモート設定を指定する。SSH使用時はSSH鍵を読み込む。
    /// </summary>
    public EditRemote(Repository repo, Models.Remote remote)
    {
        _repo = repo;
        _remote = remote;
        _name = remote.Name;
        _url = remote.URL;
        _useSSH = Models.Remote.IsSSH(remote.URL);

        if (_useSSH)
            _sshkey = new Commands.Config(repo.FullPath).Get($"remote.{remote.Name}.sshkey");
    }

    /// <summary>
    /// リモート名の一意性を検証するバリデーター。
    /// </summary>
    public static ValidationResult ValidateRemoteName(string name, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is EditRemote edit)
        {
            foreach (var remote in edit._repo.Remotes)
            {
                if (remote != edit._remote && name == remote.Name)
                    return new ValidationResult("A remote with given name already exists!!!");
            }
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// リモートURLのフォーマットと一意性を検証するバリデーター。
    /// </summary>
    public static ValidationResult ValidateRemoteURL(string url, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is EditRemote edit)
        {
            if (!Models.Remote.IsValidURL(url))
                return new ValidationResult("Bad remote URL format!!!");

            foreach (var remote in edit._repo.Remotes)
            {
                if (remote != edit._remote && url == remote.URL)
                    return new ValidationResult("A remote with the same url already exists!!!");
            }
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// SSH秘密鍵ファイルの存在を検証するバリデーター。
    /// </summary>
    public static ValidationResult ValidateSSHKey(string sshkey, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is EditRemote { _useSSH: true } && !string.IsNullOrEmpty(sshkey))
        {
            if (!File.Exists(sshkey))
                return new ValidationResult("Given SSH private key can NOT be found!");

            if (sshkey.EndsWith(".pub", System.StringComparison.OrdinalIgnoreCase))
                return new ValidationResult(App.Text("SSHKey.PublicKeySelected"));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// リモート設定の変更を実行する確認アクション。
    /// 名前変更、URL変更、プッシュURL同期、SSH鍵設定を順に行う。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.EditingRemote", _remote.Name);

        // リモート名が変更された場合はリネーム
        if (_remote.Name != _name)
        {
            var succ = await new Commands.Remote(_repo.FullPath).RenameAsync(_remote.Name, _name);
            if (succ)
                _remote.Name = _name;
        }

        // フェッチURLが変更された場合は更新
        if (_remote.URL != _url)
        {
            var succ = await new Commands.Remote(_repo.FullPath).SetURLAsync(_name, _url, false);
            if (succ)
                _remote.URL = _url;
        }

        // プッシュURLもフェッチURLと同期
        var pushURL = await new Commands.Remote(_repo.FullPath).GetURLAsync(_name, true);
        if (pushURL != _url)
            await new Commands.Remote(_repo.FullPath).SetURLAsync(_name, _url, true);

        // SSH鍵の設定を更新
        await new Commands.Config(_repo.FullPath).SetAsync($"remote.{_name}.sshkey", _useSSH ? SSHKey : null);
        return true;
    }

    private readonly Repository _repo = null; // 対象リポジトリ
    private readonly Models.Remote _remote = null; // 編集対象のリモート
    private string _name = null; // リモート名
    private string _url = null; // リモートURL
    private bool _useSSH = false; // SSH使用フラグ
    private string _sshkey = string.Empty; // SSH秘密鍵パス
}
