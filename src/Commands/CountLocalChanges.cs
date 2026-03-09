using System;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     ローカルの変更ファイル数をカウントするgitコマンド。
    ///     git status --porcelain を実行し、出力行数から変更数を算出する。
    /// </summary>
    public class CountLocalChanges : Command
    {
        /// <summary>
        ///     CountLocalChangesコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="includeUntracked">未追跡ファイルを含めるかどうか。</param>
        public CountLocalChanges(string repo, bool includeUntracked)
        {
            // -uall: 全ての未追跡ファイルを表示、-uno: 未追跡ファイルを非表示
            var option = includeUntracked ? "-uall" : "-uno";
            WorkingDirectory = repo;
            Context = repo;

            // git status --porcelain: マシン解析用のフォーマットで変更状態を出力する
            // --no-optional-locks: ロックを取得しない（バックグラウンド実行用）
            // --ignore-submodules=all: サブモジュールの変更を無視する
            Args = $"--no-optional-locks status {option} --ignore-submodules=all --porcelain";
        }

        /// <summary>
        ///     変更ファイル数を非同期で取得する。
        /// </summary>
        /// <returns>変更ファイルの数。エラー時は0を返す。</returns>
        public async Task<int> GetResultAsync()
        {
            // git statusの出力を全て読み取る
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (rs.IsSuccess)
            {
                // 出力行数 = 変更ファイル数（porcelain形式は1ファイル1行）
                var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                return lines.Length;
            }

            return 0;
        }
    }
}
