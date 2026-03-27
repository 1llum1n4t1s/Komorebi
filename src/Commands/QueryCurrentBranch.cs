using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     現在チェックアウトされているブランチ名を取得するクラス。
///     git branch --show-current を使用する。
/// </summary>
public class QueryCurrentBranch : Command
{
    /// <summary>
    ///     コンストラクタ。現在のブランチ名を取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    public QueryCurrentBranch(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = "branch --show-current";
    }

    /// <summary>
    ///     コマンドを同期的に実行し、現在のブランチ名を返す。
    /// </summary>
    /// <returns>現在のブランチ名</returns>
    public string GetResult()
    {
        return ReadToEnd().StdOut.Trim();
    }

    /// <summary>
    ///     コマンドを非同期で実行し、現在のブランチ名を返す。
    /// </summary>
    /// <returns>現在のブランチ名</returns>
    public async Task<string> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        return rs.StdOut.Trim();
    }
}
