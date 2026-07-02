using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// リポジトリの全コミット履歴から author/committer のユーザー一覧（重複除去済み）を取得するクラス。
/// コミット検索（作者/コミッター指定）のサジェスト機能で使用する。
/// </summary>
public class QueryUsers : Command
{
    /// <summary>
    /// コンストラクタ。全ブランチ・最大10万件のコミットから author/committer を収集する。
    /// </summary>
    /// <param name="repo">対象リポジトリのパス。</param>
    public QueryUsers(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
        RaiseError = false;
        Args = "log -100000 --all --format=%aN±%aE%n%cN±%cE";
    }

    /// <summary>
    /// コマンドを実行し、重複を除去したユーザー一覧を取得する。
    /// </summary>
    public async Task<List<Models.User>> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (!rs.IsSuccess)
            return [];

        var start = 0;
        var end = rs.StdOut.IndexOf('\n', start);
        var added = new HashSet<string>();
        var users = new List<Models.User>();
        while (end > 0)
        {
            var line = rs.StdOut.Substring(start, end - start);
            if (!string.IsNullOrEmpty(line) && !added.Contains(line))
            {
                var user = Models.User.FindOrAdd(line);
                users.Add(user);
                added.Add(line);
            }

            start = end + 1;
            if (start >= rs.StdOut.Length - 1)
                break;

            end = rs.StdOut.IndexOf('\n', start);
        }

        return users;
    }
}
