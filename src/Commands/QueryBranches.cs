using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// git branchコマンドを実行して、ローカルブランチとリモートブランチの一覧を取得するクラス。
/// </summary>
public class QueryBranches : Command
{
    /// <summary>ローカルブランチ参照のプレフィックス</summary>
    private const string PREFIX_LOCAL = "refs/heads/";
    /// <summary>リモートブランチ参照のプレフィックス</summary>
    private const string PREFIX_REMOTE = "refs/remotes/";
    /// <summary>デタッチドHEAD（at）のプレフィックス</summary>
    private const string PREFIX_DETACHED_AT = "(HEAD detached at";
    /// <summary>デタッチドHEAD（from）のプレフィックス</summary>
    private const string PREFIX_DETACHED_FROM = "(HEAD detached from";

    /// <summary>
    /// コンストラクタ。カスタムフォーマットでブランチ情報を取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    public QueryBranches(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
        // NULL区切りでrefname、コミット日時、SHA、HEADフラグ、upstream、追跡状態、ワークツリーパスを取得
        Args = "branch -l --all -v --format=\"%(refname)%00%(committerdate:unix)%00%(objectname)%00%(HEAD)%00%(upstream)%00%(upstream:trackshort)%00%(worktreepath)\"";
    }

    /// <summary>
    /// コマンドを非同期で実行し、ブランチ一覧を返す。
    /// ローカルブランチとリモートブランチの追跡状態も解決する。
    /// </summary>
    /// <returns>ブランチモデルのリスト</returns>
    public async Task<List<Models.Branch>> GetResultAsync()
    {
        var branches = new List<Models.Branch>();
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (!rs.IsSuccess)
            return branches;

        var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var mismatched = new HashSet<string>(); // 追跡状態が不一致のブランチ
        var remotes = new Dictionary<string, Models.Branch>(); // リモートブランチのルックアップ用
        foreach (var line in lines)
        {
            var b = ParseLine(line, mismatched);
            if (b is not null)
            {
                branches.Add(b);
                if (!b.IsLocal)
                    remotes.Add(b.FullName, b);
            }
        }

        // ローカルブランチのupstream追跡状態を解決
        var trackTasks = new List<Task>();
        foreach (var b in branches)
        {
            if (b.IsLocal && !string.IsNullOrEmpty(b.Upstream))
            {
                if (remotes.TryGetValue(b.Upstream, out var upstream))
                {
                    b.IsUpstreamGone = false;

                    // 追跡状態が不一致の場合、詳細な追跡情報を並列取得
                    if (mismatched.Contains(b.FullName))
                        trackTasks.Add(new QueryTrackStatus(WorkingDirectory).GetResultAsync(b, upstream));
                }
                else
                {
                    // 対応するリモートブランチが見つからない場合、削除済みとマーク
                    b.IsUpstreamGone = true;
                }
            }
        }

        if (trackTasks.Count > 0)
            await Task.WhenAll(trackTasks).ConfigureAwait(false);

        return branches;
    }

    /// <summary>
    /// ブランチ情報の1行を解析してBranchモデルを生成する。
    /// </summary>
    /// <param name="line">NULL区切りのブランチ情報行</param>
    /// <param name="mismatched">追跡状態が不一致のブランチ名を記録するセット</param>
    /// <returns>解析されたブランチモデル。無効な行の場合はnull</returns>
    internal static Models.Branch ParseLine(string line, HashSet<string> mismatched)
    {
        var parts = line.Split('\0');
        if (parts.Length != 7)
            return null;

        var branch = new Models.Branch();
        var refName = parts[0];
        // リモートのHEAD参照はスキップ
        if (refName.EndsWith("/HEAD", StringComparison.Ordinal))
            return null;

        branch.IsDetachedHead = refName.StartsWith(PREFIX_DETACHED_AT, StringComparison.Ordinal) ||
            refName.StartsWith(PREFIX_DETACHED_FROM, StringComparison.Ordinal);

        if (refName.StartsWith(PREFIX_LOCAL, StringComparison.Ordinal))
        {
            branch.Name = refName[PREFIX_LOCAL.Length..];
            branch.IsLocal = true;
        }
        else if (refName.StartsWith(PREFIX_REMOTE, StringComparison.Ordinal))
        {
            var name = refName[PREFIX_REMOTE.Length..];
            var nameParts = name.Split('/', 2);
            if (nameParts.Length != 2)
                return null;

            branch.Remote = nameParts[0];
            branch.Name = nameParts[1];
            branch.IsLocal = false;
        }
        else
        {
            branch.Name = refName;
            branch.IsLocal = true;
        }

        ulong.TryParse(parts[1], out var committerDate);

        branch.FullName = refName;
        branch.CommitterDate = committerDate;
        branch.Head = parts[2];
        branch.IsCurrent = parts[3] == "*";
        branch.Upstream = parts[4];
        branch.IsUpstreamGone = false;

        if (branch.IsLocal &&
            !string.IsNullOrEmpty(branch.Upstream) &&
            !string.IsNullOrEmpty(parts[5]) &&
            !parts[5].Equals("=", StringComparison.Ordinal))
            mismatched.Add(branch.FullName);

        branch.WorktreePath = parts[6];
        return branch;
    }
}
