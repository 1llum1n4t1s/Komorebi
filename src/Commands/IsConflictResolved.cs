using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     マージコンフリクトが解決済みかどうかを判定するgitコマンド。
    ///     git diff --check を実行してコンフリクトマーカーの有無を確認する。
    /// </summary>
    public class IsConflictResolved : Command
    {
        /// <summary>
        ///     IsConflictResolvedコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="change">コンフリクト確認対象の変更ファイル。</param>
        public IsConflictResolved(string repo, Models.Change change)
        {
            var opt = new Models.DiffOption(change, true);

            WorkingDirectory = repo;
            Context = repo;

            // git diff --check: コンフリクトマーカーや空白エラーをチェックする
            // -a: バイナリファイルもテキストとして扱う
            // --ignore-cr-at-eol: 行末CRを無視する
            Args = $"diff --no-color --no-ext-diff -a --ignore-cr-at-eol --check {opt}";
        }

        /// <summary>
        ///     コンフリクト解決判定の結果を同期的に取得する。
        /// </summary>
        /// <returns>コンフリクトが解決済みであればtrue。</returns>
        public bool GetResult()
        {
            // --checkの終了コードが0ならコンフリクトマーカーが存在しない（解決済み）
            return ReadToEnd().IsSuccess;
        }

        /// <summary>
        ///     コンフリクト解決判定の結果を非同期で取得する。
        /// </summary>
        /// <returns>コンフリクトが解決済みであればtrue。</returns>
        public async Task<bool> GetResultAsync()
        {
            // --checkの終了コードが0ならコンフリクトマーカーが存在しない（解決済み）
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess;
        }
    }
}
