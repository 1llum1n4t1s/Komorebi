using Komorebi.Models;

namespace Komorebi.Tests.Models;

[Collection("CommitGraph")]
public class GraphHighlightingTests
{
    [Theory]
    [InlineData(CommitGraphHighlighting.SelectedCommitsOnly, true)]
    [InlineData(CommitGraphHighlighting.SelectedCommitsOnlyFirstParent, false)]
    public void SelectionHighlighting_ControlsSecondParentsWithoutChangingMergeState(CommitGraphHighlighting mode, bool secondParentHighlighted)
    {
        CommitGraph.SetDefaultPens();
        List<Commit> commits =
        [
            new() { SHA = "merge", Parents = ["first", "second"] },
            new() { SHA = "first", Parents = ["root"] },
            new() { SHA = "second", Parents = ["root"] },
            new() { SHA = "root" },
        ];
        HashSet<string> selected = ["merge"];
        CommitGraph.Parse(commits, false, mode, selected);
        Assert.True(commits[0].IsHighlightedInGraph);
        Assert.True(commits[1].IsHighlightedInGraph);
        Assert.Equal(secondParentHighlighted, commits[2].IsHighlightedInGraph);
        Assert.True(commits[3].IsHighlightedInGraph);
        Assert.All(commits, commit => Assert.False(commit.IsMerged));
        Assert.Equal(["merge"], selected);

        CommitGraph.Parse(commits, false, CommitGraphHighlighting.SelectedCommitsOnly, ["second"]);
        Assert.False(commits[0].IsHighlightedInGraph);
        Assert.False(commits[1].IsHighlightedInGraph);
        Assert.True(commits[2].IsHighlightedInGraph);
        Assert.True(commits[3].IsHighlightedInGraph);
    }
}
