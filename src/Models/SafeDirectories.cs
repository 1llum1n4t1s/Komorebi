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

    /// <summary>
    /// エラー通知の「safe.directory に追加」ボタン（自動修正）で信頼してよい値を決める。
    /// git の stderr に含まれるパスは攻撃者がリポジトリ名などで細工できるため、次の条件をすべて満たすときだけ true を返す。
    /// (1) 候補が実在するディレクトリで、'*' を含まない（全リポジトリ信頼・ワイルドカードを作らない）
    /// (2) 候補が通知元 context のリポジトリと同一かその配下（無関係な場所を信頼しない）。文字列で判定してから
    ///     実在とリンク・ジャンクションの不在を確かめる（<see cref="IsAutoFixCandidate"/>）
    /// (3) ローカルで実行した QueryRepositoryRootPath の stderr（localStderr）が所有権エラーで、
    ///     その提案パスがあれば提案パスも context 配下
    /// (4) safe.directory の値は localStderr から <see cref="TryGetSafeDirectoryValue"/> で算出し、その対象も
    ///     context 配下（途中にリンク・ジャンクションが無いことも確認）
    /// UNC パスの %(prefix)/ は TryGetSafeDirectoryValue が保持する。
    /// </summary>
    /// <param name="candidate">通知メッセージから抽出したパス</param>
    /// <param name="contextRepository">通知元リポジトリのパス（RaiseException の context）</param>
    /// <param name="localStderr">candidate で QueryRepositoryRootPath を実行したときの stderr</param>
    /// <param name="value">git config --global --add safe.directory に渡す値</param>
    public static bool TryGetAutoFixValue(string candidate, string contextRepository, string localStderr, out string value)
    {
        value = string.Empty;
        // 事前検査（実在・'*' なし・context 配下）と同じ条件を最終判定でも繰り返す（呼び出し側が省いても安全側に倒す）
        if (!IsAutoFixCandidate(candidate, contextRepository))
            return false;

        var normalizedCandidate = Normalize(candidate);
        var normalizedContext = Normalize(contextRepository);

        if (!IsUntrustedRepository(localStderr))
            return false;

        // ローカル stderr に git の提案パス（git config --global --add safe.directory '<path>'）があれば、
        // それも context 配下であることを要求する（文字列判定のみ。提案が無い場合は算出値だけで判定する）。
        var suggested = ParseSuggestedValue(localStderr);
        if (!string.IsNullOrEmpty(suggested) &&
            (suggested.Contains('*') || !IsUnderContextByString(suggested, contextRepository, normalizedContext)))
            return false;

        if (!TryGetSafeDirectoryValue(normalizedCandidate, localStderr, out var computed))
            return false;

        // 算出値（%(prefix)/ を除いた実パス）も context 配下に限る。'*' は TryGetSafeDirectoryValue が既に除外している。
        // 文字列で配下を確かめてから、context からの各階層にリンク・ジャンクションが無いことも確かめる。
        var normalizedComputed = Normalize(computed);
        if (computed.Contains('*') ||
            !IsUnderContextByString(computed, contextRepository, normalizedContext) ||
            !PathContainment.HasNoReparsePointBelow(normalizedContext, normalizedComputed))
            return false;

        value = computed;
        return true;
    }

    /// <summary>
    /// path が context と同一かその配下かを、ファイルシステムに触れず文字列だけで判定する。
    /// UNC・デバイスパスは、context 自体も同じ形式の場合に限って許す（ローカルの context から
    /// 細工された \\host\share へ誘導されないため）。
    /// </summary>
    private static bool IsUnderContextByString(string path, string rawContext, string normalizedContext)
    {
        if (PathContainment.IsUncOrDevicePath(path) && !PathContainment.IsUncOrDevicePath(rawContext))
            return false;

        var normalized = Normalize(path);
        // GetFullPath 後に UNC 形式へ変わる入力（区切りの混在等）も同じ規則で拒否する
        if (string.IsNullOrEmpty(normalized) ||
            (PathContainment.IsUncOrDevicePath(normalized) && !PathContainment.IsUncOrDevicePath(normalizedContext)))
            return false;

        return IsSameOrUnder(normalized, normalizedContext);
    }

    /// <summary>
    /// 自動修正の候補パスが、git を起動する前の段階で検討に値するかを判定する。
    /// stderr 由来の任意のパスで git を起動しないための事前検査で、最終判定は <see cref="TryGetAutoFixValue"/> が行う。
    /// 通知に「safe.directory に追加」ボタンを出すかどうかの判定にも使う（git は起動しない）。
    /// <list type="number">
    /// <item>文字列だけの判定を先に行う: '*' を含まない、UNC・デバイスパスは context も同じ形式の場合のみ、
    /// context と同一かその配下。ここで落ちたものはファイルシステムに一切触れない
    /// （\\host\share を Directory.Exists すると context 外の SMB へ接続してしまうため）。</item>
    /// <item>文字列で配下と判定できたものだけ、context と候補の実在、および context から候補までの各階層に
    /// リンク・ジャンクションが無いこと（リポジトリ内のリンク経由で外へ出ない）を確かめる。</item>
    /// </list>
    /// </summary>
    public static bool IsAutoFixCandidate(string candidate, string contextRepository)
    {
        // (1) 文字列だけの判定（ファイルシステムに触れない）
        if (!IsAutoFixCandidateByString(candidate, contextRepository))
            return false;

        // (2) 実在とリンクの確認（文字列で context 配下と確定したものだけ）
        var normalizedContext = Normalize(contextRepository);
        var normalizedCandidate = Normalize(candidate);
        return Directory.Exists(normalizedContext) &&
            Directory.Exists(normalizedCandidate) &&
            PathContainment.HasNoReparsePointBelow(normalizedContext, normalizedCandidate);
    }

    /// <summary>
    /// <see cref="IsAutoFixCandidate"/> の文字列だけの段階（ファイルシステムに触れない）。
    /// ここで false になった候補は実在確認・リンク確認へ進まないので、UNC 等で外部へ接続しない。
    /// テストからこの段階だけを検証できるよう internal にしている。
    /// </summary>
    internal static bool IsAutoFixCandidateByString(string candidate, string contextRepository)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Contains('*') || string.IsNullOrWhiteSpace(contextRepository))
            return false;

        var normalizedContext = Normalize(contextRepository);
        return !string.IsNullOrEmpty(normalizedContext) &&
            IsUnderContextByString(candidate, contextRepository, normalizedContext);
    }

    /// <summary>正規化済みパス path が root と同一か root 配下か（'/' 区切りで比較し、名前の前方一致は配下とみなさない）。</summary>
    private static bool IsSameOrUnder(string path, string root) => PathContainment.IsSameOrUnder(path, root);

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
