namespace Komorebi.Commands;

/// <summary>
/// git rev-parse --git-dir を実行して、.gitディレクトリのパスを取得するクラス。
/// </summary>
public class QueryGitDir : Command
{
    /// <summary>
    /// コンストラクタ。gitディレクトリを取得するコマンドを設定する。
    /// </summary>
    /// <param name="workDir">作業ディレクトリのパス</param>
    public QueryGitDir(string workDir)
    {
        WorkingDirectory = workDir;
        Args = "rev-parse --git-dir";
    }

    /// <summary>
    /// コマンドを同期的に実行し、.gitディレクトリの絶対パスを返す。
    /// </summary>
    /// <returns>.gitディレクトリの絶対パス。失敗時はnull</returns>
    public string GetResult()
    {
        return Parse(ReadToEnd());
    }

    /// <summary>
    /// コマンド結果を解析して、.gitディレクトリの絶対パスを返す。
    /// </summary>
    /// <param name="rs">コマンド実行結果</param>
    /// <returns>.gitディレクトリの絶対パス。失敗時はnull</returns>
    private string Parse(Result rs)
    {
        if (!rs.IsSuccess)
            return null;

        var stdout = rs.StdOut.Trim();
        if (string.IsNullOrEmpty(stdout))
            return null;

        // 相対パスの場合はWorkingDirectoryを基準に絶対パスに変換（基底クラスの共通メソッド使用）
        return ResolveGitRelativePath(stdout);
    }
}
