using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// git logコマンドを実行して、コミット履歴を取得するクラス。
/// 通常のログ取得と検索フィルタ付きログ取得の2つのモードをサポートする。
/// </summary>
public class QueryCommits : Command
{
    /// <summary>
    /// コンストラクタ（通常のログ取得用）。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="limits">ログの範囲制限（例: HEAD~10..HEAD）</param>
    /// <param name="markMerged">マージ済みコミットをマークするかどうか</param>
    public QueryCommits(string repo, string limits, bool markMerged = true)
    {
        WorkingDirectory = repo;
        Context = repo;
        // NULL区切りでSHA、親、デコレーション、著者名±メール、タイムスタンプ、コミッター名±メール、タイムスタンプ、件名を取得
        Args = $"log --no-show-signature --decorate=full --format=%H%x00%P%x00%D%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s {limits}";
        _markMerged = markMerged;
    }

    /// <summary>
    /// コンストラクタ（検索フィルタ付きログ取得用）。
    /// 著者、コミッター、メッセージ、パス、内容変更による検索をサポートする。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="filter">検索フィルタ文字列</param>
    /// <param name="method">検索方法</param>
    /// <param name="onlyCurrentBranch">現在のブランチのみを検索するかどうか</param>
    public QueryCommits(string repo, string filter, Models.CommitSearchMethod method, bool onlyCurrentBranch)
    {
        var builder = new StringBuilder();
        builder.Append("log -1000 --date-order --no-show-signature --decorate=full --format=%H%x00%P%x00%D%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s ");

        if (!onlyCurrentBranch)
            builder.Append("--branches --remotes ");

        if (method == Models.CommitSearchMethod.ByAuthor)
        {
            builder.Append("-i --author=").Append(filter.Quoted());
        }
        else if (method == Models.CommitSearchMethod.ByCommitter)
        {
            builder.Append("-i --committer=").Append(filter.Quoted());
        }
        else if (method == Models.CommitSearchMethod.ByMessage)
        {
            var words = filter.Split([' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
                builder.Append("--grep=").Append(word.Trim().Quoted()).Append(' ');
            builder.Append("--all-match -i");
        }
        else if (method == Models.CommitSearchMethod.ByPath)
        {
            builder.Append("-- ").Append(filter.Quoted());
        }
        else
        {
            builder.Append("-G").Append(filter.Quoted());
        }

        WorkingDirectory = repo;
        Context = repo;
        Args = builder.ToString();
        _markMerged = false;
    }

    /// <summary>
    /// コマンドを非同期で実行し、コミットのリストを返す。
    /// マージ済みフラグの設定も行う。
    /// </summary>
    /// <returns>コミットモデルのリスト</returns>
    public async Task<List<Models.Commit>> GetResultAsync()
    {
        List<Models.Commit> commits = [];
        try
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);
            proc.Start();

            var findHead = false;
            while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                var commit = ParseCommitLine(line);
                if (commit is null)
                    continue;

                commits.Add(commit);
                findHead |= commit.IsMerged;
            }

            await proc.WaitForExitAsync().ConfigureAwait(false);

            // HEADが見つからなかった場合、現在ブランチのコミットハッシュと照合してマージ済みフラグを設定
            if (_markMerged && !findHead && commits.Count > 0)
            {
                var set = await new QueryCurrentBranchCommitHashes(WorkingDirectory, commits[^1].CommitterTime)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                foreach (var c in commits)
                {
                    if (set.Contains(c.SHA))
                    {
                        c.IsMerged = true;
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            App.RaiseException(Context, App.Text("Error.FailedToQueryCommits", e.Message));
        }

        return commits;
    }

    /// <summary>
    /// ログ出力の1行を解析してCommitモデルを生成する。
    /// </summary>
    /// <param name="line">NULL区切りのコミット情報行</param>
    /// <returns>解析されたコミットモデル。無効な行の場合はnull</returns>
    internal static Models.Commit ParseCommitLine(string line)
    {
        var parts = line.Split('\0');
        if (parts.Length != 8)
            return null;

        var commit = new Models.Commit() { SHA = parts[0] };
        commit.ParseParents(parts[1]);
        commit.ParseDecorators(parts[2]);
        commit.Author = Models.User.FindOrAdd(parts[3]);
        commit.AuthorTime = ulong.Parse(parts[4]);
        commit.Committer = Models.User.FindOrAdd(parts[5]);
        commit.CommitterTime = ulong.Parse(parts[6]);
        commit.Subject = parts[7];

        return commit;
    }

    /// <summary>マージ済みコミットをマークするかどうかのフラグ</summary>
    private bool _markMerged = false;
}
