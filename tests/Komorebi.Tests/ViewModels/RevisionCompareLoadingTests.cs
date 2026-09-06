using Komorebi.Models;

namespace Komorebi.Tests.ViewModels;

public class RevisionCompareLoadingTests
{
    [Fact]
    public async Task Loading_PreventsSwapAndResetIncludingOldMenuActions()
    {
        var repo = new Komorebi.ViewModels.Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        var left = new Commit { SHA = "left" };
        var right = new Commit { SHA = "right" };
        using var compare = new Komorebi.ViewModels.RevisionCompare(repo, left, right);
        // UIキューの完了反映前に連打する。GUIやDispatcherの起動は不要。
        compare.Swap();
        Assert.Same(left, compare.StartPoint);
        Assert.Same(right, compare.EndPoint);
        Assert.False(compare.CanResetToLeft);
        Assert.False(compare.CanResetToRight);
        await compare.ResetToLeftAsync(null!);
        await compare.ResetToRightAsync(null!);
        await compare.ResetMultipleToLeftAsync(null!);
        await compare.ResetMultipleToRightAsync(null!);

        compare.ApplyLoadedChanges([]);
        Assert.True(compare.CanResetToLeft);
        Assert.True(compare.CanResetToRight);
        // 前の比較で作成したメニューから渡される変更は新しい一覧に属さない。
        var stale = new Change { Path = "old.txt", Index = ChangeState.Added };
        await compare.ResetToLeftAsync(stale);
        await compare.ResetToRightAsync(stale);
        await compare.ResetMultipleToLeftAsync([stale]);
        await compare.ResetMultipleToRightAsync([stale]);
        compare.Swap();
        Assert.Same(right, compare.StartPoint);
        Assert.Same(left, compare.EndPoint);
        Assert.True(compare.IsLoading);
        compare.Swap();
        Assert.Same(right, compare.StartPoint);
    }

    [Fact]
    public void WorktreeSide_CannotBeUsedAsResetRevision()
    {
        var repo = new Komorebi.ViewModels.Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        using var compare = new Komorebi.ViewModels.RevisionCompare(repo, new Commit { SHA = "left" }, null!);
        compare.ApplyLoadedChanges([]);
        Assert.True(compare.CanResetToLeft);
        Assert.False(compare.CanResetToRight);
    }

    [Fact]
    public void SubmoduleLoading_PreventsSwap()
    {
        var left = new Commit { SHA = "left" };
        var right = new Commit { SHA = "right" };
        var compare = new Komorebi.ViewModels.SubmoduleRevisionCompare(new SubmoduleDiff
        {
            FullPath = Path.GetTempPath(),
            Old = new() { Commit = left },
            New = new() { Commit = right },
        });
        compare.Swap();
        Assert.Same(left, compare.Base);
        Assert.Same(right, compare.To);
        Assert.True(compare.IsLoading);
    }
}
