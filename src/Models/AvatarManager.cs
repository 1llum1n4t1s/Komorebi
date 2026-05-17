using System;
using System.Collections.Concurrent;
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
    /// シングルトンインスタンスを取得する。Lazy で thread-safe な遅延初期化を保証する。
    /// </summary>
    public static AvatarManager Instance => s_instance.Value;

    private static readonly Lazy<AvatarManager> s_instance = new(() => new AvatarManager(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// GitHubのnoreplyメールアドレスからユーザー名を抽出する正規表現
    /// </summary>
    [GeneratedRegex(@"^(?:(\d+)\+)?(.+?)@.+\.github\.com$")]
    private static partial Regex REG_GITHUB_USER_EMAIL();

    // HttpClientはスレッドセーフで再利用可能。ループ内で毎回new するとソケット枯渇の原因になる。
    // MaxResponseContentBufferSize: MITM や DNS 汚染で巨大レスポンスが返った場合の OOM を防ぐ（512KB 上限）。
    private static readonly HttpClient s_httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2),
        MaxResponseContentBufferSize = 512 * 1024,
    };
    /// <summary>HTTP レスポンスから受け入れる最大バイト数（Content-Length 早期判定用）</summary>
    private const long MaxAvatarBytes = 512 * 1024;
    /// <summary>並行ダウンロード上限（過剰並列でローカル/ネットワークリソースを枯渇させないため）</summary>
    private const int MaxParallelDownloads = 4;
    /// <summary>
    /// メモリ上に保持するアバター Bitmap の最大件数。
    /// 1 枚あたり ~100KB (128px PNG decoded) × 1024 件 ≒ 100MB を上限とする。
    /// 大規模リポジトリで数千の異なる commit author を閲覧してもキャッシュが
    /// 無制限に肥大化しないよう、超過時に <see cref="EvictOldest"/> で 25% を捨てる。
    /// </summary>
    private const int MaxCachedAvatars = 1024;
    /// <summary>超過時に削除する件数（一度に大量削除して GC を回すよりも段階的に減らす）</summary>
    private const int EvictionBatchSize = MaxCachedAvatars / 4;

    private readonly Lock _synclock = new();
    /// <summary>アバター画像のローカルキャッシュディレクトリパス</summary>
    private string _storePath;
    /// <summary>
    /// アバター変更通知を受け取るホストのセット。
    /// HashSet を使うことで Unsubscribe の計算量を O(n) から O(1) に、
    /// また同一ホストの二重登録を自然に防ぐ（View 再生成時の Subscribe/Unsubscribe の非対称対策）。
    /// </summary>
    private readonly HashSet<IAvatarHost> _avatars = [];
    /// <summary>
    /// メールアドレスをキーとしたアバター画像のキャッシュ。
    /// UI スレッドの Request / バックグラウンドダウンロードループの Post / SetFromLocal が
    /// 並行アクセスし得るため、ConcurrentDictionary でロックフリーに保護する。
    /// </summary>
    private readonly ConcurrentDictionary<string, Bitmap> _resources = new();
    /// <summary>
    /// アバターのキャッシュ追加順序を保持する FIFO キュー。
    /// LRU の代わりに FIFO ベースで evict する（厳密な LRU は読み取りごとの並べ替えコストが
    /// かかるが、avatar の使用パターンはほぼ「最近見たコミットの author」に偏るため
    /// FIFO でも実用上は十分に近い動作になる）。デフォルトアバターはエビクション対象外。
    /// </summary>
    private readonly ConcurrentQueue<string> _resourceInsertionOrder = new();
    /// <summary>ダウンロードリクエスト待ちのメールアドレスセット</summary>
    private readonly HashSet<string> _requesting = [];
    /// <summary>
    /// デフォルトアバターとして登録済みのメールアドレスセット。
    /// LoadDefaultAvatar 後は読み取り専用扱いだが、Request からの並行 Contains を
    /// 安全に走らせるため ConcurrentDictionary を使う（value 部はダミー）。
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _defaultAvatars = new();
    /// <summary>バックグラウンドダウンロードループのキャンセルトークンソース</summary>
    private CancellationTokenSource _cts;

    /// <summary>
    /// アバターマネージャーを開始し、バックグラウンドでアバター取得ループを起動する。
    /// デフォルトアバター（GitHub, Unreal）の読み込みも行う。
    /// </summary>
    public void Start()
    {
        // 再呼び出し防止（複数のバックグラウンドループが走るのを防ぐ）
        if (_cts is not null)
            return;

        _storePath = Path.Combine(Native.OS.DataDir, "avatars");
        if (!Directory.Exists(_storePath))
            Directory.CreateDirectory(_storePath);

        LoadDefaultAvatar("noreply@github.com", "github.png");
        LoadDefaultAvatar("unrealbot@epicgames.com", "unreal.png");

        _cts = new CancellationTokenSource();
        var token = _cts.Token; // ローカルにコピーして Stop() で _cts が null になっても安全

        Task.Run(async () =>
        {
            // 並列ダウンロードを抑制するセマフォ。Gravatar/GitHub avatars 共にレートリミットは緩いが、
            // 過剰並列で local I/O や Bitmap.DecodeToWidth が競合するのを避ける。
            using var semaphore = new SemaphoreSlim(MaxParallelDownloads, MaxParallelDownloads);
            var inflight = new List<Task>();

            while (!token.IsCancellationRequested)
            {
                List<string> batch = null;

                lock (_synclock)
                {
                    if (_requesting.Count > 0)
                    {
                        batch = new List<string>(Math.Min(_requesting.Count, MaxParallelDownloads));
                        foreach (var one in _requesting)
                        {
                            batch.Add(one);
                            if (batch.Count >= MaxParallelDownloads)
                                break;
                        }
                    }
                }

                if (batch is null)
                {
                    // Task.Delay(delay, token) は token キャンセル時に TaskCanceledException を投げるため
                    // アプリ終了時にデバッガ first-chance ノイズが出る。Register + TaskCompletionSource 方式で
                    // 例外を投げずに待機する。
                    var tcs = new TaskCompletionSource();
                    using (token.Register(() => tcs.TrySetResult()))
                    {
                        await Task.WhenAny(Task.Delay(100), tcs.Task).ConfigureAwait(false);
                    }
                    if (token.IsCancellationRequested)
                        break;
                    continue;
                }

                inflight.Clear();
                foreach (var email in batch)
                {
                    try
                    {
                        await semaphore.WaitAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    inflight.Add(DownloadOneAsync(email, semaphore, token));
                }
                await Task.WhenAll(inflight).ConfigureAwait(false);
            }

            // ReSharper disable once FunctionNeverReturns
        });
    }

    /// <summary>
    /// 1 件分のアバターをダウンロードし、結果を UI スレッドにポストする。並列実行用。
    /// </summary>
    private async Task DownloadOneAsync(string email, SemaphoreSlim semaphore, CancellationToken token)
    {
        try
        {
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
                // staticなHttpClientを再利用（ソケット枯渇防止・DNS再利用・接続プール活用）。
                // ResponseHeadersRead + Content-Length 早期判定で巨大レスポンスを弾く（OOM 対策）。
                using var rsp = await s_httpClient
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token)
                    .ConfigureAwait(false);
                if (rsp.IsSuccessStatusCode)
                {
                    var contentLength = rsp.Content.Headers.ContentLength;
                    if (contentLength.HasValue && contentLength.Value > MaxAvatarBytes)
                    {
                        Logger.Log($"アバターサイズ超過によりスキップ: {email} ({contentLength.Value} bytes)", LogLevel.Warning);
                    }
                    else
                    {
                        // パフォーマンス: MemoryStreamで一度だけ読み込み、ファイル保存とデコードを効率化
                        using var ms = new MemoryStream();
                        await rsp.Content.CopyToAsync(ms, token).ConfigureAwait(false);

                        // ファイルキャッシュに非同期で書き込み
                        ms.Position = 0;
                        await using (var writer = File.Create(localFile))
                        {
                            await ms.CopyToAsync(writer, token).ConfigureAwait(false);
                        }

                        // MemoryStreamから直接デコード（ディスク再読み込み不要）
                        ms.Position = 0;
                        img = Bitmap.DecodeToWidth(ms, 128);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // キャンセルは正常動作（アプリ終了/UI 遷移時）なのでログ不要
                return;
            }
            catch (HttpRequestException ex)
            {
                Logger.Log($"アバターダウンロード失敗（ネットワークエラー）: {email} - {ex.Message}", LogLevel.Warning);
            }
            catch (IOException ex)
            {
                // ソケット切断やTLSストリーム中断（フォントドロップダウンスクロール時等）
                Logger.Log($"アバターダウンロード失敗（I/Oエラー）: {email} - {ex.Message}", LogLevel.Warning);
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
                InsertCachedAvatar(email, img);
                NotifyResourceChanged(email, img);
            });
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// バックグラウンドのアバターダウンロードループを停止する。
    /// アプリケーション終了時に呼び出す。
    /// </summary>
    public void Stop()
    {
        // ループ側はローカル変数でトークンを参照するため、Dispose/null にしても安全。
        // null にすることで Start() の再呼び出しガードが解除され、再起動が可能になる。
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// アバターリソース変更の通知を受け取るホストを登録する。
    /// UI スレッドから呼ばれる前提だが、バックグラウンドの NotifyResourceChanged との
    /// 反復中変更を避けるため lock で保護する。
    /// </summary>
    /// <param name="host">登録するホスト</param>
    public void Subscribe(IAvatarHost host)
    {
        lock (_synclock)
            _avatars.Add(host);
    }

    /// <summary>
    /// アバターリソース変更の通知登録を解除する。
    /// Unsubscribe 漏れでシングルトン(_avatars)がホストを永久保持する事態を防ぐため、
    /// 呼び出し側(View)で OnAttachedToVisualTree / OnDetachedFromVisualTree の対称性を保つこと。
    /// </summary>
    /// <param name="host">解除するホスト</param>
    public void Unsubscribe(IAvatarHost host)
    {
        lock (_synclock)
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
            if (_defaultAvatars.ContainsKey(email))
                return null;

            _resources.TryRemove(email, out _);

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
                        InsertCachedAvatar(email, img);
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

            InsertCachedAvatar(email, image);

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
        // デフォルトアバターはエビクション対象外なので InsertCachedAvatar は呼ばず
        // 直接 _resources に登録する。_defaultAvatars に登録されているキーは EvictOldest で skip される。
        _resources.TryAdd(key, new Bitmap(icon));
        _defaultAvatars.TryAdd(key, 0);
    }

    /// <summary>
    /// アバターを LRU(FIFO) キャッシュに登録する。
    /// 容量超過時はデフォルトアバター以外を古い順に <see cref="EvictionBatchSize"/> 件削除する。
    ///
    /// 並行性: ConcurrentDictionary + ConcurrentQueue は個別にスレッドセーフだが、
    /// 「Count を見て evict する」全体は ABA 的に若干オーバーシュートし得る（複数スレッドが
    /// 同時に閾値を越えると evict が複数回走る）。実害は無く、最大でも 1〜2 バッチ分の余分
    /// な削除 / 保留に留まるため許容する。
    /// </summary>
    private void InsertCachedAvatar(string email, Bitmap image)
    {
        if (_defaultAvatars.ContainsKey(email))
        {
            _resources[email] = image;
            return;
        }

        _resources[email] = image;
        _resourceInsertionOrder.Enqueue(email);

        if (_resources.Count > MaxCachedAvatars)
            EvictOldest();
    }

    /// <summary>
    /// 古い順に <see cref="EvictionBatchSize"/> 件のアバターを削除する。
    /// デフォルトアバターはエビクションしない。削除した Bitmap は GC に任せる（明示的な Dispose は
    /// 別 View からの読み取り中に Bitmap が破棄されると Avalonia が AccessViolation を起こす危険があるため避ける）。
    /// </summary>
    private void EvictOldest()
    {
        var evicted = 0;
        while (evicted < EvictionBatchSize && _resourceInsertionOrder.TryDequeue(out var key))
        {
            if (_defaultAvatars.ContainsKey(key))
                continue;

            if (_resources.TryRemove(key, out _))
                evicted++;
        }

        if (evicted > 0)
            Logger.Log($"アバターキャッシュ {evicted} 件をエビクト（現在 {_resources.Count}/{MaxCachedAvatars}）", LogLevel.Debug);
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
    /// 全登録ホストにアバターリソース変更を通知する。
    /// 反復中に Subscribe/Unsubscribe が呼ばれても壊れないようスナップショットを取る。
    /// </summary>
    /// <param name="email">変更対象のメールアドレス</param>
    /// <param name="image">新しいアバター画像</param>
    private void NotifyResourceChanged(string email, Bitmap image)
    {
        IAvatarHost[] snapshot;
        lock (_synclock)
            snapshot = [.. _avatars];

        foreach (var avatar in snapshot)
            avatar.OnAvatarResourceChanged(email, image);
    }
}
