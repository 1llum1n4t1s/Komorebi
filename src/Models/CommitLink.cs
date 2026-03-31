using System;
using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// コミットをWebブラウザで表示するためのURLリンク情報を表すクラス。
/// 各種Gitホスティングサービスに対応。
/// </summary>
public class CommitLink
{
    /// <summary>
    /// リンクの表示名（サービス名とリポジトリパス）。
    /// </summary>
    public string Name { get; } = null;

    /// <summary>
    /// コミットURLのプレフィックス（SHAを末尾に追加して使用）。
    /// </summary>
    public string URLPrefix { get; } = null;

    /// <summary>
    /// CommitLinkの新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="name">リンクの表示名。</param>
    /// <param name="prefix">コミットURLのプレフィックス。</param>
    public CommitLink(string name, string prefix)
    {
        Name = name;
        URLPrefix = prefix;
    }

    /// <summary>
    /// リモートリポジトリのURLからコミットリンク一覧を生成する。
    /// </summary>
    /// <param name="remotes">リモートリポジトリのリスト。</param>
    /// <returns>対応するコミットリンクのリスト。</returns>
    public static List<CommitLink> Get(List<Remote> remotes)
    {
        List<CommitLink> outs = [];

        foreach (var remote in remotes)
        {
            if (remote.TryGetVisitURL(out var link))
            {
                // URLからホスト名とパスを抽出
                var uri = new Uri(link, UriKind.Absolute);
                var host = uri.Host;
                var route = uri.AbsolutePath.TrimStart('/');

                // ホスト名に基づいて各サービスのコミットURLパターンを生成
                if (host.Equals("github.com", StringComparison.Ordinal))
                    outs.Add(new($"GitHub ({route})", $"{link}/commit/"));
                else if (host.Contains("gitlab", StringComparison.Ordinal))
                    outs.Add(new($"GitLab ({route})", $"{link}/-/commit/"));
                else if (host.Equals("gitee.com", StringComparison.Ordinal))
                    outs.Add(new($"Gitee ({route})", $"{link}/commit/"));
                else if (host.Equals("bitbucket.org", StringComparison.Ordinal))
                    outs.Add(new($"BitBucket ({route})", $"{link}/commits/"));
                else if (host.Equals("codeberg.org", StringComparison.Ordinal))
                    outs.Add(new($"Codeberg ({route})", $"{link}/commit/"));
                else if (host.Equals("gitea.org", StringComparison.Ordinal))
                    outs.Add(new($"Gitea ({route})", $"{link}/commit/"));
                else if (host.Equals("git.sr.ht", StringComparison.Ordinal))
                    outs.Add(new($"sourcehut ({route})", $"{link}/commit/"));
                else if (host.Equals("gitcode.com", StringComparison.Ordinal))
                    outs.Add(new($"GitCode ({route})", $"{link}/commit/"));
            }
        }

        return outs;
    }
}
