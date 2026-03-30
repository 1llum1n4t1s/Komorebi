using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 指定ファイルがバイナリかどうかを判定するgitコマンド。
/// git diff --numstat の出力パターンでバイナリを検出する。
/// </summary>
public partial class IsBinary : Command
{
    /// <summary>
    /// バイナリファイル判定用の正規表現。
    /// numstatの出力が "- - filename" パターン（追加行・削除行が"-"）の場合バイナリと判定する。
    /// </summary>
    [GeneratedRegex(@"^\-\s+\-\s+.*$")]
    private static partial Regex REG_TEST();

    /// <summary>
    /// IsBinaryコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="commit">対象のコミットSHA。</param>
    /// <param name="path">判定対象のファイルパス。</param>
    public IsBinary(string repo, string commit, string path)
    {
        WorkingDirectory = repo;
        Context = repo;

        // git diff --numstat <empty_tree> <commit> -- <path>: 空ツリーとの差分統計でバイナリを判定する
        Args = $"diff --no-color --no-ext-diff --numstat {Models.Commit.EmptyTreeSHA1} {commit} -- {path.Quoted()}";

        // バイナリ判定はエラーを抑制する
        RaiseError = false;
    }

    /// <summary>
    /// バイナリ判定の結果を非同期で取得する。
    /// </summary>
    /// <returns>バイナリファイルであればtrue。</returns>
    public async Task<bool> GetResultAsync()
    {
        // numstatの出力が "- - ..." パターンであればバイナリファイルと判定する
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        return REG_TEST().IsMatch(rs.StdOut.Trim());
    }
}
