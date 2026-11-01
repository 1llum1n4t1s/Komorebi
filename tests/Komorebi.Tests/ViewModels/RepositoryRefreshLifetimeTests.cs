using System.Reflection;

using Komorebi.ViewModels;

namespace Komorebi.Tests.ViewModels;

public class RepositoryRefreshLifetimeTests
{
    [Theory]
    [InlineData("Branches")]
    [InlineData("Tags")]
    [InlineData("Commits")]
    [InlineData("WorkingCopyChanges")]
    [InlineData("Stashes")]
    public async Task CloseDuringRenewal_CancelsPublishedRequestAndRejectsFurtherRefresh(string kind)
    {
        var repo = new Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        var field = typeof(Repository).GetField($"_cancellationRefresh{kind}", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var previous = new CancellationTokenSource();
        field.SetValue(repo, previous);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var registration = previous.Token.Register(() =>
        {
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        });
        var refresh = (Action)typeof(Repository).GetMethod($"Refresh{kind}")!.CreateDelegate(typeof(Action), repo);
        var renewal = Task.Run(refresh, TestContext.Current.CancellationToken);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            var published = ((CancellationTokenSource)field.GetValue(repo)!).Token;
            // Cancel callback が止まっていても Close はロックを待たずに新要求を無効化できる。
            var close = Task.Run(repo.StopRefreshes, TestContext.Current.CancellationToken);
            await close.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.True(repo.IsClosed);
            Assert.True(published.IsCancellationRequested);
            refresh();
        }
        finally
        {
            release.Set();
        }

        await renewal.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Null(field.GetValue(repo));
    }

    [Fact]
    public void CancellationCallback_CanCloseBeforeRenewalReturns()
    {
        var repo = new Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        CancellationTokenSource source = new();
        using var registration = source.Token.Register(repo.StopRefreshes);
        Assert.False(repo.TryRenewCancellation(ref source, out _));
        Assert.True(repo.IsClosed);
        Assert.False(repo.TryRenewCancellation(ref source, out var rejected));
        Assert.True(rejected.IsCancellationRequested);
        source.Dispose();
    }
}
