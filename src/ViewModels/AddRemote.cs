using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// リモートリポジトリ追加ダイアログのViewModel。
/// 新しいリモートの名前・URL・SSH鍵を設定してリポジトリに追加する。
/// </summary>
public class AddRemote : Popup
{
    /// <summary>
    /// リモート名。バリデーション付きで、英数字・ハイフン・ドット・アンダースコアのみ許可。
    /// </summary>
    [Required(ErrorMessage = "Remote name is required!!!")]
    [RegularExpression(@"^[\w\-\.]+$", ErrorMessage = "Bad remote name format!!!")]
    [CustomValidation(typeof(AddRemote), nameof(ValidateRemoteName))]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, true);
    }

    /// <summary>
    /// リモートリポジトリのURL。設定時にSSH接続かどうかを自動判定する。
    /// </summary>
    [Required(ErrorMessage = "Remote URL is required!!!")]
    [CustomValidation(typeof(AddRemote), nameof(ValidateRemoteURL))]
    public string Url
    {
        get => _url;
        set
        {
            if (SetProperty(ref _url, value, true))
                // URLがSSH形式かどうかを判定してフラグを更新する
                UseSSH = Models.Remote.IsSSH(value);
        }
    }

    /// <summary>
    /// SSH接続を使用するかどうかのフラグ。変更時にSSH鍵のバリデーションを再実行する。
    /// </summary>
    public bool UseSSH
    {
        get => _useSSH;
        set
        {
            if (SetProperty(ref _useSSH, value))
                // SSH使用状態が変わったのでSSH鍵のバリデーションを再実行する
                ValidateProperty(_sshkey, nameof(SSHKey));
        }
    }

    /// <summary>
    /// SSH秘密鍵のファイルパス。SSH接続時に使用される。
    /// </summary>
    [CustomValidation(typeof(AddRemote), nameof(ValidateSSHKey))]
    public string SSHKey
    {
        get => _sshkey;
        set => SetProperty(ref _sshkey, value, true);
    }

    /// <summary>
    /// コンストラクタ。対象リポジトリを受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    public AddRemote(Repository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// リモート名の重複チェックを行うバリデーションメソッド。
    /// </summary>
    /// <param name="name">検証するリモート名</param>
    /// <param name="ctx">バリデーションコンテキスト</param>
    /// <returns>バリデーション結果</returns>
    public static ValidationResult ValidateRemoteName(string name, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is AddRemote add)
        {
            // 同じ名前のリモートが既に存在するか確認する
            var exists = add._repo.Remotes.Find(x => x.Name == name);
            if (exists is not null)
                return new ValidationResult("A remote with given name already exists!!!");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// リモートURLの形式と重複チェックを行うバリデーションメソッド。
    /// </summary>
    /// <param name="url">検証するURL</param>
    /// <param name="ctx">バリデーションコンテキスト</param>
    /// <returns>バリデーション結果</returns>
    public static ValidationResult ValidateRemoteURL(string url, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is AddRemote add)
        {
            // URL形式の妥当性を検証する
            if (!Models.Remote.IsValidURL(url))
                return new ValidationResult("Bad remote URL format!!!");

            // 同じURLのリモートが既に存在するか確認する
            var exists = add._repo.Remotes.Find(x => x.URL == url);
            if (exists is not null)
                return new ValidationResult("A remote with the same url already exists!!!");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// SSH秘密鍵ファイルの存在チェックを行うバリデーションメソッド。
    /// </summary>
    /// <param name="sshkey">検証するSSH鍵ファイルパス</param>
    /// <param name="ctx">バリデーションコンテキスト</param>
    /// <returns>バリデーション結果</returns>
    public static ValidationResult ValidateSSHKey(string sshkey, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is AddRemote { _useSSH: true } && !string.IsNullOrEmpty(sshkey))
        {
            // SSH鍵ファイルが実際に存在するか確認する
            if (!File.Exists(sshkey))
                return new ValidationResult("Given SSH private key can NOT be found!");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// 確定処理。リモートを追加し、SSH鍵の設定とフェッチを実行する。
    /// </summary>
    /// <returns>成功した場合はtrue</returns>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.AddingRemote");

        // コマンドログを作成する
        var log = _repo.CreateLog("Add Remote");
        Use(log);

        // git remote addコマンドを実行してリモートを追加する
        var succ = await new Commands.Remote(_repo.FullPath)
            .Use(log)
            .AddAsync(_name, _url);

        if (succ)
        {
            // SSH鍵の設定をgit configに保存する
            await new Commands.Config(_repo.FullPath)
                .Use(log)
                .SetAsync($"remote.{_name}.sshkey", _useSSH ? SSHKey : null);

            // 追加したリモートからフェッチを実行する
            await new Commands.Fetch(_repo.FullPath, _name, false, false)
                .Use(log)
                .RunAsync();
        }

        log.Complete();

        // リポジトリの状態を更新する
        _repo.MarkFetched();
        _repo.MarkBranchesDirtyManually();
        return succ;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo = null;
    /// <summary>入力されたリモート名</summary>
    private string _name = string.Empty;
    /// <summary>入力されたリモートURL</summary>
    private string _url = string.Empty;
    /// <summary>SSH接続を使用するかどうか</summary>
    private bool _useSSH = false;
    /// <summary>SSH秘密鍵のファイルパス</summary>
    private string _sshkey = string.Empty;
}
