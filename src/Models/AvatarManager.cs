using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Komorebi.Models;

/// <summary>
/// アバター画像のリソース変更を通知するためのホストインターフェース
/// </summary>
public interface IAvatarHost
{
    /// <summary>
    /// アバターリソースが変更された際に呼び出されるコールバック
    /// </summary>
    /// <param name="email">変更対象のメールアドレス</param>
    /// <param name="image">新しいアバター画像（取得失敗時はnull）</param>
    void OnAvatarResourceChanged(string email, Bitmap image);
}

/// <summary>
/// ユーザーアバター画像の取得・キャッシュ・管理を行うシングルトンクラス。
/// GravatarおよびGitHubアバターをサポートする。
/// </summary>
public partial class AvatarManager
{
    /// <summary>
    /// シングルトンインスタンスを取得する
    /// </summary>
    public static AvatarManager Instance
    {
        get
        {
            return _instance ??= new AvatarManager();
        }
    }

    private static AvatarManager _instance = null;

    /// <summary>
    /// GitHubのnoreplyメールアドレスからユーザー名を抽出する正規表現
    /// </summary>
    [GeneratedRegex(@"^(?:(\d+)\+)?(.+?)@.+\.github\.com$")]
    private static partial Regex REG_GITHUB_USER_EMAIL();

    // HttpClientはスレッドセーフで再利用可能。ループ内で毎回new するとソケット枯渇の原因になる
    private static readonly HttpClient s_httpClient = new() { Timeout = TimeSpan.FromSeconds(2) };
    private readonly Lock _synclock = new();
    /// <summary>アバター画像のローカルキャッシュディレクトリパス</summary>
    private string _storePath;
    /// <summary>アバター変更通知を受け取るホストのリスト</summary>
    private List<IAvatarHost> _avatars = [];
    /// <summary>メールアドレスをキーとしたアバター画像のキャッシュ</summary>
    private Dictionary<string, Bitmap> _resources = [];
    /// <summary>ダウンロードリクエスト待ちのメールアドレスセット</summary>
    private HashSet<string> _requesting = [];
    /// <summary>デフォルトアバターとして登録済みのメールアドレスセット</summary>
    private HashSet<string> _defaultAvatars = [];

    /// <summary>
    /// アバターマネージャーを開始し、バックグラウンドでアバター取得ループを起動する。
    /// デフォルトアバター（GitHub, Unreal）の読み込みも行う。
    /// </summary>
    public void Start()
    {
        _storePath = Path.Combine(Native.OS.DataDir, "avatars");
        if (!Directory.Exists(_storePath))
            Directory.CreateDirectory(_storePath);

        LoadDefaultAvatar("noreply@github.com", "github.png");
        LoadDefaultAvatar("unrealbot@epicgames.com", "unreal.png");

        Task.Run(async () =>
        {
            while (true)
            {
                string email = null;

                lock (_synclock)
                {
                    foreach (var one in _requesting)
                    {
                        email = one;
                        break;
                    }
                }

                if (email is null)
                {
                    // Thread.Sleepはスレッドプールを占有するためTask.Delayに変更
                    await Task.Delay(100).ConfigureAwait(false);
                    continue;
                }

                var md5 = GetEmailHash(email);
                var matchGitHubUser = REG_GITHUB_USER_EMAIL().Match(email);
                var url = $"https://www.gravatar.com/avatar/{md5}?d=404";
                if (matchGitHubUser.Success)
                {
                    var githubUser = matchGitHubUser.Groups[2].Value;
                    if (githubUser.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase))
                        githubUser = githubUser[..^5];

                    url = $"https://avatars.githubusercontent.com/{githubUser}";
                }

                var localFile = Path.Combine(_storePath, md5);
                Bitmap img = null;
                try
                {
                    // staticなHttpClientを再利用（ソケット枯渇防止・DNS再利用・接続プール活用）
                    using var rsp = await s_httpClient.GetAsync(url).ConfigureAwait(false);
                    if (rsp.IsSuccessStatusCode)
                    {
                        // パフォーマンス: MemoryStreamで一度だけ読み込み、ファイル保存とデコードを効率化
                        // 旧: sync CopyTo + 二重ファイルI/O（書き込み→読み込み）
                        using var ms = new MemoryStream();
                        await rsp.Content.CopyToAsync(ms).ConfigureAwait(false);

                        // ファイルキャッシュに非同期で書き込み
                        ms.Position = 0;
                        await using (var writer = File.Create(localFile))
                        {
                            await ms.CopyToAsync(writer).ConfigureAwait(false);
                        }

                        // MemoryStreamから直接デコード（ディスク再読み込み不要）
                        ms.Position = 0;
                        img = Bitmap.DecodeToWidth(ms, 128);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"アバターダウンロード失敗: {email} - {ex.Message}", LogLevel.Warning);
                }

                lock (_synclock)
                {
                    _requesting.Remove(email);
                }

                Dispatcher.UIThread.Post(() =>
                {
                    _resources[email] = img;
                    NotifyResourceChanged(email, img);
                });
            }

            // ReSharper disable once FunctionNeverReturns
        });
    }

    /// <summary>
    /// アバターリソース変更の通知を受け取るホストを登録する
    /// </summary>
    /// <param name="host">登録するホスト</param>
    public void Subscribe(IAvatarHost host)
    {
        _avatars.Add(host);
    }

    /// <summary>
    /// アバターリソース変更の通知登録を解除する
    /// </summary>
    /// <param name="host">解除するホスト</param>
    public void Unsubscribe(IAvatarHost host)
    {
        _avatars.Remove(host);
    }

    /// <summary>
    /// 指定メールアドレスのアバター画像をリクエストする。
    /// キャッシュにあればそれを返し、なければバックグラウンドでダウンロードをキューに入れる。
    /// </summary>
    /// <param name="email">対象のメールアドレス</param>
    /// <param name="forceRefetch">trueの場合、キャッシュを削除して再取得する</param>
    /// <returns>キャッシュ済みのアバター画像、またはnull（取得中の場合）</returns>
    public Bitmap Request(string email, bool forceRefetch)
    {
        if (forceRefetch)
        {
            if (_defaultAvatars.Contains(email))
                return null;

            _resources.Remove(email);

            var localFile = Path.Combine(_storePath, GetEmailHash(email));
            if (File.Exists(localFile))
                File.Delete(localFile);

            NotifyResourceChanged(email, null);
        }
        else
        {
            if (_resources.TryGetValue(email, out var value))
                return value;

            var localFile = Path.Combine(_storePath, GetEmailHash(email));
            if (File.Exists(localFile))
            {
                try
                {
                    using (var stream = File.OpenRead(localFile))
                    {
                        var img = Bitmap.DecodeToWidth(stream, 128);
                        _resources.Add(email, img);
                        return img;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"アバターキャッシュ読み込み失敗: {email} - {ex.Message}", LogLevel.Warning);
                }
            }
        }

        lock (_synclock)
        {
            _requesting.Add(email);
        }

        return null;
    }

    /// <summary>
    /// ローカルファイルからアバター画像を設定する
    /// </summary>
    /// <param name="email">対象のメールアドレス</param>
    /// <param name="file">ローカル画像ファイルパス</param>
    public void SetFromLocal(string email, string file)
    {
        try
        {
            Bitmap image;

            using (var stream = File.OpenRead(file))
            {
                image = Bitmap.DecodeToWidth(stream, 128);
            }

            _resources[email] = image;

            lock (_synclock)
            {
                _requesting.Remove(email);
            }

            var store = Path.Combine(_storePath, GetEmailHash(email));
            File.Copy(file, store, true);
            NotifyResourceChanged(email, image);
        }
        catch (Exception ex)
        {
            Logger.Log($"ローカルアバター設定失敗: {email} - {ex.Message}", LogLevel.Warning);
        }
    }

    /// <summary>
    /// 埋め込みリソースからデフォルトアバターを読み込む
    /// </summary>
    /// <param name="key">メールアドレスキー</param>
    /// <param name="img">リソース画像ファイル名</param>
    private void LoadDefaultAvatar(string key, string img)
    {
        var icon = AssetLoader.Open(new Uri($"avares://Komorebi/Resources/Images/{img}", UriKind.RelativeOrAbsolute));
        _resources.Add(key, new Bitmap(icon));
        _defaultAvatars.Add(key);
    }

    /// <summary>
    /// メールアドレスのMD5ハッシュを計算する（Gravatar URL生成用）
    /// </summary>
    /// <param name="email">対象のメールアドレス</param>
    /// <returns>MD5ハッシュの16進数文字列</returns>
    private static string GetEmailHash(string email)
    {
        // Gravatar仕様に準拠: UTF-8でエンコードしたメールアドレスのMD5ハッシュ
        // 旧: Encoding.Defaultはプラットフォーム依存で非ASCII文字のハッシュが異なる可能性あり
        var lowered = email.Trim().ToLowerInvariant();
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(lowered));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// 全登録ホストにアバターリソース変更を通知する
    /// </summary>
    /// <param name="email">変更対象のメールアドレス</param>
    /// <param name="image">新しいアバター画像</param>
    private void NotifyResourceChanged(string email, Bitmap image)
    {
        foreach (var avatar in _avatars)
            avatar.OnAvatarResourceChanged(email, image);
    }
}
