using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class ChangeTests
    {
        // -----------------------------------------------------------
        // Set() — ChangeState assignment
        // -----------------------------------------------------------

        [Fact]
        public void Set_Conflicted_SetsWorkTree()
        {
            var change = new Change { Path = "conflict.txt" };
            change.Set(ChangeState.None, ChangeState.Conflicted);

            Assert.Equal(ChangeState.Conflicted, change.WorkTree);
            Assert.True(change.IsConflicted);
        }

        [Fact]
        public void Set_IndexAndWorkTreeBothSet()
        {
            var change = new Change { Path = "file.txt" };
            change.Set(ChangeState.Modified, ChangeState.Modified);

            Assert.Equal(ChangeState.Modified, change.Index);
            Assert.Equal(ChangeState.Modified, change.WorkTree);
        }

        // -----------------------------------------------------------
        // Set() — Rename parsing (tab-separated)
        // -----------------------------------------------------------

        [Fact]
        public void Set_RenamedIndex_SplitsByTab()
        {
            var change = new Change { Path = "old.txt\tnew.txt" };
            change.Set(ChangeState.Renamed);

            Assert.Equal("new.txt", change.Path);
            Assert.Equal("old.txt", change.OriginalPath);
        }

        [Fact]
        public void Set_RenamedWorkTree_SplitsByTab()
        {
            var change = new Change { Path = "before.txt\tafter.txt" };
            change.Set(ChangeState.None, ChangeState.Renamed);

            Assert.Equal("after.txt", change.Path);
            Assert.Equal("before.txt", change.OriginalPath);
        }

        [Fact]
        public void Set_RenamedIndex_SplitsByArrow()
        {
            var change = new Change { Path = "old.txt -> new.txt" };
            change.Set(ChangeState.Renamed);

            Assert.Equal("new.txt", change.Path);
            Assert.Equal("old.txt", change.OriginalPath);
        }

        [Fact]
        public void Set_Renamed_NoSeparator_KeepsOriginalPath()
        {
            var change = new Change { Path = "single_path.txt" };
            change.Set(ChangeState.Renamed);

            Assert.Equal("single_path.txt", change.Path);
            Assert.Equal("", change.OriginalPath);
        }

        // -----------------------------------------------------------
        // Set() — Quoted path stripping
        // -----------------------------------------------------------

        [Fact]
        public void Set_QuotedPath_RemovesQuotes()
        {
            var change = new Change { Path = "\"path with spaces.txt\"" };
            change.Set(ChangeState.Modified);

            Assert.Equal("path with spaces.txt", change.Path);
        }

        [Fact]
        public void Set_RenamedWithQuotedPaths_RemovesQuotes()
        {
            var change = new Change { Path = "\"old path.txt\"\t\"new path.txt\"" };
            change.Set(ChangeState.Renamed);

            Assert.Equal("new path.txt", change.Path);
            Assert.Equal("old path.txt", change.OriginalPath);
        }

        // -----------------------------------------------------------
        // Set() — Empty path edge case (bug fix verification)
        // -----------------------------------------------------------

        [Fact]
        public void Set_EmptyPath_DoesNotThrow()
        {
            var change = new Change { Path = "" };

            var exception = Record.Exception(() => change.Set(ChangeState.Modified));

            Assert.Null(exception);
        }
    }
}
