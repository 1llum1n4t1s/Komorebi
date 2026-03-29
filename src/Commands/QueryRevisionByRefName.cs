using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 参照名（ブランチ名、タグ名など）からリビジョンSHAを解決するクラス。
/// git rev-parse を使用する。
/// </summary>
public class QueryRevisionByRefName : Command
{
    /// <summary>
    /// コンストラクタ。指定参照名のSHAを取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="refname">参照名（例: HEAD, refs/heads/main, v1.0）</param>
    public QueryRevisionByRefName(string repo, string refname)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = $"rev-parse {refname}";
    }

    /// <summary>
    /// コマンドを同期的に実行し、リビジョンSHAを返す。
    /// </summary>
    /// <returns>リビジョンSHA。失敗時はnull</returns>
    public string GetResult()
    {
        return Parse(ReadToEnd());
    }

    /// <summary>
    /// コマンドを非同期で実行し、リビジョンSHAを返す。
    /// </summary>
    /// <returns>リビジョンSHA。失敗時はnull</returns>
    public async Task<string> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        return Parse(rs);
    }

    /// <summary>
    /// コマンド結果を解析してSHA文字列を返す。
    /// </summary>
    /// <param name="rs">コマンド実行結果</param>
    /// <returns>リビジョンSHA。失敗時はnull</returns>
    private static string Parse(Result rs)
    {
        if (rs.IsSuccess && !string.IsNullOrEmpty(rs.StdOut))
            return rs.StdOut.Trim();

        return null;
    }
}
