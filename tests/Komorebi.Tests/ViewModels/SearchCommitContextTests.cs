using System.Collections.Concurrent;

using Komorebi.Models;
using Komorebi.ViewModels;

namespace Komorebi.Tests.ViewModels;

public class SearchCommitContextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OlderSearch_CannotClearBusyOrOverwriteNewerResult(bool newerFirst)
    {
        var repo = CreateRepository();
        var requests = new ConcurrentDictionary<string, TaskCompletionSource<List<Commit>>>();
        var posts = new ConcurrentQueue<Action>();
        using var context = CreateContext(repo, (path, filter, method, current) => requests[filter].Task, posts.Enqueue);
        requests["old"] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        requests["new"] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Filter = "old";
        var old = context.StartSearchAsync();
        context.Filter = "new";
        var latest = context.StartSearchAsync();
        if (newerFirst)
        {
            requests["new"].SetResult([new() { SHA = "new" }]);
            await latest.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Drain(posts);
        }
        requests["old"].SetResult([new() { SHA = "old" }]);
        await old.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Drain(posts);
        if (!newerFirst)
        {
            Assert.True(context.IsQuerying);
            Assert.Null(context.Results);
            requests["new"].SetResult([new() { SHA = "new" }]);
            await latest.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Drain(posts);
        }
        Assert.False(context.IsQuerying);
        Assert.NotNull(context.Results);
        Assert.Equal("new", Assert.Single(context.Results).SHA);
    }

    [Theory]
    [InlineData("dispose")]
    [InlineData("end")]
    [InlineData("clear")]
    public async Task QueuedCompletion_AfterInvalidationDoesNotRestoreResults(string action)
    {
        var repo = CreateRepository();
        var posts = new ConcurrentQueue<Action>();
        using var context = CreateContext(repo, (_, _, _, _) => Task.FromResult<List<Commit>>([new() { SHA = "late" }]), posts.Enqueue);
        context.Filter = "query";
        await context.StartSearchAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        if (action == "dispose")
            context.Dispose();
        else if (action == "end")
            context.EndSearch();
        else
            context.ClearFilter();
        Drain(posts);
        Assert.Null(context.Results);
        Assert.False(context.IsQuerying);
    }

    [Fact]
    public async Task Query_UsesInputsCapturedBeforeWorkerStarts()
    {
        var repo = CreateRepository();
        var observed = new TaskCompletionSource<(string Path, string Filter, CommitSearchMethod Method, bool Current)>();
        using var context = CreateContext(repo, (path, filter, method, current) =>
        {
            observed.SetResult((path, filter, method, current));
            return Task.FromResult<List<Commit>>([]);
        }, _ => { });
        context.Filter = "original";
        var search = context.StartSearchAsync();
        context.Filter = "edited";
        context.Dispose();
        await search.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var input = await observed.Task;
        Assert.Equal(repo.FullPath, input.Path);
        Assert.Equal("original", input.Filter);
        Assert.Equal(CommitSearchMethod.ByMessage, input.Method);
        Assert.False(input.Current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedSuggestions_AfterDisposeDoNotTouchContext(bool files)
    {
        var repo = new Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        var queued = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var context = new SearchCommitContext(repo, (_, _, _, _) => Task.FromResult<List<Commit>>([]),
            action => queued.TrySetResult(action),
            _ => Task.FromResult<List<User>>([new("dummy±dummy@example.invalid")]),
            _ => Task.FromResult<List<string>>(["dummy.txt"]));
        // IsSearchingCommits=false により Method の自動コミット検索は開始させない。
        context.Method = (int)(files ? CommitSearchMethod.ByPath : CommitSearchMethod.ByAuthor);
        var completion = await queued.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        context.Dispose();
        completion();
        Assert.Null(context.Suggestions);
    }

    [Fact]
    public async Task SuggestionCompletion_FromPreviousMethodDoesNotRestoreSuggestions()
    {
        var repo = new Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        var queued = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var context = new SearchCommitContext(repo, (_, _, _, _) => Task.FromResult<List<Commit>>([]),
            action => queued.TrySetResult(action),
            _ => Task.FromResult<List<User>>([new("dummy±dummy@example.invalid")]),
            _ => Task.FromResult<List<string>>([]));
        context.Filter = "dummy";
        context.Method = (int)CommitSearchMethod.ByAuthor;
        var completion = await queued.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        context.Method = (int)CommitSearchMethod.ByMessage;
        repo.IsSearchingCommits = true;
        completion();
        Assert.Null(context.Suggestions);
    }

    private static Repository CreateRepository()
    {
        return new Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git")) { IsSearchingCommits = true };
    }

    private static SearchCommitContext CreateContext(Repository repo,
        Func<string, string, CommitSearchMethod, bool, Task<List<Commit>>> query, Action<Action> post)
    {
        return new SearchCommitContext(repo, query, post,
            _ => Task.FromResult<List<User>>([]), _ => Task.FromResult<List<string>>([]));
    }

    private static void Drain(ConcurrentQueue<Action> posts)
    {
        while (posts.TryDequeue(out var post))
            post();
    }
}
