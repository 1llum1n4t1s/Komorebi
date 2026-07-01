using System;
using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// Git無視パターンの保存先ファイル（.gitignore / exclude）を表すレコード
/// </summary>
/// <param name="DisplayName">表示用のファイル名</param>
/// <param name="FullPath">ファイルシステム上のフルパス</param>
/// <param name="Pattern">このファイルに書き込む際の無視パターン</param>
/// <param name="IsLocalOnly">ローカル限定（.git/info/exclude）かどうか</param>
public record GitIgnoreFile(string DisplayName, string FullPath, string Pattern, bool IsLocalOnly)
{
    /// <summary>
    /// 指定パターンに対して選択可能な保存先ファイルの一覧を取得する
    /// </summary>
    /// <param name="repo">リポジトリのルートパス</param>
    /// <param name="gitDir">.gitディレクトリのパス</param>
    /// <param name="pattern">無視パターン</param>
    /// <returns>保存先候補の一覧</returns>
    public static List<GitIgnoreFile> GetSupported(string repo, string gitDir, string pattern)
    {
        List<GitIgnoreFile> supported = [];

        // リポジトリルートの .gitignore
        supported.Add(new(".gitignore", $"{repo}/.gitignore", pattern, false));

        // パターンがサブディレクトリ内のファイル/ディレクトリを指す場合は、
        // そのサブディレクトリの .gitignore も候補に加える
        var normalizedPattern = pattern.Replace('\\', '/').TrimEnd('/');
        var lastDirIdx = normalizedPattern.LastIndexOf('/');
        if (lastDirIdx > 0)
        {
            var parentDir = normalizedPattern.Substring(0, lastDirIdx);
            var overridedPattern = normalizedPattern.Substring(lastDirIdx + 1);
            supported.Add(new($"{parentDir}/.gitignore", $"{repo}/{parentDir}/.gitignore", overridedPattern, false));
        }

        // gitディレクトリ内の .git/info/exclude
        var normalizedGitDir = gitDir.Replace('\\', '/');
        var testGitDir = $"{repo}/.git".Replace('\\', '/');
        if (normalizedGitDir.Equals(testGitDir, StringComparison.Ordinal))
            supported.Add(new(".git/info/exclude", $"{normalizedGitDir}/info/exclude", pattern, true));
        else
            supported.Add(new(".git/info/exclude", $"{gitDir}/info/exclude", pattern, true));

        return supported;
    }
}
