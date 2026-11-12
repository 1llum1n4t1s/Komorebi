using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 現在のブランチの upstream の完全 ref 名（例: refs/remotes/origin/main）を取得する。
/// upstream 1b080758: 「現在のブランチに集中」モードで upstream 側のコミットも表示するために使う。
/// </summary>
public class QueryUpstreamOfCurrentBranch : Command
{
    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    public QueryUpstreamOfCurrentBranch(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = "rev-parse --symbolic-full-name \"@{upstream}\"";

        // detached HEAD や upstream 未設定では失敗するのが正常なので、エラー表示しない。
        RaiseError = false;
    }

    /// <summary>
    /// upstream の完全 ref 名を返す。取得できない場合は空文字列。
    /// </summary>
    public async Task<string> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        return rs.IsSuccess ? rs.StdOut.Trim() : string.Empty;
    }
}
