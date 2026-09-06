using System;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>空白エラーと分離してテキスト・バイナリの競合状態を調べる。</summary>
public class QueryConflictFileState : Command
{
    public QueryConflictFileState(string repo, Models.Change change)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = $"diff --no-color --no-ext-diff --no-textconv --full-index --patch {new Models.DiffOption(change, true)}";
    }

    public async Task<Models.ConflictFileState> GetResultAsync()
    {
        var result = await ReadToEndAsync().ConfigureAwait(false);
        // 上流との差分: コマンド失敗を解決済みとして扱わない。
        return result.IsSuccess ? Parse(result.StdOut) : Models.ConflictFileState.Unknown;
    }

    internal static Models.ConflictFileState Parse(string output)
    {
        var state = Models.ConflictFileState.Resolved;
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("Binary files ", StringComparison.Ordinal))
                return Models.ConflictFileState.UnmergedBinary;
            if (line.StartsWith("++<<<<<<<", StringComparison.Ordinal) ||
                line.StartsWith("++=======", StringComparison.Ordinal) ||
                line.StartsWith("++>>>>>>>", StringComparison.Ordinal))
                state = Models.ConflictFileState.UnmergedText;
        }
        return state;
    }
}
