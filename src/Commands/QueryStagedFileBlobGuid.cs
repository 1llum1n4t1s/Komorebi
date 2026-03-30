using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// ステージング済みファイルのBlobオブジェクトGUID（SHA）を取得するクラス。
/// git ls-files -s を使用する。
/// </summary>
public partial class QueryStagedFileBlobGuid : Command
{
    /// <summary>
    /// ls-files -s の出力からBlob SHAを抽出する正規表現。
    /// </summary>
    [GeneratedRegex(@"^\d+\s+([0-9a-f]+)\s+.*$")]
    private static partial Regex REG_FORMAT();

    /// <summary>
    /// コンストラクタ。指定ファイルのステージング済みBlob SHAを取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="file">対象ファイルのパス</param>
    public QueryStagedFileBlobGuid(string repo, string file)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = $"ls-files -s -- {file.Quoted()}";
    }

    /// <summary>
    /// コマンドを非同期で実行し、Blob SHAを返す。
    /// </summary>
    /// <returns>Blob SHA文字列。取得失敗時は空文字列</returns>
    public async Task<string> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        var match = REG_FORMAT().Match(rs.StdOut.Trim());
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
