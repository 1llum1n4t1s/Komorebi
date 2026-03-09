using System;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     ローカルブランチとリモートブランチ間のトラッキング状態（ahead/behind）を取得するクラス。
    ///     git rev-list --left-right を使用する。
    /// </summary>
    public class QueryTrackStatus : Command
    {
        /// <summary>
        ///     コンストラクタ。作業ディレクトリを設定する。
        /// </summary>
        /// <param name="repo">リポジトリのパス</param>
        public QueryTrackStatus(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        /// <summary>
        ///     コマンドを非同期で実行し、ローカルブランチのAhead/Behind情報を設定する。
        /// </summary>
        /// <param name="local">ローカルブランチ</param>
        /// <param name="remote">リモートブランチ</param>
        public async Task GetResultAsync(Models.Branch local, Models.Branch remote)
        {
            // --left-right: 左側（ローカル）と右側（リモート）のコミットを区別
            Args = $"rev-list --left-right {local.Head}...{remote.Head}";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return;

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // '>' で始まる行はリモート側のコミット（ローカルが遅れている）
                if (line[0] == '>')
                    local.Behind.Add(line.Substring(1));
                // '<' で始まる行はローカル側のコミット（ローカルが進んでいる）
                else
                    local.Ahead.Add(line.Substring(1));
            }
        }
    }
}
