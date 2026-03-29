using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// git rev-parse --show-toplevel を実行して、リポジトリのルートパスを取得するクラス。
/// </summary>
public class QueryRepositoryRootPath : Command
{
    /// <summary>
    /// コンストラクタ。リポジトリのルートパスを取得するコマンドを設定する。
    /// </summary>
    /// <param name="path">作業ディレクトリのパス</param>
    public QueryRepositoryRootPath(string path)
    {
        WorkingDirectory = path;
        Args = "rev-parse --show-toplevel";
    }

    /// <summary>
    /// コマンドを同期的に実行し、結果を返す。
    /// </summary>
    /// <returns>コマンド実行結果（StdOutにルートパスを含む）</returns>
    public Result GetResult()
    {
        return ReadToEnd();
    }

    /// <summary>
    /// コマンドを非同期で実行し、結果を返す。
    /// </summary>
    /// <returns>コマンド実行結果（StdOutにルートパスを含む）</returns>
    public async Task<Result> GetResultAsync()
    {
        return await ReadToEndAsync().ConfigureAwait(false);
    }
}
