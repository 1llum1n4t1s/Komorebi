using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// リポジトリの全体的なステータス（現在ブランチ、ahead/behind数、ローカル変更数）を取得するクラス。
/// </summary>
public partial class QueryRepositoryStatus : Command
{
    /// <summary>ahead（先行コミット数）を抽出する正規表現</summary>
    [GeneratedRegex(@"ahead\s(\d+)")]
    private static partial Regex REG_AHEAD();

    /// <summary>behind（遅延コミット数）を抽出する正規表現</summary>
    [GeneratedRegex(@"behind\s(\d+)")]
    private static partial Regex REG_BEHIND();

    /// <summary>
    /// コンストラクタ。リポジトリステータスを取得するコマンドの基本設定を行う。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    public QueryRepositoryStatus(string repo)
    {
        WorkingDirectory = repo;
        RaiseError = false;
    }

    /// <summary>
    /// コマンドを非同期で実行し、リポジトリのステータス情報を返す。
    /// 現在ブランチ、ahead/behind数、ローカル変更数を取得する。
    /// </summary>
    /// <returns>リポジトリステータスモデル。失敗時はnull</returns>
    public async Task<Models.RepositoryStatus> GetResultAsync()
    {
        Args = "branch -l -v --format=\"%(refname:short)%00%(HEAD)%00%(upstream:track,nobracket)\"";
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (!rs.IsSuccess)
            return null;

        var status = new Models.RepositoryStatus();
        var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('\0');
            if (parts.Length != 3 || !parts[1].Equals("*", StringComparison.Ordinal))
                continue;

            status.CurrentBranch = parts[0];
            if (!string.IsNullOrEmpty(parts[2]))
                ParseTrackStatus(status, parts[2]);
        }

        status.LocalChanges = await new CountLocalChanges(WorkingDirectory, true) { RaiseError = false }
            .GetResultAsync()
            .ConfigureAwait(false);

        return status;
    }

    /// <summary>
    /// 追跡状態文字列からahead/behind数を解析する。
    /// </summary>
    /// <param name="status">設定先のリポジトリステータス</param>
    /// <param name="input">追跡状態文字列（例: "ahead 3, behind 2"）</param>
    private static void ParseTrackStatus(Models.RepositoryStatus status, string input)
    {
        var aheadMatch = REG_AHEAD().Match(input);
        if (aheadMatch.Success)
            status.Ahead = int.Parse(aheadMatch.Groups[1].Value);

        var behindMatch = REG_BEHIND().Match(input);
        if (behindMatch.Success)
            status.Behind = int.Parse(behindMatch.Groups[1].Value);
    }
}
