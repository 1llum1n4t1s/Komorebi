using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     インタラクティブリベース用のコミット一覧を取得するクラス。
    ///     各コミットのフルメッセージ（本文含む）も取得する。
    /// </summary>
    public class QueryCommitsForInteractiveRebase : Command
    {
        /// <summary>
        ///     コンストラクタ。インタラクティブリベース対象のコミット範囲を設定する。
        /// </summary>
        /// <param name="repo">リポジトリのパス</param>
        /// <param name="on">リベースの基点となるコミット/ブランチ</param>
        public QueryCommitsForInteractiveRebase(string repo, string on)
        {
            // コミットメッセージ本文の区切りとしてユニークな境界文字列を生成
            _boundary = $"----- BOUNDARY OF COMMIT {Guid.NewGuid()} -----";

            WorkingDirectory = repo;
            Context = repo;
            // --topo-order: トポロジカル順、--cherry-pick --right-only: チェリーピック済みを除外、--no-merges: マージコミットを除外
            Args = $"log --topo-order --cherry-pick --right-only --no-merges --no-show-signature --decorate=full --format=\"%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%B%n{_boundary}\" {on}...HEAD";
        }

        /// <summary>
        ///     コマンドを非同期で実行し、インタラクティブリベース用のコミットリストを返す。
        /// </summary>
        /// <returns>インタラクティブリベース用コミットのリスト</returns>
        public async Task<List<Models.InteractiveCommit>> GetResultAsync()
        {
            var commits = new List<Models.InteractiveCommit>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
            {
                App.RaiseException(Context, $"Failed to query commits for interactive-rebase. Reason: {rs.StdErr}");
                return commits;
            }

            Models.InteractiveCommit current = null;

            var nextPartIdx = 0;
            var start = 0;
            var end = rs.StdOut.IndexOf('\n', start);
            while (end > 0)
            {
                var line = rs.StdOut.Substring(start, end - start);
                switch (nextPartIdx)
                {
                    case 0:
                        current = new Models.InteractiveCommit();
                        current.Commit.SHA = line;
                        commits.Add(current);
                        break;
                    case 1:
                        current.Commit.ParseParents(line);
                        break;
                    case 2:
                        current.Commit.ParseDecorators(line);
                        break;
                    case 3:
                        current.Commit.Author = Models.User.FindOrAdd(line);
                        break;
                    case 4:
                        current.Commit.AuthorTime = ulong.Parse(line);
                        break;
                    case 5:
                        current.Commit.Committer = Models.User.FindOrAdd(line);
                        break;
                    case 6:
                        current.Commit.CommitterTime = ulong.Parse(line);
                        break;
                    default:
                        var boundary = rs.StdOut.IndexOf(_boundary, end + 1, StringComparison.Ordinal);
                        if (boundary > end)
                        {
                            current.Message = rs.StdOut.Substring(start, boundary - start - 1);
                            end = boundary + _boundary.Length;
                        }
                        else
                        {
                            current.Message = rs.StdOut.Substring(start);
                            end = rs.StdOut.Length - 2;
                        }

                        nextPartIdx = -1;
                        break;
                }

                nextPartIdx++;

                start = end + 1;
                if (start >= rs.StdOut.Length - 1)
                    break;

                end = rs.StdOut.IndexOf('\n', start);
            }

            return commits;
        }

        /// <summary>コミットメッセージ本文の区切りに使用するユニークな境界文字列</summary>
        private readonly string _boundary;
    }
}
