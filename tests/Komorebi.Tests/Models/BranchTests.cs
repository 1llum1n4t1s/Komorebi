using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    public class BranchTests
    {
        #region FriendlyName

        [Fact]
        public void FriendlyName_LocalBranch_ReturnsName()
        {
            var branch = new Branch { Name = "feature", IsLocal = true, Remote = "origin" };
            Assert.Equal("feature", branch.FriendlyName);
        }

        [Fact]
        public void FriendlyName_RemoteBranch_ReturnsRemoteSlashName()
        {
            var branch = new Branch { Name = "main", IsLocal = false, Remote = "origin" };
            Assert.Equal("origin/main", branch.FriendlyName);
        }

        [Fact]
        public void FriendlyName_RemoteBranch_DifferentRemote()
        {
            var branch = new Branch { Name = "develop", IsLocal = false, Remote = "upstream" };
            Assert.Equal("upstream/develop", branch.FriendlyName);
        }

        #endregion

        #region HasWorktree

        [Fact]
        public void HasWorktree_NotCurrentAndHasPath_ReturnsTrue()
        {
            var branch = new Branch { IsCurrent = false, WorktreePath = "/path/to/worktree" };
            Assert.True(branch.HasWorktree);
        }

        [Fact]
        public void HasWorktree_IsCurrent_ReturnsFalse()
        {
            var branch = new Branch { IsCurrent = true, WorktreePath = "/path/to/worktree" };
            Assert.False(branch.HasWorktree);
        }

        [Fact]
        public void HasWorktree_NullPath_ReturnsFalse()
        {
            var branch = new Branch { IsCurrent = false, WorktreePath = null };
            Assert.False(branch.HasWorktree);
        }

        [Fact]
        public void HasWorktree_EmptyPath_ReturnsFalse()
        {
            var branch = new Branch { IsCurrent = false, WorktreePath = "" };
            Assert.False(branch.HasWorktree);
        }

        #endregion

        #region TrackStatusDescription

        [Fact]
        public void TrackStatusDescription_BothAheadAndBehind()
        {
            var branch = new Branch
            {
                Ahead = new List<string> { "a", "b" },
                Behind = new List<string> { "c" }
            };
            Assert.Equal("2\u2191 1\u2193", branch.TrackStatusDescription);
        }

        [Fact]
        public void TrackStatusDescription_OnlyAhead()
        {
            var branch = new Branch
            {
                Ahead = new List<string> { "a", "b", "c" },
                Behind = new List<string>()
            };
            Assert.Equal("3\u2191", branch.TrackStatusDescription);
        }

        [Fact]
        public void TrackStatusDescription_OnlyBehind()
        {
            var branch = new Branch
            {
                Ahead = new List<string>(),
                Behind = new List<string> { "a", "b" }
            };
            Assert.Equal("2\u2193", branch.TrackStatusDescription);
        }

        [Fact]
        public void TrackStatusDescription_NeitherAheadNorBehind()
        {
            var branch = new Branch
            {
                Ahead = new List<string>(),
                Behind = new List<string>()
            };
            Assert.Equal(string.Empty, branch.TrackStatusDescription);
        }

        #endregion

        #region IsTrackStatusVisible

        [Fact]
        public void IsTrackStatusVisible_Ahead_ReturnsTrue()
        {
            var branch = new Branch { Ahead = new List<string> { "a" }, Behind = new List<string>() };
            Assert.True(branch.IsTrackStatusVisible);
        }

        [Fact]
        public void IsTrackStatusVisible_Behind_ReturnsTrue()
        {
            var branch = new Branch { Ahead = new List<string>(), Behind = new List<string> { "a" } };
            Assert.True(branch.IsTrackStatusVisible);
        }

        [Fact]
        public void IsTrackStatusVisible_Neither_ReturnsFalse()
        {
            var branch = new Branch { Ahead = new List<string>(), Behind = new List<string>() };
            Assert.False(branch.IsTrackStatusVisible);
        }

        #endregion

        #region Default Values

        [Fact]
        public void NewBranch_HasEmptyDefaults()
        {
            var branch = new Branch();
            Assert.Null(branch.Name);
            Assert.Null(branch.FullName);
            Assert.Null(branch.Head);
            Assert.Null(branch.Upstream);
            Assert.Null(branch.Remote);
            Assert.Null(branch.WorktreePath);
            Assert.False(branch.IsLocal);
            Assert.False(branch.IsCurrent);
            Assert.False(branch.IsDetachedHead);
            Assert.False(branch.IsUpstreamGone);
            Assert.Equal(0UL, branch.CommitterDate);
            Assert.Empty(branch.Ahead);
            Assert.Empty(branch.Behind);
        }

        #endregion
    }
}
