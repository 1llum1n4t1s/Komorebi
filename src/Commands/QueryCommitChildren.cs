using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     指定されたコミットの子コミット（直接の後継）を検索するクラス。
///     git rev-list --ancestry-path を使用して子コミットを特定する。
/// </summary>
public class QueryCommitChildren : Command
{
    /// <summary>
    ///     コンストラクタ。指定されたコミットからの子孫パスを検索するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="commit">対象コミットのSHA</param>
    /// <param name="max">取得する最大コミット数</param>
    public QueryCommitChildren(string repo, string commit, int max)
    {
        WorkingDirectory = repo;
        Context = repo;
        _commit = commit;
        // --ancestry-pathで指定コミットからの子孫のみを列挙し、--parentsで親情報を含める
        Args = $"rev-list -{max} --parents --branches --remotes --ancestry-path ^{commit}";
    }

    /// <summary>
    ///     コマンドを非同期で実行し、子コミットのSHAリストを返す。
    /// </summary>
    /// <returns>子コミットのSHAリスト</returns>
    public async Task<List<string>> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        var outs = new List<string>();
        if (rs.IsSuccess)
        {
            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // 親コミットに対象コミットを含む行のSHA（先頭40文字）を取得
                if (line.Contains(_commit))
                    outs.Add(line[..40]);
            }
        }

        return outs;
    }

    /// <summary>検索対象の親コミットSHA</summary>
    private string _commit;
}
