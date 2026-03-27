using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     指定されたコミットのフルメッセージ（件名+本文）を取得するクラス。
///     git show --format=%B を使用する。
/// </summary>
public class QueryCommitFullMessage : Command
{
    /// <summary>
    ///     コンストラクタ。指定SHAのコミットメッセージ全文を取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="sha">対象コミットのSHA</param>
    public QueryCommitFullMessage(string repo, string sha)
    {
        WorkingDirectory = repo;
        Context = repo;
        // %B: コミットメッセージ全文（件名+本文）、-s: diffを表示しない
        Args = $"show --no-show-signature --format=%B -s {sha}";
    }

    /// <summary>
    ///     コマンドを同期的に実行し、コミットメッセージ全文を返す。
    /// </summary>
    /// <returns>コミットメッセージ全文。失敗時は空文字列</returns>
    public string GetResult()
    {
        var rs = ReadToEnd();
        return rs.IsSuccess ? rs.StdOut.TrimEnd() : string.Empty;
    }

    /// <summary>
    ///     コマンドを非同期で実行し、コミットメッセージ全文を返す。
    /// </summary>
    /// <returns>コミットメッセージ全文。失敗時は空文字列</returns>
    public async Task<string> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        return rs.IsSuccess ? rs.StdOut.TrimEnd() : string.Empty;
    }
}
