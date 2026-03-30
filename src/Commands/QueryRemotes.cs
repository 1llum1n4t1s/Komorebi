using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// git remote -v を実行して、リモートリポジトリの一覧を取得するクラス。
/// </summary>
public partial class QueryRemotes : Command
{
    /// <summary>
    /// remote -v の出力行からリモート名とURLを抽出する正規表現。
    /// </summary>
    [GeneratedRegex(@"^([\w\.\-]+)\s*(\S+).*$")]
    private static partial Regex REG_REMOTE();

    /// <summary>
    /// コンストラクタ。リモート一覧を取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    public QueryRemotes(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = "remote -v";
    }

    /// <summary>
    /// コマンドを非同期で実行し、リモートリポジトリのリストを返す。
    /// </summary>
    /// <returns>リモートモデルのリスト</returns>
    public async Task<List<Models.Remote>> GetResultAsync()
    {
        var outs = new List<Models.Remote>();
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (!rs.IsSuccess)
            return outs;

        var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var match = REG_REMOTE().Match(line);
            if (!match.Success)
                continue;

            var remote = new Models.Remote()
            {
                Name = match.Groups[1].Value,
                URL = match.Groups[2].Value,
            };

            // 同名のリモートは重複して追加しない（fetch/push両方の行があるため）
            if (outs.Find(x => x.Name == remote.Name) is not null)
                continue;

            // SSH形式のURLからホスト名を抽出してHTTPS検証リストに追加
            if (remote.URL.StartsWith("git@", StringComparison.Ordinal))
            {
                var hostEnd = remote.URL.IndexOf(':', 4);
                if (hostEnd > 4)
                {
                    var host = remote.URL[4..hostEnd];
                    Models.HTTPSValidator.Add(host);
                }
            }

            outs.Add(remote);
        }

        return outs;
    }
}
