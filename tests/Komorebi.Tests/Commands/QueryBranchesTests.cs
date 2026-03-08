using System.Collections.Generic;
using Komorebi.Commands;
using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Commands
{
    public class QueryBranchesTests
    {
        /// <summary>
        /// Helper to build a null-separated branch line with 7 fields.
        /// Fields: refname, committerdate:unix, objectname, HEAD, upstream, upstream:trackshort, worktreepath
        /// </summary>
        private static string MakeLine(
            string refName,
            string committerDate = "1700000000",
            string objectName = "abc1234567890abcdef1234567890abcdef123456",
            string head = " ",
            string upstream = "",
            string trackShort = "",
            string worktreePath = "")
        {
            return string.Join('\0', refName, committerDate, objectName, head, upstream, trackShort, worktreePath);
        }

        // ---------------------------------------------------------------
        // Normal local branch
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_LocalBranch_Basic()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/heads/main");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal("main", b.Name);
            Assert.Equal("refs/heads/main", b.FullName);
            Assert.True(b.IsLocal);
            Assert.False(b.IsCurrent);
            Assert.False(b.IsDetachedHead);
            Assert.Equal("abc1234567890abcdef1234567890abcdef123456", b.Head);
            Assert.Equal(1700000000UL, b.CommitterDate);
            Assert.Equal(string.Empty, b.Upstream);
            Assert.Equal(string.Empty, b.WorktreePath);
            Assert.False(b.IsUpstreamGone);
        }

        [Fact]
        public void ParseLine_LocalBranch_NestedName()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/heads/feature/my-feature");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal("feature/my-feature", b.Name);
            Assert.True(b.IsLocal);
        }

        // ---------------------------------------------------------------
        // Remote branch
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_RemoteBranch_Basic()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/remotes/origin/main");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal("main", b.Name);
            Assert.Equal("origin", b.Remote);
            Assert.False(b.IsLocal);
        }

        [Fact]
        public void ParseLine_RemoteBranch_NestedName()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/remotes/origin/feature/deep/branch");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal("feature/deep/branch", b.Name);
            Assert.Equal("origin", b.Remote);
            Assert.False(b.IsLocal);
        }

        [Fact]
        public void ParseLine_RemoteBranch_HEAD_IsSkipped()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/remotes/origin/HEAD");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.Null(b);
        }

        [Fact]
        public void ParseLine_RemoteBranch_NoSlashInName_ReturnsNull()
        {
            // A remote ref like "refs/remotes/originonly" with no '/' after the remote name
            // should produce nameParts.Length != 2 and return null.
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/remotes/originonly");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.Null(b);
        }

        // ---------------------------------------------------------------
        // HEAD detection
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_CurrentBranch_IsCurrent()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/heads/main", head: "*");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.True(b.IsCurrent);
        }

        [Fact]
        public void ParseLine_NotCurrentBranch_IsNotCurrent()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/heads/develop", head: " ");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.False(b.IsCurrent);
        }

        // ---------------------------------------------------------------
        // Detached HEAD
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_DetachedAt()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("(HEAD detached at v1.0)");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.True(b.IsDetachedHead);
            Assert.True(b.IsLocal);
            Assert.Equal("(HEAD detached at v1.0)", b.Name);
        }

        [Fact]
        public void ParseLine_DetachedFrom()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("(HEAD detached from abc1234)");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.True(b.IsDetachedHead);
            Assert.True(b.IsLocal);
        }

        // ---------------------------------------------------------------
        // Upstream
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_WithUpstream_InSync()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine(
                "refs/heads/main",
                upstream: "refs/remotes/origin/main",
                trackShort: "=");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal("refs/remotes/origin/main", b.Upstream);
            Assert.False(b.IsUpstreamGone);
            // trackShort "=" means in sync, should NOT be in mismatched
            Assert.DoesNotContain(b.FullName, mismatched);
        }

        [Fact]
        public void ParseLine_WithUpstream_Ahead()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine(
                "refs/heads/main",
                upstream: "refs/remotes/origin/main",
                trackShort: ">");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal("refs/remotes/origin/main", b.Upstream);
            // trackShort ">" means ahead, should be in mismatched
            Assert.Contains(b.FullName, mismatched);
        }

        [Fact]
        public void ParseLine_WithUpstream_Behind()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine(
                "refs/heads/main",
                upstream: "refs/remotes/origin/main",
                trackShort: "<");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Contains(b.FullName, mismatched);
        }

        [Fact]
        public void ParseLine_WithUpstream_Diverged()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine(
                "refs/heads/main",
                upstream: "refs/remotes/origin/main",
                trackShort: "<>");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Contains(b.FullName, mismatched);
        }

        [Fact]
        public void ParseLine_NoUpstream_NotMismatched()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/heads/feature");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal(string.Empty, b.Upstream);
            Assert.Empty(mismatched);
        }

        [Fact]
        public void ParseLine_RemoteBranch_NotAddedToMismatched()
        {
            // Remote branches should never be added to mismatched
            // because the mismatched check requires b.IsLocal.
            var mismatched = new HashSet<string>();
            var line = MakeLine(
                "refs/remotes/origin/main",
                upstream: "refs/remotes/upstream/main",
                trackShort: ">");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.False(b.IsLocal);
            Assert.Empty(mismatched);
        }

        // ---------------------------------------------------------------
        // Worktree path
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_WithWorktreePath()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/heads/feature", worktreePath: "/home/user/worktree");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal("/home/user/worktree", b.WorktreePath);
        }

        [Fact]
        public void ParseLine_EmptyWorktreePath()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/heads/feature", worktreePath: "");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal(string.Empty, b.WorktreePath);
        }

        // ---------------------------------------------------------------
        // CommitterDate parsing
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_CommitterDate_ValidUnix()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/heads/main", committerDate: "1609459200");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal(1609459200UL, b.CommitterDate);
        }

        [Fact]
        public void ParseLine_CommitterDate_Empty_DefaultsToZero()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/heads/main", committerDate: "");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal(0UL, b.CommitterDate);
        }

        [Fact]
        public void ParseLine_CommitterDate_NonNumeric_DefaultsToZero()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/heads/main", committerDate: "notanumber");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal(0UL, b.CommitterDate);
        }

        // ---------------------------------------------------------------
        // Field count validation
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_TooFewFields_ReturnsNull()
        {
            var mismatched = new HashSet<string>();
            // Only 6 fields (missing worktreepath)
            var line = string.Join('\0', "refs/heads/main", "1700000000", "abc123", " ", "", "");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.Null(b);
        }

        [Fact]
        public void ParseLine_TooManyFields_ReturnsNull()
        {
            var mismatched = new HashSet<string>();
            // 8 fields (one extra)
            var line = string.Join('\0', "refs/heads/main", "1700000000", "abc123", " ", "", "", "", "extra");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.Null(b);
        }

        [Fact]
        public void ParseLine_EmptyString_ReturnsNull()
        {
            var mismatched = new HashSet<string>();

            var b = QueryBranches.ParseLine("", mismatched);

            Assert.Null(b);
        }

        [Fact]
        public void ParseLine_NoNullSeparators_ReturnsNull()
        {
            var mismatched = new HashSet<string>();

            var b = QueryBranches.ParseLine("refs/heads/main 1700000000 abc123", mismatched);

            Assert.Null(b);
        }

        // ---------------------------------------------------------------
        // HEAD suffix filter (refs ending with /HEAD are skipped)
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_LocalBranch_EndingWithHEAD_IsSkipped()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine("refs/heads/some/HEAD");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.Null(b);
        }

        // ---------------------------------------------------------------
        // Upstream with empty trackShort (no divergence info)
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_WithUpstream_EmptyTrackShort_NotMismatched()
        {
            var mismatched = new HashSet<string>();
            var line = MakeLine(
                "refs/heads/main",
                upstream: "refs/remotes/origin/main",
                trackShort: "");

            var b = QueryBranches.ParseLine(line, mismatched);

            Assert.NotNull(b);
            Assert.Equal("refs/remotes/origin/main", b.Upstream);
            // Empty trackShort should not add to mismatched
            Assert.Empty(mismatched);
        }
    }
}
