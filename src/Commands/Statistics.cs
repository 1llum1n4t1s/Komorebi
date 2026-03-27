using System.IO;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     コミット統計情報を取得するクラス。
///     git log を使用して、コミット日時と著者情報を取得する。
/// </summary>
public class Statistics : Command
{
    /// <summary>
    ///     コンストラクタ。統計情報取得用のコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="max">取得する最大コミット数</param>
    public Statistics(string repo, int max)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = $"log --date-order --branches --remotes -{max} --format=%ct$%aN±%aE";
    }

    /// <summary>
    ///     コマンドを非同期で実行し、統計モデルを返す。
    /// </summary>
    /// <returns>統計モデル</returns>
    public async Task<Models.Statistics> ReadAsync()
    {
        var statistics = new Models.Statistics();
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (!rs.IsSuccess)
            return statistics;

        var sr = new StringReader(rs.StdOut);
        while (sr.ReadLine() is { } line)
            ParseLine(statistics, line);

        // 全行の解析完了後、統計データを集計
        statistics.Complete();
        return statistics;
    }

    /// <summary>
    ///     1行分のログ出力を解析して統計モデルに追加する。
    /// </summary>
    /// <param name="statistics">統計モデル</param>
    /// <param name="line">解析対象の行（形式: タイムスタンプ$著者名±メール）</param>
    private void ParseLine(Models.Statistics statistics, string line)
    {
        // '$' でタイムスタンプと著者情報を分割
        var parts = line.Split('$', 2);
        if (parts.Length == 2 && double.TryParse(parts[0], out var date))
            statistics.AddCommit(parts[1], date);
    }
}
