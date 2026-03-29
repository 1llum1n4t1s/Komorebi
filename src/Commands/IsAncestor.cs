using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// あるコミットが別のコミットの祖先かどうかを判定するgitコマンド。
/// git merge-base --is-ancestor を実行する。
/// </summary>
public class IsAncestor : Command
{
    /// <summary>
    /// IsAncestorコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="checkPoint">祖先かどうかを確認するコミットSHA。</param>
    /// <param name="endPoint">子孫側のコミットSHA。</param>
    public IsAncestor(string repo, string checkPoint, string endPoint)
    {
        WorkingDirectory = repo;
        Context = repo;

        // git merge-base --is-ancestor: checkPointがendPointの祖先かどうかを判定する
        Args = $"merge-base --is-ancestor {checkPoint} {endPoint}";
    }

    /// <summary>
    /// 祖先判定の結果を非同期で取得する。
    /// </summary>
    /// <returns>checkPointがendPointの祖先であればtrue。</returns>
    public async Task<bool> GetResultAsync()
    {
        // コマンドの終了コードが0なら祖先である
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        return rs.IsSuccess;
    }
}
