using System.Collections;
using Change = Komorebi.Models.Change;
using ChangeSelection = Komorebi.ViewModels.ChangeSelection;
using ChangeTreeNode = Komorebi.ViewModels.ChangeTreeNode;

namespace Komorebi.Tests.ViewModels;

/// <summary>
/// 変更選択の重複除去（フォルダー経由とファイル経由で同じ変更へ到達する場合）を検証する。
/// </summary>
public class ChangeSelectionTests
{
    [Fact]
    public void FolderAndItsFiles_AreCollectedOnce_InFirstSeenOrder()
    {
        var a = new Change { Path = "dir/a.txt" };
        var b = new Change { Path = "dir/b.txt" };
        var folder = new ChangeTreeNode("dir", true) { Children = [new ChangeTreeNode(a), new ChangeTreeNode(b)] };

        var selection = new ChangeSelection(new ArrayList { folder, new ChangeTreeNode(b), new ChangeTreeNode(a) });

        Assert.Equal(2, selection.Count);
        Assert.Same(a, selection[0]);
        Assert.Same(b, selection[1]);
        Assert.False(selection.IsSingleFolder);
    }

    [Fact]
    public void FilesBeforeFolder_KeepFirstSeenOrder()
    {
        var a = new Change { Path = "dir/a.txt" };
        var b = new Change { Path = "dir/b.txt" };
        var folder = new ChangeTreeNode("dir", true) { Children = [new ChangeTreeNode(a), new ChangeTreeNode(b)] };

        var selection = new ChangeSelection(new ArrayList { new ChangeTreeNode(b), folder });

        Assert.Equal(2, selection.Count);
        Assert.Same(b, selection[0]);
        Assert.Same(a, selection[1]);
    }

    [Fact]
    public void ListSelection_DuplicateChanges_AreCollectedOnce()
    {
        var a = new Change { Path = "a.txt" };
        var b = new Change { Path = "b.txt" };

        var selection = new ChangeSelection(new ArrayList { a, b, a });

        Assert.Equal(2, selection.Count);
        Assert.Same(a, selection[0]);
        Assert.Same(b, selection[1]);
    }

    [Fact]
    public void SingleFolder_IsReportedWithItsPath()
    {
        var a = new Change { Path = "dir/a.txt" };
        var folder = new ChangeTreeNode("dir", true) { Children = [new ChangeTreeNode(a)] };

        var selection = new ChangeSelection(new ArrayList { folder });

        Assert.True(selection.IsSingleFolder);
        Assert.Equal("dir", selection.SingleFolderPath);
        Assert.Single(selection);
    }

    [Fact]
    public void EmptyAndSingleFile_AreNotSingleFolder()
    {
        var empty = new ChangeSelection(new ArrayList());
        Assert.Empty(empty);
        Assert.False(empty.IsSingleFolder);

        var a = new Change { Path = "a.txt" };
        var single = new ChangeSelection(new ArrayList { new ChangeTreeNode(a) });
        Assert.Single(single);
        Assert.Same(a, single[0]);
        Assert.False(single.IsSingleFolder);
    }
}
