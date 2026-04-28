using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
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

internal static class ApiKeyProtector
{
    private const string Prefix = "komorebi:v1:aes:";
    private const string KeyFileName = "ai-api-key.key";
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    internal static string KeyDirectoryOverride { get; set; } = null;

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
        var dir = KeyDirectoryOverride;
        if (string.IsNullOrWhiteSpace(dir))
            dir = string.IsNullOrWhiteSpace(Native.OS.DataDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Komorebi")
                : Native.OS.DataDir;

        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, KeyFileName);
        if (File.Exists(file))
        {
            var existing = Convert.FromBase64String(File.ReadAllText(file).Trim());
            if (existing.Length == KeySize)
                return existing;
        }

        var key = RandomNumberGenerator.GetBytes(KeySize);
        File.WriteAllText(file, Convert.ToBase64String(key));
        RestrictKeyFile(file);
        return key;
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
}
