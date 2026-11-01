// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// リポジトリの全体的なステータス（現在ブランチ、ahead/behind数、ローカル変更数）を取得するクラス。
/// </summary>
public class QueryRepositoryStatus : Command
{
    /// <summary>
    /// コンストラクタ。リポジトリステータスを取得するコマンドの基本設定を行う。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    public QueryRepositoryStatus(string repo)
    {
        WorkingDirectory = repo;
        RaiseError = false;
        Args = "--no-optional-locks status --porcelain=v2 -b -z -uall --ignore-submodules=dirty";
    }

    /// <summary>
    /// コマンドを非同期で実行し、リポジトリのステータス情報を返す。
    /// 現在ブランチ、ahead/behind数、ローカル変更数を取得する。
    /// </summary>
    /// <returns>リポジトリステータスモデル。失敗時はnull</returns>
    public async Task<Models.RepositoryStatus> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (rs.IsSuccess)
            return ParseOutput(rs.StdOut);

        // upstream と異なり、作業ツリーのない bare リポジトリでもブランチの状態を表示する。
        if (CancellationToken.IsCancellationRequested ||
            !await new IsBareRepository(WorkingDirectory) { RaiseError = false, CancellationToken = CancellationToken }
                .GetResultAsync().ConfigureAwait(false))
            return null;

        var statusArgs = Args;
        try
        {
            Args = "branch -l --format=\"%(refname:short)%00%(HEAD)%00%(upstream:track,nobracket)\"";
            rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess ? ParseBareOutput(rs.StdOut) : null;
        }
        finally
        {
            Args = statusArgs;
        }
    }

    /// <summary>bare リポジトリの現在ブランチと追跡状態を解析する。</summary>
    internal static Models.RepositoryStatus ParseBareOutput(string output)
    {
        var status = new Models.RepositoryStatus();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\0');
            if (parts.Length != 3 || parts[1] != "*")
                continue;

            status.CurrentBranch = parts[0];
            foreach (var track in parts[2].Split(", ", StringSplitOptions.RemoveEmptyEntries))
            {
                if (track.StartsWith("ahead ", StringComparison.Ordinal) && int.TryParse(track[6..], out var ahead))
                    status.Ahead = ahead;
                else if (track.StartsWith("behind ", StringComparison.Ordinal) && int.TryParse(track[7..], out var behind))
                    status.Behind = behind;
            }
        }
        return status;
    }

    /// <summary>NUL 区切りの status 出力を解析し、改行を含むパスやリネーム元を数え間違えない。</summary>
    internal static Models.RepositoryStatus ParseOutput(string output)
    {
        var status = new Models.RepositoryStatus();
        var oid = string.Empty;
        var records = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < records.Length; i++)
        {
            var record = records[i];
            if (record.StartsWith("# branch.oid ", StringComparison.Ordinal))
                oid = record[13..];
            else if (record.StartsWith("# branch.head ", StringComparison.Ordinal))
                status.CurrentBranch = record[14..];
            else if (record.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                var counts = record[12..].Split(' ');
                if (counts.Length == 2 && int.TryParse(counts[0].TrimStart('+'), out var ahead) &&
                    int.TryParse(counts[1].TrimStart('-'), out var behind))
                {
                    status.Ahead = ahead;
                    status.Behind = behind;
                }
            }
            else if (record.StartsWith("1 ", StringComparison.Ordinal) || record.StartsWith("2 ", StringComparison.Ordinal) ||
                record.StartsWith("u ", StringComparison.Ordinal) || record.StartsWith("? ", StringComparison.Ordinal))
            {
                status.LocalChanges++;
                if (record.StartsWith("2 ", StringComparison.Ordinal))
                    i++; // リネーム元パスは変更レコードではない。
            }
        }
        if (status.CurrentBranch == "(detached)")
            status.CurrentBranch = oid.Length > 10 ? $"({oid[..10]})" : "-";
        return status;
    }
}
