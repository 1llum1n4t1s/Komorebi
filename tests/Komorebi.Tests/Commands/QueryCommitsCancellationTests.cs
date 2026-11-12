using Komorebi.Commands;

namespace Komorebi.Tests.Commands;

/// <summary>
/// QueryCommits の独自 Process 読取に ReadToEndAsync と同じ中断契約が適用されていることを検証する。
/// </summary>
public class QueryCommitsCancellationTests
{
    [Fact]
    public async Task AlreadyCancelled_ReturnsEmptyListInsteadOfNull()
    {
        using var repo = new GitFixture();
        repo.Commit("a.txt", "a", "first");
        repo.Commit("b.txt", "b", "second");

        // 比較対象: トークン未設定（Blame・Compare 等の呼出元と同じ）なら従来どおり全件返る
        var all = await new QueryCommits(repo.Root, "-n 10 HEAD").GetResultAsync();
        Assert.Equal(2, all.Count);

        // 中断済みトークンでは git を起動せず、null ではなく空リストを返す
        // （Repository.RefreshCommits はトークン確認より前に commits.Count を読むため null は NRE になる）
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var cancelled = await new QueryCommits(repo.Root, "-n 10 HEAD") { CancellationToken = cancellation.Token }.GetResultAsync();
        Assert.NotNull(cancelled);
        Assert.Empty(cancelled);
    }
}
