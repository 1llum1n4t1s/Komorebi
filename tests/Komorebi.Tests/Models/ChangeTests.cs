using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class ChangeTests
    {
        // -----------------------------------------------------------
        // Set() — ChangeState assignment
        // -----------------------------------------------------------

        [Fact]
        public void Set_Modified_SetsIndexAndWorkTree()
        {
            var change = new Change { Path = "file.txt" };
            change.Set(ChangeState.Modified);

            Assert.Equal(ChangeState.Modified, change.Index);
            Assert.Equal(ChangeState.None, change.WorkTree);
        }

        [Fact]
        public void Set_Added_SetsIndex()
        {
            var change = new Change { Path = "new_file.txt" };
            change.Set(ChangeState.Added);

            Assert.Equal(ChangeState.Added, change.Index);
        }

        [Fact]
        public void Set_Deleted_SetsIndex()
        {
            var change = new Change { Path = "old_file.txt" };
            change.Set(ChangeState.Deleted);

            Assert.Equal(ChangeState.Deleted, change.Index);
        }

        [Fact]
        public void Set_Copied_SetsIndex()
        {
            var change = new Change { Path = "copied.txt" };
            change.Set(ChangeState.Copied);

            Assert.Equal(ChangeState.Copied, change.Index);
        }

        [Fact]
        public void Set_Untracked_SetsIndex()
        {
            var change = new Change { Path = "untracked.txt" };
            change.Set(ChangeState.Untracked);

            Assert.Equal(ChangeState.Untracked, change.Index);
        }

        [Fact]
        public void Set_TypeChanged_SetsIndex()
        {
            var change = new Change { Path = "link.txt" };
            change.Set(ChangeState.TypeChanged);

            Assert.Equal(ChangeState.TypeChanged, change.Index);
        }

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

        // -----------------------------------------------------------
        // Descriptor / marker properties
        // -----------------------------------------------------------

        [Theory]
        [InlineData(ChangeState.None, "Unknown")]
        [InlineData(ChangeState.Modified, "Modified")]
        [InlineData(ChangeState.TypeChanged, "Type Changed")]
        [InlineData(ChangeState.Added, "Added")]
        [InlineData(ChangeState.Deleted, "Deleted")]
        [InlineData(ChangeState.Renamed, "Renamed")]
        [InlineData(ChangeState.Copied, "Copied")]
        [InlineData(ChangeState.Untracked, "Untracked")]
        [InlineData(ChangeState.Conflicted, "Conflict")]
        public void WorkTreeDesc_ReturnsExpectedDescription(ChangeState state, string expected)
        {
            var change = new Change { Path = "file.txt" };
            change.Set(ChangeState.None, state);

            Assert.Equal(expected, change.WorkTreeDesc);
        }

        [Theory]
        [InlineData(ChangeState.None, "Unknown")]
        [InlineData(ChangeState.Modified, "Modified")]
        [InlineData(ChangeState.Added, "Added")]
        [InlineData(ChangeState.Deleted, "Deleted")]
        public void IndexDesc_ReturnsExpectedDescription(ChangeState state, string expected)
        {
            var change = new Change { Path = "file.txt" };
            change.Set(state);

            Assert.Equal(expected, change.IndexDesc);
        }

        [Fact]
        public void IsConflicted_ReturnsTrueWhenWorkTreeIsConflicted()
        {
            var change = new Change { WorkTree = ChangeState.Conflicted };
            Assert.True(change.IsConflicted);
        }

        [Fact]
        public void IsConflicted_ReturnsFalseForNonConflictedState()
        {
            var change = new Change { WorkTree = ChangeState.Modified };
            Assert.False(change.IsConflicted);
        }

        [Theory]
        [InlineData(ConflictReason.None, "")]
        [InlineData(ConflictReason.BothDeleted, "DD")]
        [InlineData(ConflictReason.AddedByUs, "AU")]
        [InlineData(ConflictReason.DeletedByThem, "UD")]
        [InlineData(ConflictReason.AddedByThem, "UA")]
        [InlineData(ConflictReason.DeletedByUs, "DU")]
        [InlineData(ConflictReason.BothAdded, "AA")]
        [InlineData(ConflictReason.BothModified, "UU")]
        public void ConflictMarker_ReturnsExpectedMarker(ConflictReason reason, string expected)
        {
            var change = new Change { ConflictReason = reason };
            Assert.Equal(expected, change.ConflictMarker);
        }

        [Theory]
        [InlineData(ConflictReason.None, "")]
        [InlineData(ConflictReason.BothDeleted, "Both deleted")]
        [InlineData(ConflictReason.AddedByUs, "Added by us")]
        [InlineData(ConflictReason.DeletedByThem, "Deleted by them")]
        [InlineData(ConflictReason.AddedByThem, "Added by them")]
        [InlineData(ConflictReason.DeletedByUs, "Deleted by us")]
        [InlineData(ConflictReason.BothAdded, "Both added")]
        [InlineData(ConflictReason.BothModified, "Both modified")]
        public void ConflictDesc_ReturnsExpectedDescription(ConflictReason reason, string expected)
        {
            var change = new Change { ConflictReason = reason };
            Assert.Equal(expected, change.ConflictDesc);
        }

        // -----------------------------------------------------------
        // Default property values
        // -----------------------------------------------------------

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var change = new Change();

            Assert.Equal(ChangeState.None, change.Index);
            Assert.Equal(ChangeState.None, change.WorkTree);
            Assert.Equal("", change.Path);
            Assert.Equal("", change.OriginalPath);
            Assert.Null(change.DataForAmend);
            Assert.Equal(ConflictReason.None, change.ConflictReason);
        }
    }
}
