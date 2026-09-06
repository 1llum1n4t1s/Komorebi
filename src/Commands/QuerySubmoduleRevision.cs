using System;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>サブモジュールのコミット情報と本文を一度に取得する。</summary>
public class QuerySubmoduleRevision : Command
{
    public QuerySubmoduleRevision(string repo, string revision)
    {
        WorkingDirectory = repo;
        Context = repo;
        RaiseError = false;
        _dirty = revision.EndsWith("-dirty", StringComparison.Ordinal);
        _revision = _dirty ? revision[..^6] : revision;
        Args = $"show --no-show-signature --decorate=full --format=%H%x00%P%x00%D%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s%x00%B -s {_revision.Quoted()}";
    }

    public async Task<Models.RevisionSubmodule> GetResultAsync()
    {
        var fallback = new Models.RevisionSubmodule { Commit = new Models.Commit { SHA = _revision } };
        // 未初期化のサブモジュールでは親リポジトリを誤って照会しない。
        if (!File.Exists(Path.Combine(WorkingDirectory, ".git")) && !Directory.Exists(Path.Combine(WorkingDirectory, ".git")))
            return fallback;
        var result = await ReadToEndAsync().ConfigureAwait(false);
        if (!result.IsSuccess)
            return fallback;
        var parts = result.StdOut.Split('\0', 9);
        if (parts.Length != 9 || !ulong.TryParse(parts[4], out var authorTime) || !ulong.TryParse(parts[6], out var committerTime))
            return fallback;
        var commit = new Models.Commit
        {
            SHA = parts[0],
            Author = Models.User.FindOrAdd(parts[3]),
            AuthorTime = authorTime,
            Committer = Models.User.FindOrAdd(parts[5]),
            CommitterTime = committerTime,
            Subject = parts[7],
        };
        commit.ParseParents(parts[1]);
        commit.ParseDecorators(parts[2]);
        return new Models.RevisionSubmodule
        {
            Commit = commit,
            FullMessage = new Models.CommitFullMessage { Message = parts[8].TrimEnd() },
            UncommittedChanges = _dirty ? await new CountLocalChanges(WorkingDirectory, true).GetResultAsync().ConfigureAwait(false) : 0,
        };
    }

    private readonly bool _dirty;
    private readonly string _revision;
}
