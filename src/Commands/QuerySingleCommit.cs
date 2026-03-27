using System;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     指定SHAの単一コミットの詳細情報を取得するクラス。
///     git show を使用して、SHA、親、デコレーション、著者、コミッター、件名を取得する。
/// </summary>
public class QuerySingleCommit : Command
{
    /// <summary>
    ///     コンストラクタ。指定SHAのコミット詳細を取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="sha">対象コミットのSHA</param>
    public QuerySingleCommit(string repo, string sha)
    {
        WorkingDirectory = repo;
        Context = repo;
        // 改行区切りでSHA、親、デコレーション、著者名±メール、タイムスタンプ等を取得
        Args = $"show --no-show-signature --decorate=full --format=%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%s -s {sha}";
    }

    /// <summary>
    ///     コマンドを同期的に実行し、コミットモデルを返す。
    /// </summary>
    /// <returns>コミットモデル。失敗時はnull</returns>
    public Models.Commit GetResult()
    {
        var rs = ReadToEnd();
        return Parse(rs);
    }

    /// <summary>
    ///     コマンドを非同期で実行し、コミットモデルを返す。
    /// </summary>
    /// <returns>コミットモデル。失敗時はnull</returns>
    public async Task<Models.Commit> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        return Parse(rs);
    }

    /// <summary>
    ///     コマンド結果を解析してCommitモデルを生成する。
    /// </summary>
    /// <param name="rs">コマンド実行結果</param>
    /// <returns>コミットモデル。失敗時はnull</returns>
    private Models.Commit Parse(Result rs)
    {
        if (!rs.IsSuccess || string.IsNullOrEmpty(rs.StdOut))
            return null;

        var commit = new Models.Commit();
        var lines = rs.StdOut.Split('\n');
        if (lines.Length < 8)
            return null;

        commit.SHA = lines[0];
        if (!string.IsNullOrEmpty(lines[1]))
            commit.Parents.AddRange(lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (!string.IsNullOrEmpty(lines[2]))
            commit.ParseDecorators(lines[2]);
        commit.Author = Models.User.FindOrAdd(lines[3]);
        commit.AuthorTime = ulong.Parse(lines[4]);
        commit.Committer = Models.User.FindOrAdd(lines[5]);
        commit.CommitterTime = ulong.Parse(lines[6]);
        commit.Subject = lines[7];

        return commit;
    }
}
