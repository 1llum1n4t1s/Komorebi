using Komorebi.Models;

namespace Komorebi.Tests.Models;

/// <summary>
/// 「現在のブランチに集中」モードの git log 引数を検証する（upstream 26fef126 / 1b080758 / 3e608dd5）。
/// </summary>
public class FocusOnCurrentBranchHistoryParamsTests
{
    [Fact]
    public void WithUpstream_ListsHeadAndQuotedUpstreamOnly()
    {
        var states = new RepositoryUIStates { EnableTopoOrderInHistory = true };
        states.HistoryFilters.Add(new HistoryFilter("refs/heads/other", FilterType.LocalBranch, FilterMode.Included));

        var args = states.BuildHistoryParamsForCurrentBranch("refs/remotes/origin/main");

        // 履歴フィルターや --branches 等は使わず、HEAD と upstream だけを辿る
        Assert.Equal("--topo-order HEAD \"refs/remotes/origin/main\"", args);
    }

    [Fact]
    public void WithoutUpstream_ListsHeadOnly()
    {
        var states = new RepositoryUIStates { EnableTopoOrderInHistory = false };
        Assert.Equal("--date-order HEAD", states.BuildHistoryParamsForCurrentBranch(string.Empty));
    }

    [Fact]
    public void ReflogFlag_IsIgnoredButOtherFlagsAreKept()
    {
        var states = new RepositoryUIStates
        {
            EnableTopoOrderInHistory = false,
            HistoryShowFlags = HistoryShowFlags.Reflog | HistoryShowFlags.FirstParentOnly | HistoryShowFlags.SimplifyByDecoration,
        };

        var args = states.BuildHistoryParamsForCurrentBranch(string.Empty);

        Assert.DoesNotContain("--reflog", args, StringComparison.Ordinal);
        Assert.Equal("--date-order --first-parent --simplify-by-decoration HEAD", args);
    }
}
