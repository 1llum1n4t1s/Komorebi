#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Komorebi.Models;

/// <summary>
/// SSHキーの情報を保持するモデル。ドロップダウン選択用。
/// </summary>
public class SSHKeyInfo
{
    /// <summary>SSHキーエントリの種別。</summary>
    public enum EntryType
    {
        /// <summary>指定なし（システムデフォルト）。</summary>
        None,
        /// <summary>グローバル設定を使用。</summary>
        GlobalFallback,
        /// <summary>検出された秘密鍵ファイル。</summary>
        Key,
        /// <summary>カスタムパスの秘密鍵。</summary>
        CustomKey,
        /// <summary>参照...（ファイルピッカーを開く）。</summary>
        Browse,
    }

    /// <summary>秘密鍵の絶対パス。センチネルエントリでは空文字列。</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>ファイル名（例: id_ed25519）。</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>対応する .pub ファイルのコメント部分（例: user@host）。</summary>
    public string Comment { get; init; } = string.Empty;

    /// <summary>エントリの種別。</summary>
    public EntryType Type { get; init; }

    /// <summary>ComboBox に表示する文字列。</summary>
    public string DisplayName
    {
        get
        {
            return Type switch
            {
                EntryType.None => App.Text("SSHKey.None"),
                EntryType.GlobalFallback => FormatKeyDisplay(App.Text("SSHKey.UseGlobal")),
                EntryType.Browse => App.Text("SSHKey.Browse"),
                EntryType.Key or EntryType.CustomKey => FormatKeyDisplay(null),
                _ => string.Empty,
            };
        }
    }

    /// <summary>キー表示名をフォーマットする。</summary>
    private string FormatKeyDisplay(string? prefix)
    {
        var name = FileName;
        if (!string.IsNullOrEmpty(Comment))
            name = $"{name} ({Comment})";
        return string.IsNullOrEmpty(prefix) ? name : $"{prefix}: {name}";
    }

    /// <summary>「指定なし」センチネル。</summary>
    public static SSHKeyInfo CreateNone() => new() { Type = EntryType.None };

    /// <summary>「グローバル設定を使用」エントリを作成する。</summary>
    public static SSHKeyInfo CreateGlobalFallback(string globalKeyPath)
    {
        var fileName = Path.GetFileName(globalKeyPath);
        var comment = ReadPublicKeyComment(globalKeyPath);
        return new SSHKeyInfo
        {
            Type = EntryType.GlobalFallback,
            FilePath = globalKeyPath,
            FileName = fileName,
            Comment = comment,
        };
    }

    /// <summary>「参照...」センチネル。</summary>
    public static SSHKeyInfo CreateBrowse() => new() { Type = EntryType.Browse };

    /// <summary>カスタムパスからエントリを作成する。</summary>
    public static SSHKeyInfo FromCustomPath(string path) => new()
    {
        Type = EntryType.CustomKey,
        FilePath = path,
        FileName = Path.GetFileName(path),
        Comment = ReadPublicKeyComment(path),
    };

    /// <summary>
    /// ~/.ssh/ ディレクトリをスキャンして秘密鍵ファイルを列挙する。
    /// 先頭行に "PRIVATE KEY" を含むファイルのみを対象とする。
    /// </summary>
    public static List<SSHKeyInfo> ScanSSHDirectory()
    {
        List<SSHKeyInfo> keys = [];
        var sshDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");

        if (!Directory.Exists(sshDir))
            return keys;

        try
        {
            foreach (var file in Directory.GetFiles(sshDir))
            {
                var name = Path.GetFileName(file);

                // 公開鍵や設定ファイルを除外
                if (name.EndsWith(".pub", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("config", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("known_hosts", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("known_hosts.old", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("authorized_keys", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("authorized_keys2", StringComparison.OrdinalIgnoreCase))
                    continue;

                // 先頭行で秘密鍵形式を判定
                try
                {
                    using var reader = new StreamReader(file);
                    var firstLine = reader.ReadLine();
                    if (firstLine is null || !firstLine.Contains("PRIVATE KEY"))
                        continue;
                }
                catch
                {
                    continue;
                }

                keys.Add(new SSHKeyInfo
                {
                    Type = EntryType.Key,
                    FilePath = file,
                    FileName = name,
                    Comment = ReadPublicKeyComment(file),
                });
            }
        }
        catch
        {
            // ディレクトリ読み取りエラーは無視
        }

        keys.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.Ordinal));
        return keys;
    }

    /// <summary>
    /// 秘密鍵パスに対応する .pub ファイルからコメント部分を抽出する。
    /// </summary>
    private static string ReadPublicKeyComment(string privateKeyPath)
    {
        var pubPath = privateKeyPath + ".pub";
        if (!File.Exists(pubPath))
            return string.Empty;

        try
        {
            var content = File.ReadAllText(pubPath).Trim();
            var parts = content.Split(' ');
            // ssh-rsa AAAAB3... user@host → 3番目以降がコメント
            return parts.Length >= 3 ? string.Join(' ', parts.Skip(2)) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
