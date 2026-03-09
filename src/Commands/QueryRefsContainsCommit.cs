using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     指定コミットを含むすべての参照（ブランチ、タグ）を取得するクラス。
    ///     git for-each-ref --contains を使用する。
    /// </summary>
    public class QueryRefsContainsCommit : Command
    {
        /// <summary>
        ///     コンストラクタ。指定コミットを含む参照を検索するコマンドを設定する。
        /// </summary>
        /// <param name="repo">リポジトリのパス</param>
        /// <param name="commit">対象コミットのSHA</param>
        public QueryRefsContainsCommit(string repo, string commit)
        {
            WorkingDirectory = repo;
            RaiseError = false;
            Args = $"for-each-ref --format=\"%(refname)\" --contains {commit}";
        }

        /// <summary>
        ///     コマンドを非同期で実行し、参照（ブランチ・タグ）のデコレータリストを返す。
        /// </summary>
        /// <returns>デコレータモデルのリスト</returns>
        public async Task<List<Models.Decorator>> GetResultAsync()
        {
            var outs = new List<Models.Decorator>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return outs;

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.EndsWith("/HEAD", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("refs/heads/", StringComparison.Ordinal))
                    outs.Add(new() { Name = line.Substring("refs/heads/".Length), Type = Models.DecoratorType.LocalBranchHead });
                else if (line.StartsWith("refs/remotes/", StringComparison.Ordinal))
                    outs.Add(new() { Name = line.Substring("refs/remotes/".Length), Type = Models.DecoratorType.RemoteBranchHead });
                else if (line.StartsWith("refs/tags/", StringComparison.Ordinal))
                    outs.Add(new() { Name = line.Substring("refs/tags/".Length), Type = Models.DecoratorType.Tag });
            }

            return outs;
        }
    }
}
