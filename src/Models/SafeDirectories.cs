using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Komorebi.Models;

/// <summary>
/// Git が所有権の相違を検出したリポジトリの信頼確認を支援する。
/// </summary>
public static partial class SafeDirectories
{
    public static bool IsUntrustedRepository(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return false;

        return output.Contains("detected dubious ownership", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("unsafe repository", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     ファイルシステム上のリポジトリ境界と一致するパスだけを信頼対象にする。
    /// </summary>
    public static bool TryGetSafeDirectoryValue(string path, string output, out string value)
    {
        var normalized = Normalize(path);
        value = string.Empty;
        if (string.IsNullOrEmpty(normalized) || normalized.EndsWith("/*", StringComparison.Ordinal))
            return false;

        normalized = FindRepositoryRoot(normalized);
        if (normalized.EndsWith("/*", StringComparison.Ordinal))
            return false;

        // Komorebi: stderr 中のパス名自体にも偽の設定コマンドが入り得るため、
        // Git が表示した文字列だけを根拠に別パスや全リポジトリを信頼しない。
        var suggested = ParseSuggestedValue(output);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (suggested != "*" && !suggested.EndsWith("/*", StringComparison.Ordinal) &&
            normalized.Equals(Normalize(suggested), comparison))
        {
            value = suggested;
            return true;
        }

        // Git for Windows は UNC パスに %(prefix)/ を付ける。
        value = OperatingSystem.IsWindows() && normalized.StartsWith("//", StringComparison.Ordinal) ? $"%(prefix)/{normalized}" : normalized;
        return true;
    }

    private static string FindRepositoryRoot(string path)
    {
        if (!Directory.Exists(path))
            return path;

        // Komorebi: 子フォルダーを選んだ場合も実ルートを確認する。
        // 診断文の祖先パスを信用せず、最も近い .git（worktree ではファイル）で止める。
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            var git = Path.Combine(current, ".git");
            if (Directory.Exists(git) || File.Exists(git))
                return Normalize(current);

            // 親リポジトリ内に置かれた bare リポジトリの境界も越えない。
            if (File.Exists(Path.Combine(current, "HEAD")) &&
                Directory.Exists(Path.Combine(current, "objects")) &&
                Directory.Exists(Path.Combine(current, "refs")))
                return Normalize(current);
        }

        return path;
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        if (path.StartsWith("%(prefix)/", StringComparison.Ordinal))
            path = path[10..];

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }
    }

    private static string ParseSuggestedValue(string output)
    {
        if (string.IsNullOrEmpty(output))
            return string.Empty;

        var match = REG_SAFE_DIRECTORY_HINT().Match(output);
        if (!match.Success)
            return string.Empty;

        return Dequote(match.Groups["value"].Value.Trim());
    }

    /// <summary>
    ///     Git が提示するパスのシェル引用符を解除する。
    /// </summary>
    private static string Dequote(string value)
    {
        if (value.Length > 1 && value[0] == '\'')
        {
            var builder = new StringBuilder(value.Length);
            for (var i = 1; i < value.Length; i++)
            {
                var c = value[i];
                if (c != '\'')
                {
                    builder.Append(c);
                    continue;
                }

                if (i == value.Length - 1)
                    return builder.ToString();

                if (i + 3 < value.Length && value[i + 1] == '\\' && (value[i + 2] == '\'' || value[i + 2] == '!') && value[i + 3] == '\'')
                {
                    builder.Append(value[i + 2]);
                    i += 3;
                    continue;
                }

                return string.Empty;
            }

            return string.Empty;
        }

        if (value.Length > 1 && value[0] == '"' && value[^1] == '"')
            return value[1..^1];

        return value;
    }

    [GeneratedRegex(@"--add\s+safe\.directory\s+(?<value>[^\r\n]+)", RegexOptions.Multiline)]
    private static partial Regex REG_SAFE_DIRECTORY_HINT();
}
