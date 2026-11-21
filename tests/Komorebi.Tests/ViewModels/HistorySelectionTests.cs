using Komorebi.Models;

namespace Komorebi.Tests.ViewModels;

public class HistorySelectionTests
{
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
