using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     指定ハッシュがコミットオブジェクトのSHAかどうかを判定するgitコマンド。
    ///     git cat-file -t を実行してオブジェクトの種別を確認する。
    /// </summary>
    public class IsCommitSHA : Command
    {
        /// <summary>
        ///     IsCommitSHAコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="hash">判定対象のハッシュ文字列。</param>
        public IsCommitSHA(string repo, string hash)
        {
            WorkingDirectory = repo;

            // git cat-file -t: オブジェクトの種別（commit/tree/blob/tag）を取得する
            Args = $"cat-file -t {hash}";
        }

        /// <summary>
        ///     コミットSHA判定の結果を非同期で取得する。
        /// </summary>
        /// <returns>指定ハッシュがコミットオブジェクトであればtrue。</returns>
        public async Task<bool> GetResultAsync()
        {
            // 出力が"commit"であればコミットオブジェクトと判定する
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess && rs.StdOut.Trim().Equals("commit");
        }
    }
}
