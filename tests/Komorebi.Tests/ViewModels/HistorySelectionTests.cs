using Komorebi.Models;

namespace Komorebi.Tests.ViewModels;

public class HistorySelectionTests
{
    [Fact]
    public void FailedNavigation_PreservesCurrentSelectionAndDetails()
    {
        var repo = new Komorebi.ViewModels.Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        using var histories = new Komorebi.ViewModels.Histories(repo);
        var selected = new Commit { SHA = "current" };
        var details = new DisposableDetails();
        histories.SelectedCommit = selected;
        histories.DetailContext = details;
        histories.ApplyNavigationResult(histories.BeginNavigationRequest(), null!);
        Assert.Same(selected, histories.SelectedCommit);
        Assert.Same(details, histories.DetailContext);
        Assert.Equal(0, details.DisposeCount);
    }

    [Theory]
    [InlineData("selection")]
    [InlineData("navigation")]
    [InlineData("newer-query")]
    public void LateNavigation_DoesNotOverwriteNewerIntent(string intent)
    {
        var repo = new Komorebi.ViewModels.Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        using var histories = new Komorebi.ViewModels.Histories(repo);
        histories.Commits = [new() { SHA = "a" }, new() { SHA = "b" }, new() { SHA = "c" }];
        var pending = histories.BeginNavigationRequest();
        if (intent == "selection")
            histories.Select(histories.Commits);
        else if (intent == "navigation")
            histories.NavigateTo("b");
        else
            histories.BeginNavigationRequest();
        var selected = histories.SelectedCommit;
        var details = histories.DetailContext;
        histories.ApplyNavigationResult(pending, new Commit { SHA = "old-result" });
        Assert.Same(selected, histories.SelectedCommit);
        Assert.Same(details, histories.DetailContext);
    }

    [Fact]
    public void QueuedNavigationResult_AfterDisposeDoesNotReviveDetails()
    {
        var repo = new Komorebi.ViewModels.Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        using var histories = new Komorebi.ViewModels.Histories(repo);
        // Dispatcherへ積んだ後にタブを閉じ、積んであった反映処理だけが遅れて実行される順序。
        var request = histories.BeginNavigationRequest();
        Action queuedCompletion = () => histories.ApplyNavigationResult(request, new Commit { SHA = "late" });
        histories.Dispose();
        var changes = new List<string?>();
        histories.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        queuedCompletion();
        Assert.Null(histories.DetailContext);
        Assert.Null(histories.SelectedCommit);
        Assert.Empty(changes);
    }

    [Fact]
    public void LateDetailAssignment_AfterDisposeReleasesIncomingDetails()
    {
        var repo = new Komorebi.ViewModels.Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        using var histories = new Komorebi.ViewModels.Histories(repo);
        var oldDetails = new DisposableDetails();
        histories.DetailContext = oldDetails;
        histories.Dispose();
        histories.Dispose();
        var lateDetails = new DisposableDetails();
        histories.DetailContext = lateDetails;
        Assert.Equal(1, oldDetails.DisposeCount);
        Assert.Equal(1, lateDetails.DisposeCount);
        Assert.Null(histories.DetailContext);
    }

    private sealed class DisposableDetails : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    [Fact]
    public void Refresh_RestoresMultipleSelectionsInHistoryOrderUsingNewInstances()
    {
        var repo = new Komorebi.ViewModels.Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        using var histories = new Komorebi.ViewModels.Histories(repo);
        histories.Commits = [new() { SHA = "a" }, new() { SHA = "b" }, new() { SHA = "c" }];
        histories.Select(new List<Commit> { histories.Commits[2], histories.Commits[0], histories.Commits[1] });
        List<Commit> refreshed = [new() { SHA = "d" }, new() { SHA = "a" }, new() { SHA = "c" }];
        histories.Commits = refreshed;
        Assert.Equal(["a", "c"], histories.SelectedCommits.Select(commit => commit.SHA));
        Assert.Same(refreshed[1], histories.SelectedCommits[0]);
        Assert.Same(refreshed[2], histories.SelectedCommits[1]);
    }

    [Fact]
    public void Refresh_ClearsSelectionAboveRetentionLimit()
    {
        var repo = new Komorebi.ViewModels.Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        using var histories = new Komorebi.ViewModels.Histories(repo);
        histories.Commits = Enumerable.Range(0, 21).Select(i => new Commit { SHA = i.ToString() }).ToList();
        histories.Select(histories.Commits);
        histories.Commits = [.. histories.Commits];
        Assert.Empty(histories.SelectedCommits);
    }
}
