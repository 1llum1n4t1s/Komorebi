using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     指定リビジョンにおけるファイルサイズを取得するクラス。
///     git ls-tree -l を使用する。
/// </summary>
public partial class QueryFileSize : Command
{
    /// <summary>
    ///     ls-tree -l の出力からファイルサイズを抽出する正規表現。
    /// </summary>
    [GeneratedRegex(@"^\d+\s+\w+\s+[0-9a-f]+\s+(\d+)\s+.*$")]
    private static partial Regex REG_FORMAT();

    /// <summary>
    ///     コンストラクタ。指定リビジョンのファイルサイズを取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="file">対象ファイルのパス</param>
    /// <param name="revision">対象リビジョン</param>
    public QueryFileSize(string repo, string file, string revision)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = $"ls-tree {revision} -l -- {file.Quoted()}";
    }

    /// <summary>
    ///     コマンドを非同期で実行し、ファイルサイズをバイト単位で返す。
    /// </summary>
    /// <returns>ファイルサイズ（バイト）。取得失敗時は0</returns>
    public async Task<long> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (rs.IsSuccess)
        {
            var match = REG_FORMAT().Match(rs.StdOut);
            if (match.Success)
                return long.Parse(match.Groups[1].Value);
        }

        return 0;
    }
}
