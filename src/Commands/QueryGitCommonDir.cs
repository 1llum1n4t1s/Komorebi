using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// git rev-parse --git-common-dir を実行して、共通のgitディレクトリパスを取得するクラス。
/// ワークツリー環境では、メインリポジトリの.gitディレクトリを返す。
/// </summary>
public class QueryGitCommonDir : Command
{
    /// <summary>
    /// コンストラクタ。共通gitディレクトリを取得するコマンドを設定する。
    /// </summary>
    /// <param name="workDir">作業ディレクトリのパス</param>
    public QueryGitCommonDir(string workDir)
    {
        WorkingDirectory = workDir;
        Args = "rev-parse --git-common-dir";
        RaiseError = false;
    }

    /// <summary>
    /// コマンドを非同期で実行し、共通gitディレクトリの絶対パスを返す。
    /// </summary>
    /// <returns>共通gitディレクトリの絶対パス。失敗時は空文字列</returns>
    public async Task<string> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (!rs.IsSuccess || string.IsNullOrEmpty(rs.StdOut))
            return string.Empty;

        var dir = rs.StdOut.Trim();
        // 相対パスの場合はWorkingDirectoryを基準に絶対パスに変換（基底クラスの共通メソッド使用）
        return ResolveGitRelativePath(dir);
    }
}
