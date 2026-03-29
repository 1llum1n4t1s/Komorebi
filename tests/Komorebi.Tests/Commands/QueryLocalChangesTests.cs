using Komorebi.Commands;
using Komorebi.Models;

namespace Komorebi.Tests.Commands
{
    public class QueryLocalChangesTests
    {
        // ---------------------------------------------------------------
        // Worktree-only modifications (leading space + status letter)
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_WorktreeModified()
        {
            var change = QueryLocalChanges.ParseLine(" M src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.None, change.Index);
            Assert.Equal(ChangeState.Modified, change.WorkTree);
            Assert.Equal("src/file.cs", change.Path);
        }

        [Fact]
        public void ParseLine_WorktreeTypeChanged()
        {
            var change = QueryLocalChanges.ParseLine(" T src/link.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.None, change.Index);
            Assert.Equal(ChangeState.TypeChanged, change.WorkTree);
        }

        [Fact]
        public void ParseLine_WorktreeAdded()
        {
            var change = QueryLocalChanges.ParseLine(" A src/new.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.None, change.Index);
            Assert.Equal(ChangeState.Added, change.WorkTree);
        }

        [Fact]
        public void ParseLine_WorktreeDeleted()
        {
            var change = QueryLocalChanges.ParseLine(" D src/old.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.None, change.Index);
            Assert.Equal(ChangeState.Deleted, change.WorkTree);
        }

        [Fact]
        public void ParseLine_WorktreeRenamed()
        {
            var change = QueryLocalChanges.ParseLine(" R old.cs\tnew.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.None, change.Index);
            Assert.Equal(ChangeState.Renamed, change.WorkTree);
            Assert.Equal("new.cs", change.Path);
            Assert.Equal("old.cs", change.OriginalPath);
        }

        [Fact]
        public void ParseLine_WorktreeCopied()
        {
            var change = QueryLocalChanges.ParseLine(" C original.cs\tcopy.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.None, change.Index);
            Assert.Equal(ChangeState.Copied, change.WorkTree);
        }

        // ---------------------------------------------------------------
        // Index-only modifications (single letter, no leading space)
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_IndexModified()
        {
            var change = QueryLocalChanges.ParseLine("M  src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Modified, change.Index);
            Assert.Equal(ChangeState.None, change.WorkTree);
            Assert.Equal("src/file.cs", change.Path);
        }

        [Fact]
        public void ParseLine_IndexTypeChanged()
        {
            var change = QueryLocalChanges.ParseLine("T  src/link.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.TypeChanged, change.Index);
            Assert.Equal(ChangeState.None, change.WorkTree);
        }

        [Fact]
        public void ParseLine_IndexAdded()
        {
            var change = QueryLocalChanges.ParseLine("A  src/new.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Added, change.Index);
            Assert.Equal(ChangeState.None, change.WorkTree);
        }

        [Fact]
        public void ParseLine_IndexDeleted()
        {
            var change = QueryLocalChanges.ParseLine("D  src/old.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Deleted, change.Index);
            Assert.Equal(ChangeState.None, change.WorkTree);
        }

        [Fact]
        public void ParseLine_IndexRenamed()
        {
            var change = QueryLocalChanges.ParseLine("R  old.cs\tnew.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Renamed, change.Index);
            Assert.Equal(ChangeState.None, change.WorkTree);
            Assert.Equal("new.cs", change.Path);
            Assert.Equal("old.cs", change.OriginalPath);
        }

        [Fact]
        public void ParseLine_IndexCopied()
        {
            var change = QueryLocalChanges.ParseLine("C  src.cs\tdst.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Copied, change.Index);
            Assert.Equal(ChangeState.None, change.WorkTree);
        }

        // ---------------------------------------------------------------
        // Combined index + worktree modifications
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_ModifiedModified()
        {
            var change = QueryLocalChanges.ParseLine("MM src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Modified, change.Index);
            Assert.Equal(ChangeState.Modified, change.WorkTree);
        }

        [Fact]
        public void ParseLine_ModifiedTypeChanged()
        {
            var change = QueryLocalChanges.ParseLine("MT src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Modified, change.Index);
            Assert.Equal(ChangeState.TypeChanged, change.WorkTree);
        }

        [Fact]
        public void ParseLine_ModifiedDeleted()
        {
            var change = QueryLocalChanges.ParseLine("MD src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Modified, change.Index);
            Assert.Equal(ChangeState.Deleted, change.WorkTree);
        }

        [Fact]
        public void ParseLine_TypeChangedModified()
        {
            var change = QueryLocalChanges.ParseLine("TM src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.TypeChanged, change.Index);
            Assert.Equal(ChangeState.Modified, change.WorkTree);
        }

        [Fact]
        public void ParseLine_TypeChangedTypeChanged()
        {
            var change = QueryLocalChanges.ParseLine("TT src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.TypeChanged, change.Index);
            Assert.Equal(ChangeState.TypeChanged, change.WorkTree);
        }

        [Fact]
        public void ParseLine_TypeChangedDeleted()
        {
            var change = QueryLocalChanges.ParseLine("TD src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.TypeChanged, change.Index);
            Assert.Equal(ChangeState.Deleted, change.WorkTree);
        }

        [Fact]
        public void ParseLine_AddedModified()
        {
            var change = QueryLocalChanges.ParseLine("AM src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Added, change.Index);
            Assert.Equal(ChangeState.Modified, change.WorkTree);
        }

        [Fact]
        public void ParseLine_AddedTypeChanged()
        {
            var change = QueryLocalChanges.ParseLine("AT src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Added, change.Index);
            Assert.Equal(ChangeState.TypeChanged, change.WorkTree);
        }

        [Fact]
        public void ParseLine_AddedDeleted()
        {
            var change = QueryLocalChanges.ParseLine("AD src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Added, change.Index);
            Assert.Equal(ChangeState.Deleted, change.WorkTree);
        }

        [Fact]
        public void ParseLine_RenamedModified()
        {
            var change = QueryLocalChanges.ParseLine("RM old.cs\tnew.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Renamed, change.Index);
            Assert.Equal(ChangeState.Modified, change.WorkTree);
        }

        [Fact]
        public void ParseLine_RenamedTypeChanged()
        {
            var change = QueryLocalChanges.ParseLine("RT old.cs\tnew.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Renamed, change.Index);
            Assert.Equal(ChangeState.TypeChanged, change.WorkTree);
        }

        [Fact]
        public void ParseLine_RenamedDeleted()
        {
            var change = QueryLocalChanges.ParseLine("RD old.cs\tnew.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Renamed, change.Index);
            Assert.Equal(ChangeState.Deleted, change.WorkTree);
        }

        [Fact]
        public void ParseLine_CopiedModified()
        {
            var change = QueryLocalChanges.ParseLine("CM src.cs\tdst.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Copied, change.Index);
            Assert.Equal(ChangeState.Modified, change.WorkTree);
        }

        [Fact]
        public void ParseLine_CopiedTypeChanged()
        {
            var change = QueryLocalChanges.ParseLine("CT src.cs\tdst.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Copied, change.Index);
            Assert.Equal(ChangeState.TypeChanged, change.WorkTree);
        }

        [Fact]
        public void ParseLine_CopiedDeleted()
        {
            var change = QueryLocalChanges.ParseLine("CD src.cs\tdst.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Copied, change.Index);
            Assert.Equal(ChangeState.Deleted, change.WorkTree);
        }

        // ---------------------------------------------------------------
        // Conflict statuses
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_Conflict_BothDeleted()
        {
            var change = QueryLocalChanges.ParseLine("DD src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Conflicted, change.WorkTree);
            Assert.Equal(ConflictReason.BothDeleted, change.ConflictReason);
        }

        [Fact]
        public void ParseLine_Conflict_AddedByUs()
        {
            var change = QueryLocalChanges.ParseLine("AU src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Conflicted, change.WorkTree);
            Assert.Equal(ConflictReason.AddedByUs, change.ConflictReason);
        }

        [Fact]
        public void ParseLine_Conflict_DeletedByThem()
        {
            var change = QueryLocalChanges.ParseLine("UD src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Conflicted, change.WorkTree);
            Assert.Equal(ConflictReason.DeletedByThem, change.ConflictReason);
        }

        [Fact]
        public void ParseLine_Conflict_AddedByThem()
        {
            var change = QueryLocalChanges.ParseLine("UA src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Conflicted, change.WorkTree);
            Assert.Equal(ConflictReason.AddedByThem, change.ConflictReason);
        }

        [Fact]
        public void ParseLine_Conflict_DeletedByUs()
        {
            var change = QueryLocalChanges.ParseLine("DU src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Conflicted, change.WorkTree);
            Assert.Equal(ConflictReason.DeletedByUs, change.ConflictReason);
        }

        [Fact]
        public void ParseLine_Conflict_BothAdded()
        {
            var change = QueryLocalChanges.ParseLine("AA src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Conflicted, change.WorkTree);
            Assert.Equal(ConflictReason.BothAdded, change.ConflictReason);
        }

        [Fact]
        public void ParseLine_Conflict_BothModified()
        {
            var change = QueryLocalChanges.ParseLine("UU src/file.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Conflicted, change.WorkTree);
            Assert.Equal(ConflictReason.BothModified, change.ConflictReason);
        }

        // ---------------------------------------------------------------
        // Untracked
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_Untracked()
        {
            var change = QueryLocalChanges.ParseLine("?? newfile.txt");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.None, change.Index);
            Assert.Equal(ChangeState.Untracked, change.WorkTree);
            Assert.Equal("newfile.txt", change.Path);
        }

        // ---------------------------------------------------------------
        // Rename / Copy path parsing
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_Rename_TabSeparated()
        {
            var change = QueryLocalChanges.ParseLine("R  old/path.cs\tnew/path.cs");

            Assert.NotNull(change);
            Assert.Equal("new/path.cs", change.Path);
            Assert.Equal("old/path.cs", change.OriginalPath);
        }

        [Fact]
        public void ParseLine_Rename_ArrowSeparated()
        {
            var change = QueryLocalChanges.ParseLine("R  old/path.cs -> new/path.cs");

            Assert.NotNull(change);
            Assert.Equal("new/path.cs", change.Path);
            Assert.Equal("old/path.cs", change.OriginalPath);
        }

        // ---------------------------------------------------------------
        // Edge cases / invalid input
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_EmptyString_ReturnsNull()
        {
            var change = QueryLocalChanges.ParseLine("");

            Assert.Null(change);
        }

        [Fact]
        public void ParseLine_Whitespace_ReturnsNull()
        {
            var change = QueryLocalChanges.ParseLine("   ");

            Assert.Null(change);
        }

        [Fact]
        public void ParseLine_NoMatch_ReturnsNull()
        {
            var change = QueryLocalChanges.ParseLine("this is not a valid status line");

            Assert.Null(change);
        }

        [Fact]
        public void ParseLine_UnknownStatus_ReturnsNull()
        {
            // "ZZ" is not a recognized git status code
            // The regex will match it, but the switch won't set any state,
            // so change.Index and change.WorkTree both remain None => returns null
            var change = QueryLocalChanges.ParseLine("ZZ src/file.cs");

            Assert.Null(change);
        }

        // ---------------------------------------------------------------
        // Path with spaces
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_PathWithSpaces()
        {
            var change = QueryLocalChanges.ParseLine(" M src/my file with spaces.cs");

            Assert.NotNull(change);
            Assert.Equal("src/my file with spaces.cs", change.Path);
        }

        // ---------------------------------------------------------------
        // Paths with special characters in directories
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_DeeplyNestedPath()
        {
            var change = QueryLocalChanges.ParseLine("A  a/b/c/d/e/f/g.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Added, change.Index);
            Assert.Equal("a/b/c/d/e/f/g.cs", change.Path);
        }

        // ---------------------------------------------------------------
        // All conflict types have correct Index state (should be None)
        // ---------------------------------------------------------------

        [Theory]
        [InlineData("DD", ConflictReason.BothDeleted)]
        [InlineData("AU", ConflictReason.AddedByUs)]
        [InlineData("UD", ConflictReason.DeletedByThem)]
        [InlineData("UA", ConflictReason.AddedByThem)]
        [InlineData("DU", ConflictReason.DeletedByUs)]
        [InlineData("AA", ConflictReason.BothAdded)]
        [InlineData("UU", ConflictReason.BothModified)]
        public void ParseLine_AllConflicts_IndexIsNone(string status, ConflictReason expectedReason)
        {
            var change = QueryLocalChanges.ParseLine($"{status} conflict.cs");

            Assert.NotNull(change);
            Assert.Equal(ChangeState.None, change.Index);
            Assert.Equal(ChangeState.Conflicted, change.WorkTree);
            Assert.Equal(expectedReason, change.ConflictReason);
            Assert.True(change.IsConflicted);
        }
    }
}
