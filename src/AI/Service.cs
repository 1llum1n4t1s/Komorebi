// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;

namespace Komorebi.AI;

public class Service : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public static IReadOnlyList<Provider> AllProviders => [Provider.OpenAI, Provider.AzureOpenAI, Provider.Anthropic, Provider.Gemini];

    public string Name { get; set; } = string.Empty;
    public Provider Provider { get; set; } = Provider.OpenAI;
    public string Server { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ReasoningEffortLevel { get; set; } = "unspecified";
    public static IReadOnlyList<string> ReasoningEffortLevels => ["unspecified", "none", "minimal", "low", "medium", "high", "xhigh", "max"];

    [JsonIgnore]
    public string ApiKey
    {
        get => _apiKey;
        set
        {
            // キーを差し替えたら、保持している暗号文は再利用できなくなる。
            // （ここで消し損ねても getter が「暗号文の元の平文」と現在値を照合するので、古い暗号文は使われない）
            if (string.IsNullOrEmpty(value) || !string.Equals(_apiKey, value, StringComparison.Ordinal))
                _storedApiKey = null;

            _apiKey = value ?? string.Empty;
        }
    }

    /// <summary>
    /// preference.json へ保存される暗号化済み API キー。
    /// </summary>
    /// <remarks>
    /// 暗号文は「その暗号文の元になった平文」と対で保持し（<see cref="StoredCipher"/>）、次の規則で保存値を決める
    /// （詳細は <see cref="ResolveCipher"/>）。
    /// <list type="bullet">
    /// <item>元の平文が現在の ApiKey と一致すれば暗号文を再利用する（AES-GCM の乱数ノンスで毎回内容が変わり、
    /// 同一内容の保存スキップが効かなくなるのを防ぐ）。一致しなければ現在値で暗号化し直す
    /// （保存中に UI でキーを変えても、旧キーの暗号文が固定されて再起動後に巻き戻らない）。</item>
    /// <item>読み込んだ暗号文を復号できなかった（鍵ファイルの読込失敗・破損など）ため ApiKey が空のときは、
    /// その暗号文をそのまま書き戻す。鍵を作り直さず、ディスク上のキーを失わない。</item>
    /// <item>利用者が明示的に空にしたときは空を保存する。暗号化に失敗したときも、平文や別の値の暗号文は保存しない。</item>
    /// </list>
    /// </remarks>
    [JsonPropertyName("ApiKey")]
    public string ProtectedApiKey
    {
        // 保存（AutoFetch 等のワーカースレッド）と UI からの ApiKey 変更が並行しても、
        // 旧キーの暗号文が新キーの保存値として固定されないよう、暗号文と元の平文を対で扱う（ResolveCipher 参照）。
        get => ResolveCipher(ref _storedApiKey, _apiKey);
        set => _apiKey = LoadCipher(ref _storedApiKey, value);
    }

    public bool ReadApiKeyFromEnv
    {
        get => _readApiKeyFromEnv;
        set => SetProperty(ref _readApiKeyFromEnv, value);
    }

    private bool _readApiKeyFromEnv;
    public string AdditionalPrompt { get; set; } = string.Empty;

    /// <summary>リクエストへ追加するHTTPヘッダー。1行につき「名前: 値」で指定する。</summary>
    [JsonIgnore]
    public string ExtraHeaders
    {
        get => _extraHeaders;
        set
        {
            if (string.IsNullOrEmpty(value) || !string.Equals(_extraHeaders, value, StringComparison.Ordinal))
                _storedExtraHeaders = null;
            _extraHeaders = value ?? string.Empty;
        }
    }

    /// <summary>APIキーと同じ保護方式で追加ヘッダーを保存する。</summary>
    [JsonPropertyName("ExtraHeaders")]
    public string ProtectedExtraHeaders
    {
        // ProtectedApiKey と同じく、暗号文と元の平文の対で再利用を判定する
        get => ResolveCipher(ref _storedExtraHeaders, _extraHeaders);
        set => _extraHeaders = LoadCipher(ref _storedExtraHeaders, value);
    }

    private string _extraHeaders = string.Empty;
    /// <summary>保存用の追加ヘッダー暗号文とその元の平文（null は未保持）。</summary>
    private StoredCipher _storedExtraHeaders;

    /// <summary>
    /// 暗号文と、その暗号文の元になった平文の組。1 つの不変オブジェクトとして参照ごと差し替えるため、
    /// 別スレッドから読んでも「暗号文だけ新しく元の平文が古い」といった組み合わせの破れは起きない。
    /// <see cref="Source"/> が空文字なのは、読み込んだ暗号文を復号できなかった場合だけ。
    /// </summary>
    private sealed record StoredCipher(string Cipher, string Source);

    /// <summary>
    /// 保存する暗号文を決める。
    /// <list type="bullet">
    /// <item>保持している暗号文の元の平文が現在値と一致するときだけ再利用する。AES-GCM は毎回乱数ノンスで
    /// 暗号化するため、再暗号化すると内容が同じでも preference.json が毎回変わり、同一内容の保存スキップが効かない。</item>
    /// <item>一致しなければ現在値を暗号化し直す。以前は「暗号文が残っていれば再利用」だったため、ワーカースレッドの
    /// 保存中に UI でキーを変えると、旧キーの暗号文が setter の消去後に書き戻されて固定され、再起動後に巻き戻っていた。</item>
    /// <item>現在値が空のときは、読み込んだが復号できなかった暗号文（元の平文が空）だけをそのまま返す
    /// （鍵を作り直さない・暗号文を失わない契約）。利用者が明示的に空にした場合は空を返す。</item>
    /// <item>暗号化に失敗しても平文は絶対に返さない。別の値の暗号文も返さない。</item>
    /// </list>
    /// </summary>
    private static string ResolveCipher(ref StoredCipher slot, string plain)
    {
        // 判定と返却を同じスナップショットで行う（途中で別スレッドが差し替えても組は崩れない）
        var stored = Volatile.Read(ref slot);
        if (string.IsNullOrEmpty(plain))
            return stored is { Source.Length: 0 } ? stored.Cipher : string.Empty;

        if (stored is not null && string.Equals(stored.Source, plain, StringComparison.Ordinal))
            return stored.Cipher;

        var cipher = ApiKeyProtector.Protect(plain);
        if (string.IsNullOrEmpty(cipher))
            return string.Empty;

        Volatile.Write(ref slot, new StoredCipher(cipher, plain));
        return cipher;
    }

    /// <summary>
    /// preference.json から読んだ値を復号し、平文を返す。暗号文は復号結果（失敗時は空）を元の平文として保持する。
    /// 暗号文でない旧形式の平文は保持しない（次回保存時に暗号化して移行する。移行失敗時に平文を書き戻さない）。
    /// </summary>
    private static string LoadCipher(ref StoredCipher slot, string value)
    {
        var plain = ApiKeyProtector.UnprotectOrPlainText(value);
        Volatile.Write(ref slot, ApiKeyProtector.IsProtected(value) ? new StoredCipher(value, plain ?? string.Empty) : null);
        return plain ?? string.Empty;
    }


    public ApiKeyCredential Credential
    {
        get => new ApiKeyCredential(ResolvedApiKey);
    }

    public string ResolvedApiKey
    {
        get
        {
            // /rere 10 人分隊 P2#A1-#4: ReadApiKeyFromEnv 有効時に ApiKey (= 環境変数名) が空だと
            // Environment.GetEnvironmentVariable("") は実装依存で ArgumentException を投げる。
            // 事前にバリデーションして InvalidOperationException で統一する。
            if (ReadApiKeyFromEnv && string.IsNullOrWhiteSpace(ApiKey))
                throw new InvalidOperationException("API key environment variable name is not configured.");

            var key = ReadApiKeyFromEnv ? Environment.GetEnvironmentVariable(ApiKey) : ApiKey;
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("API key is not configured.");
            return key;
        }
    }

    /// <summary>
    /// 設定された <see cref="Server"/> URL の安全性検証。HTTPS / localhost / loopback 以外は弾く。
    /// ユーザーが誤って `http://` の Server を設定した場合に
    /// API key を平文で LAN/WAN に流す事故を防ぐ。各 Strategy で HTTP リクエスト送信前に呼ぶ。
    /// </summary>
    public void ValidateServerScheme()
    {
        if (string.IsNullOrWhiteSpace(Server))
            return; // 空文字列はデフォルト URL (https://) を使うので OK

        if (!Uri.TryCreate(Server, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"AI Server URL is invalid: {Server}");

        if (uri.Scheme == Uri.UriSchemeHttps)
            return;

        // localhost / 127.0.0.1 / ::1 は debug 用途として http://も許可
        var host = uri.Host;
        var isLoopback = uri.IsLoopback ||
                        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);
        if (uri.Scheme == Uri.UriSchemeHttp && isLoopback)
            return;

        throw new InvalidOperationException(
            $"AI Server URL must use HTTPS (or http://localhost for debug). Got: {uri.Scheme}://{host}");
    }

    private string _apiKey = string.Empty;
    /// <summary>保存用の API キー暗号文とその元の平文（null は未保持）。<see cref="ResolveCipher"/> 参照。</summary>
    private StoredCipher _storedApiKey;
}

/// <summary>
/// AI APIキーをローカル保存する際の AES-GCM 暗号化を担当する。
/// 暗号化に使う対称鍵 (KeySize=32) は <c>ai-api-key.key</c> に保存し、
/// Windows では DPAPI (CurrentUser) でラップして他ユーザーから読み取れないようにする。
/// Unix では chmod 600 で守る。
/// </summary>
internal static partial class ApiKeyProtector
{
    private const string Prefix = "komorebi:v1:aes:";
    private const string KeyFileName = "ai-api-key.key";
    /// <summary>Windows DPAPI でラップした鍵ファイルを表す行プレフィックス。旧式（平文 Base64）と区別するため。</summary>
    private const string DpapiPrefix = "dpapi:v1:";
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    /// <summary>CryptProtectData/CryptUnprotectData にユーザー UI を出させないフラグ。</summary>
    private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    /// <summary>
    /// 鍵生成・読み込みを直列化するロック。File.Exists → ReadAllText → WriteAllText の TOCTOU を防ぐ。
    /// </summary>
    private static readonly System.Threading.Lock s_keyLock = new();
    /// <summary>
    /// 1 セッション中の鍵をメモリキャッシュ。Lazy 化することで Protect/Unprotect の高頻度呼び出しでも
    /// ファイル I/O を発生させない。
    /// volatile は Double-Checked Locking パターンで ARM64 等の弱メモリモデル CPU で stale read を
    /// 防ぐため必要 ()。
    /// </summary>
    private static volatile byte[] s_cachedKey;
    private static string s_cachedKeyDir;

    internal static string KeyDirectoryOverride
    {
        get => s_keyDirectoryOverride;
        set
        {
            // テストで切り替えた際にキャッシュを破棄してテスト独立性を保つ
            lock (s_keyLock)
            {
                s_keyDirectoryOverride = value;
                s_cachedKey = null;
                s_cachedKeyDir = null;
            }
        }
    }
    private static string s_keyDirectoryOverride;

    internal static bool IsProtected(string value) => value?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            var key = GetOrCreateKey();
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var source = Encoding.UTF8.GetBytes(plainText);
            var cipher = new byte[source.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, source, cipher, tag);

            var payload = new byte[NonceSize + TagSize + cipher.Length];
            Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, payload, NonceSize + TagSize, cipher.Length);
            return Prefix + Convert.ToBase64String(payload);
        }
        catch (Exception ex)
        {
            Models.Logger.LogException("AI APIキーの暗号化に失敗しました", ex);
            return string.Empty;
        }
    }

    public static string UnprotectOrPlainText(string stored)
    {
        if (string.IsNullOrEmpty(stored))
            return string.Empty;

        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored;

        try
        {
            var payload = Convert.FromBase64String(stored[Prefix.Length..]);
            if (payload.Length < NonceSize + TagSize)
                return string.Empty;

            var nonce = payload[..NonceSize];
            var tag = payload[NonceSize..(NonceSize + TagSize)];
            var cipher = payload[(NonceSize + TagSize)..];
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(GetOrCreateKey(), TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex)
        {
            Models.Logger.LogException("AI APIキーの復号に失敗しました", ex);
            return string.Empty;
        }
    }

    private static byte[] GetOrCreateKey()
    {
        // double-checked locking: 高頻度の読み取りを lock 外で済ませる
        var cached = s_cachedKey;
        if (cached is not null)
            return cached;

        lock (s_keyLock)
        {
            if (s_cachedKey is not null)
                return s_cachedKey;

            s_cachedKey = LoadOrCreateKeyCore(out s_cachedKeyDir);
            return s_cachedKey;
        }
    }

    private static byte[] LoadOrCreateKeyCore(out string dir)
    {
        dir = s_keyDirectoryOverride;
        if (string.IsNullOrWhiteSpace(dir))
            dir = string.IsNullOrWhiteSpace(Native.OS.DataDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Komorebi")
                : Native.OS.DataDir;

        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, KeyFileName);

        if (File.Exists(file))
        {
            // 一時的な I/O・DPAPI 障害では元の鍵を保持し、次回読込での復旧を可能にする。
            var existing = TryReadKeyFile(file);
            if (existing is not null && existing.Length == KeySize)
                return existing;

            throw new CryptographicException("AI APIキー鍵ファイルを読み込めません。既存の鍵は変更していません。");
        }

        var key = RandomNumberGenerator.GetBytes(KeySize);
        try
        {
            WriteKeyFile(file, key, false);
        }
        catch (IOException) when (File.Exists(file))
        {
            // 別プロセスが先に鍵を作った場合、その鍵を上書きせず共有する。
            var existing = TryReadKeyFile(file);
            if (existing is not null && existing.Length == KeySize)
                return existing;
            throw;
        }
        return key;
    }

    /// <summary>
    /// 鍵ファイルを読み込んで生鍵を返す。Windows の旧形式（平文 Base64）はこの場で DPAPI に移行する。
    /// </summary>
    private static byte[] TryReadKeyFile(string file, Action<Stream, string> writeContents = null, Action<string, string> replaceFile = null)
    {
        var content = File.ReadAllText(file).Trim();
        if (string.IsNullOrEmpty(content))
            return null;

        // Windows: DPAPI 形式（dpapi:v1:Base64）。CurrentUser 範囲で他ユーザーは復号不能。
        if (OperatingSystem.IsWindows() && content.StartsWith(DpapiPrefix, StringComparison.Ordinal))
        {
            var protectedBlob = Convert.FromBase64String(content[DpapiPrefix.Length..]);
            return DpapiUnprotect(protectedBlob);
        }

        // 旧形式 / 非 Windows: 平文 Base64
        var raw = Convert.FromBase64String(content);
        if (raw.Length != KeySize)
            return null;

        // Windows のみ: 旧形式を DPAPI 形式に書き直してマイグレーション
        if (OperatingSystem.IsWindows())
        {
            try
            {
                WriteKeyFile(file, raw, true, writeContents, replaceFile);
            }
            catch (Exception ex)
            {
                Models.Logger.LogException("AI APIキー鍵ファイルの DPAPI 化に失敗", ex);
            }
        }
        return raw;
    }

    private static void WriteKeyFile(string file, byte[] rawKey, bool replaceExisting,
        Action<Stream, string> writeContents = null, Action<string, string> replaceFile = null)
    {
        var content = OperatingSystem.IsWindows()
            ? DpapiPrefix + Convert.ToBase64String(DpapiProtect(rawKey))
            : Convert.ToBase64String(rawKey);
        var stage = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var created = false;
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(stage, options))
            {
                created = true;
                if (writeContents is not null)
                    writeContents(stream, content);
                else
                {
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
                    writer.Write(content);
                    writer.Flush();
                }
                stream.Flush(true);
            }
            RestrictKeyFile(stage);

            // 上流との差分: 移行中の I/O 失敗で唯一の旧鍵を失わないよう、同じディレクトリで置換する。
            // 新規生成は上書きを禁止し、別プロセスが先に保存した鍵を保護する。
            if (replaceExisting)
            {
                if (replaceFile is not null)
                    replaceFile(stage, file);
                else
                    File.Replace(stage, file, null);
            }
            else
                File.Move(stage, file);
        }
        finally
        {
            if (created)
            {
                try
                {
                    File.Delete(stage);
                }
                catch (Exception ex)
                {
                    Models.Logger.LogException("AI APIキー鍵ファイルの一時ファイル清掃に失敗", ex);
                }
            }
        }
    }

    private static void RestrictKeyFile(string file)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // 権限変更に失敗しても、平文で設定ファイルへ保存するより安全なため続行する。
        }
    }

    // ===== Windows DPAPI P/Invoke =====
    // System.Security.Cryptography.ProtectedData は別 NuGet 配布で AOT trim ハンドリングが煩雑なため、
    // crypt32.dll を直接呼ぶ source-generated P/Invoke ([LibraryImport]) を使う。

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [SupportedOSPlatform("windows")]
    [LibraryImport("crypt32.dll", EntryPoint = "CryptProtectData", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CryptProtectData(
        ref DATA_BLOB pDataIn, IntPtr szDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DATA_BLOB pDataOut);

    [SupportedOSPlatform("windows")]
    [LibraryImport("crypt32.dll", EntryPoint = "CryptUnprotectData", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CryptUnprotectData(
        ref DATA_BLOB pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DATA_BLOB pDataOut);

    [SupportedOSPlatform("windows")]
    [LibraryImport("kernel32.dll", EntryPoint = "LocalFree")]
    private static partial IntPtr LocalFree(IntPtr hMem);

    [SupportedOSPlatform("windows")]
    private static byte[] DpapiProtect(byte[] data) => DpapiCrypt(data, protect: true);

    [SupportedOSPlatform("windows")]
    private static byte[] DpapiUnprotect(byte[] data) => DpapiCrypt(data, protect: false);

    [SupportedOSPlatform("windows")]
    private static byte[] DpapiCrypt(byte[] data, bool protect)
    {
        var pbData = Marshal.AllocHGlobal(data.Length);
        try
        {
            Marshal.Copy(data, 0, pbData, data.Length);
            var dataIn = new DATA_BLOB { cbData = data.Length, pbData = pbData };
            DATA_BLOB dataOut;
            var ok = protect
                ? CryptProtectData(ref dataIn, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out dataOut)
                : CryptUnprotectData(ref dataIn, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out dataOut);
            if (!ok)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var result = new byte[dataOut.cbData];
                Marshal.Copy(dataOut.pbData, result, 0, dataOut.cbData);
                return result;
            }
            finally
            {
                LocalFree(dataOut.pbData);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pbData);
        }
    }
}
