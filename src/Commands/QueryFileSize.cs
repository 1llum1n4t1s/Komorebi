
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 指定リビジョンにおけるファイルサイズを取得するクラス。
/// git cat-file -s を使用する。
/// </summary>
public class QueryFileSize : Command
{
    /// <summary>
    /// コンストラクタ。指定リビジョンのファイルサイズを取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="file">対象ファイルのパス</param>
    /// <param name="revision">対象リビジョン</param>
    public QueryFileSize(string repo, string file, string revision)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = $"cat-file -s {(revision + ":" + file).Quoted()}";
        RaiseError = false;
    }

    /// <summary>
    /// コマンドを非同期で実行し、ファイルサイズをバイト単位で返す。
    /// </summary>
    /// <returns>ファイルサイズ（バイト）。取得失敗時は0</returns>
    public async Task<long> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (rs.IsSuccess && long.TryParse(rs.StdOut.Trim(), out var size))
            return size;

        return 0;
    }
}
