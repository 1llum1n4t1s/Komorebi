using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Komorebi.AI;

public class Service
{
    public static IReadOnlyList<Provider> AllProviders => [Provider.OpenAI, Provider.AzureOpenAI, Provider.Anthropic, Provider.Gemini];

    public string Name { get; set; } = string.Empty;
    public Provider Provider { get; set; } = Provider.OpenAI;
    public string Server { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    [JsonIgnore]
    public string ApiKey
    {
        get => _apiKey;
        set => _apiKey = value ?? string.Empty;
    }

    [JsonPropertyName("ApiKey")]
    public string ProtectedApiKey
    {
        get => ApiKeyProtector.Protect(_apiKey);
        set => _apiKey = ApiKeyProtector.UnprotectOrPlainText(value);
    }

    public bool ReadApiKeyFromEnv { get; set; } = false;
    public string AdditionalPrompt { get; set; } = string.Empty;

    public ApiKeyCredential Credential
    {
        get => new ApiKeyCredential(ResolvedApiKey);
    }

    public string ResolvedApiKey
    {
        get
        {
            var key = ReadApiKeyFromEnv ? Environment.GetEnvironmentVariable(ApiKey) : ApiKey;
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("API key is not configured.");
            return key;
        }
    }

    private string _apiKey = string.Empty;
}

/// <summary>
/// AI APIキーをローカル保存する際の AES-GCM 暗号化を担当する。
/// 暗号化に使う対称鍵 (KeySize=32) は <c>ai-api-key.key</c> に保存し、
/// Windows では DPAPI (CurrentUser) でラップして他ユーザーから読み取れないようにする。
/// Unix では chmod 600 で守る。
/// </summary>
internal static class ApiKeyProtector
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
    private static readonly object s_keyLock = new();
    /// <summary>
    /// 1 セッション中の鍵をメモリキャッシュ。Lazy 化することで Protect/Unprotect の高頻度呼び出しでも
    /// ファイル I/O を発生させない。
    /// </summary>
    private static byte[] s_cachedKey;
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
            try
            {
                var existing = TryReadKeyFile(file);
                if (existing is not null && existing.Length == KeySize)
                    return existing;

                // 不正サイズや復号失敗: バックアップ取って新規生成
                Models.Logger.Log("AI APIキー鍵ファイルが破損しています。バックアップ後に再生成します。", Models.LogLevel.Warning);
                BackupKeyFile(file);
            }
            catch (Exception ex)
            {
                Models.Logger.LogException("AI APIキー鍵ファイルの読み込みに失敗。新規生成します", ex);
                BackupKeyFile(file);
            }
        }

        var key = RandomNumberGenerator.GetBytes(KeySize);
        WriteKeyFile(file, key);
        RestrictKeyFile(file);
        return key;
    }

    /// <summary>
    /// 鍵ファイルを読み込んで生鍵を返す。Windows の旧形式（平文 Base64）はこの場で DPAPI に移行する。
    /// </summary>
    private static byte[] TryReadKeyFile(string file)
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
                WriteKeyFile(file, raw);
                RestrictKeyFile(file);
            }
            catch (Exception ex)
            {
                Models.Logger.LogException("AI APIキー鍵ファイルの DPAPI 化に失敗", ex);
            }
        }
        return raw;
    }

    private static void WriteKeyFile(string file, byte[] rawKey)
    {
        if (OperatingSystem.IsWindows())
        {
            var protectedBlob = DpapiProtect(rawKey);
            File.WriteAllText(file, DpapiPrefix + Convert.ToBase64String(protectedBlob));
        }
        else
        {
            File.WriteAllText(file, Convert.ToBase64String(rawKey));
        }
    }

    private static void BackupKeyFile(string file)
    {
        try
        {
            File.Copy(file, file + ".bak", overwrite: true);
            // バックアップにも同じパーミッションを適用（Unix の chmod 600）
            RestrictKeyFile(file + ".bak");
        }
        catch (Exception ex)
        {
            // バックアップ失敗は致命ではないので続行
            Models.Logger.LogException("AI APIキー鍵ファイルのバックアップに失敗", ex);
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
    // crypt32.dll を直接呼ぶ source-generated P/Invoke を使う。

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [SupportedOSPlatform("windows")]
    [DllImport("crypt32.dll", EntryPoint = "CryptProtectData", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn, IntPtr szDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DATA_BLOB pDataOut);

    [SupportedOSPlatform("windows")]
    [DllImport("crypt32.dll", EntryPoint = "CryptUnprotectData", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DATA_BLOB pDataOut);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", EntryPoint = "LocalFree")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    [SupportedOSPlatform("windows")]
    private static byte[] DpapiProtect(byte[] data)
    {
        var pbData = Marshal.AllocHGlobal(data.Length);
        try
        {
            Marshal.Copy(data, 0, pbData, data.Length);
            var dataIn = new DATA_BLOB { cbData = data.Length, pbData = pbData };
            if (!CryptProtectData(ref dataIn, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out var dataOut))
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

    [SupportedOSPlatform("windows")]
    private static byte[] DpapiUnprotect(byte[] data)
    {
        var pbData = Marshal.AllocHGlobal(data.Length);
        try
        {
            Marshal.Copy(data, 0, pbData, data.Length);
            var dataIn = new DATA_BLOB { cbData = data.Length, pbData = pbData };
            if (!CryptUnprotectData(ref dataIn, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out var dataOut))
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
