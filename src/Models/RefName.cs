using System;

namespace Komorebi.Models;

/// <summary>Git の参照名規則で、作成するブランチ名・タグ名を検証する。</summary>
public static class RefName
{
    /// <summary>HEAD とオプション形式を除く、作成可能なブランチ名かを返す。</summary>
    public static bool IsValidBranchName(string? name)
    {
        return !string.Equals(name, "HEAD", StringComparison.Ordinal) && IsValidRefName(name);
    }

    /// <summary>作成可能なタグ名かを返す。</summary>
    public static bool IsValidTagName(string? name) => IsValidRefName(name);

    private static bool IsValidRefName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.StartsWith('-') || name.Equals("@", StringComparison.Ordinal))
            return false;

        if (name[0] == '/' || name[^1] is '/' or '.' ||
            name.Contains("//", StringComparison.Ordinal) ||
            name.Contains("..", StringComparison.Ordinal) ||
            name.Contains("@{", StringComparison.Ordinal))
            return false;

        foreach (var ch in name)
        {
            if (ch is <= ' ' or '\x7f' or '~' or '^' or ':' or '?' or '*' or '[' or '\\')
                return false;
        }

        foreach (var component in name.Split('/'))
        {
            if (component[0] == '.' || component.EndsWith(".lock", StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
