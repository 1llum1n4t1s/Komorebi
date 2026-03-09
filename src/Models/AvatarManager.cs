using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace Komorebi.Models
{
    /// <summary>
    ///     アバター画像のリソース変更を通知するためのホストインターフェース
    /// </summary>
    public interface IAvatarHost
    {
        /// <summary>
        ///     アバターリソースが変更された際に呼び出されるコールバック
        /// </summary>
        /// <param name="email">変更対象のメールアドレス</param>
        /// <param name="image">新しいアバター画像（取得失敗時はnull）</param>
        void OnAvatarResourceChanged(string email, Bitmap image);
    }

    /// <summary>
    ///     ユーザーアバター画像の取得・キャッシュ・管理を行うシングルトンクラス。
    ///     GravatarおよびGitHubアバターをサポートする。
    /// </summary>
    public partial class AvatarManager
    {
        /// <summary>
        ///     シングルトンインスタンスを取得する
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
        ///     GitHubのnoreplyメールアドレスからユーザー名を抽出する正規表現
        /// </summary>
        [GeneratedRegex(@"^(?:(\d+)\+)?(.+?)@.+\.github\.com$")]
        private static partial Regex REG_GITHUB_USER_EMAIL();

        private readonly Lock _synclock = new();
        /// <summary>アバター画像のローカルキャッシュディレクトリパス</summary>
        private string _storePath;
        /// <summary>アバター変更通知を受け取るホストのリスト</summary>
        private List<IAvatarHost> _avatars = new List<IAvatarHost>();
        /// <summary>メールアドレスをキーとしたアバター画像のキャッシュ</summary>
        private Dictionary<string, Bitmap> _resources = new Dictionary<string, Bitmap>();
        /// <summary>ダウンロードリクエスト待ちのメールアドレスセット</summary>
        private HashSet<string> _requesting = new HashSet<string>();
        /// <summary>デフォルトアバターとして登録済みのメールアドレスセット</summary>
        private HashSet<string> _defaultAvatars = new HashSet<string>();

        /// <summary>
        ///     アバターマネージャーを開始し、バックグラウンドでアバター取得ループを起動する。
        ///     デフォルトアバター（GitHub, Unreal）の読み込みも行う。
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

                    if (email == null)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    var md5 = GetEmailHash(email);
                    var matchGitHubUser = REG_GITHUB_USER_EMAIL().Match(email);
                    var url = $"https://www.gravatar.com/avatar/{md5}?d=404";
                    if (matchGitHubUser.Success)
                    {
                        var githubUser = matchGitHubUser.Groups[2].Value;
                        if (githubUser.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase))
                            githubUser = githubUser.Substring(0, githubUser.Length - 5);

                        url = $"https://avatars.githubusercontent.com/{githubUser}";
                    }

                    var localFile = Path.Combine(_storePath, md5);
                    Bitmap img = null;
                    try
                    {
                        using var client = new HttpClient();
                        client.Timeout = TimeSpan.FromSeconds(2);
                        var rsp = await client.GetAsync(url);
                        if (rsp.IsSuccessStatusCode)
                        {
                            using (var stream = rsp.Content.ReadAsStream())
                            {
                                using (var writer = File.Create(localFile))
                                {
                                    stream.CopyTo(writer);
                                }
                            }

                            using (var reader = File.OpenRead(localFile))
                            {
                                img = Bitmap.DecodeToWidth(reader, 128);
                            }
                        }
                    }
                    catch
                    {
                        // ignored
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
        ///     アバターリソース変更の通知を受け取るホストを登録する
        /// </summary>
        /// <param name="host">登録するホスト</param>
        public void Subscribe(IAvatarHost host)
        {
            _avatars.Add(host);
        }

        /// <summary>
        ///     アバターリソース変更の通知登録を解除する
        /// </summary>
        /// <param name="host">解除するホスト</param>
        public void Unsubscribe(IAvatarHost host)
        {
            _avatars.Remove(host);
        }

        /// <summary>
        ///     指定メールアドレスのアバター画像をリクエストする。
        ///     キャッシュにあればそれを返し、なければバックグラウンドでダウンロードをキューに入れる。
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
                    catch
                    {
                        // ignore
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
        ///     ローカルファイルからアバター画像を設定する
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
            catch
            {
                // ignore
            }
        }

        /// <summary>
        ///     埋め込みリソースからデフォルトアバターを読み込む
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
        ///     メールアドレスのMD5ハッシュを計算する（Gravatar URL生成用）
        /// </summary>
        /// <param name="email">対象のメールアドレス</param>
        /// <returns>MD5ハッシュの16進数文字列</returns>
        private string GetEmailHash(string email)
        {
            var lowered = email.ToLower(CultureInfo.CurrentCulture).Trim();
            var hash = MD5.HashData(Encoding.Default.GetBytes(lowered));
            var builder = new StringBuilder(hash.Length * 2);
            foreach (var c in hash)
                builder.Append(c.ToString("x2"));
            return builder.ToString();
        }

        /// <summary>
        ///     全登録ホストにアバターリソース変更を通知する
        /// </summary>
        /// <param name="email">変更対象のメールアドレス</param>
        /// <param name="image">新しいアバター画像</param>
        private void NotifyResourceChanged(string email, Bitmap image)
        {
            foreach (var avatar in _avatars)
                avatar.OnAvatarResourceChanged(email, image);
        }
    }
}
