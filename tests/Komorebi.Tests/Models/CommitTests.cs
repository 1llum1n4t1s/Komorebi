using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class CommitTests
    {
        #region ParseParents

        #endregion

        #region ParseDecorators - Early Return

        [Fact]
        public void ParseDecorators_ExactlyThreeChars_ProceedsWithParsing()
        {
            // "abc" is 3 chars - passes the length check but won't match any known prefix
            var commit = new Commit();
            commit.ParseDecorators("abc");
            Assert.Empty(commit.Decorators);
        }

        #endregion

        #region ParseDecorators - Tag

        [Fact]
        public void ParseDecorators_Tag_ParsesCorrectly()
        {
            var commit = new Commit();
            commit.ParseDecorators("tag: refs/tags/v1.0");
            Assert.Single(commit.Decorators);
            Assert.Equal(DecoratorType.Tag, commit.Decorators[0].Type);
            Assert.Equal("v1.0", commit.Decorators[0].Name);
        }

        #endregion

        #region ParseDecorators - CurrentBranchHead (HEAD ->)

        [Fact]
        public void ParseDecorators_HeadArrowBranch_ParsesAsCurrentBranchHead()
        {
            var commit = new Commit();
            commit.ParseDecorators("HEAD -> refs/heads/main");
            Assert.Single(commit.Decorators);
            Assert.Equal(DecoratorType.CurrentBranchHead, commit.Decorators[0].Type);
            Assert.Equal("main", commit.Decorators[0].Name);
        }

        #endregion

        #region ParseDecorators - CurrentCommitHead (bare HEAD)

        [Fact]
        public void ParseDecorators_BareHead_ParsesAsCurrentCommitHead()
        {
            var commit = new Commit();
            commit.ParseDecorators("HEAD");
            Assert.Single(commit.Decorators);
            Assert.Equal(DecoratorType.CurrentCommitHead, commit.Decorators[0].Type);
            Assert.Equal("HEAD", commit.Decorators[0].Name);
        }

        #endregion

        #region ParseDecorators - LocalBranchHead

        [Fact]
        public void ParseDecorators_LocalBranch_ParsesCorrectly()
        {
            var commit = new Commit();
            commit.ParseDecorators("refs/heads/feature");
            Assert.Single(commit.Decorators);
            Assert.Equal(DecoratorType.LocalBranchHead, commit.Decorators[0].Type);
            Assert.Equal("feature", commit.Decorators[0].Name);
        }

        #endregion

        #region ParseDecorators - RemoteBranchHead

        [Fact]
        public void ParseDecorators_RemoteBranch_ParsesCorrectly()
        {
            var commit = new Commit();
            commit.ParseDecorators("refs/remotes/origin/main");
            Assert.Single(commit.Decorators);
            Assert.Equal(DecoratorType.RemoteBranchHead, commit.Decorators[0].Type);
            Assert.Equal("origin/main", commit.Decorators[0].Name);
        }

        #endregion

        #region ParseDecorators - /HEAD skip

        [Fact]
        public void ParseDecorators_RemoteHead_IsSkipped()
        {
            var commit = new Commit();
            commit.ParseDecorators("refs/remotes/origin/HEAD");
            Assert.Empty(commit.Decorators);
        }

        [Fact]
        public void ParseDecorators_SlashHeadInMiddle_NotSkipped()
        {
            // "refs/heads/HEAD-fix" does NOT end with "/HEAD", so should not be skipped
            var commit = new Commit();
            commit.ParseDecorators("refs/heads/HEAD-fix");
            Assert.Single(commit.Decorators);
            Assert.Equal(DecoratorType.LocalBranchHead, commit.Decorators[0].Type);
            Assert.Equal("HEAD-fix", commit.Decorators[0].Name);
        }

        #endregion

        #region ParseDecorators - Multiple decorators and sort order

        [Fact]
        public void ParseDecorators_MultipleDecorators_ParsesAll()
        {
            var commit = new Commit();
            commit.ParseDecorators("HEAD -> refs/heads/main, refs/remotes/origin/main, tag: refs/tags/v1.0");
            Assert.Equal(3, commit.Decorators.Count);
        }

        [Fact]
        public void ParseDecorators_MultipleDecorators_SortedByType()
        {
            // DecoratorType order: CurrentBranchHead(1), LocalBranchHead(2),
            //   CurrentCommitHead(3), RemoteBranchHead(4), Tag(5)
            var commit = new Commit();
            commit.ParseDecorators("tag: refs/tags/v1.0, refs/heads/develop, HEAD -> refs/heads/main, refs/remotes/origin/main");

            Assert.Equal(4, commit.Decorators.Count);
            // CurrentBranchHead first (type 1)
            Assert.Equal(DecoratorType.CurrentBranchHead, commit.Decorators[0].Type);
            Assert.Equal("main", commit.Decorators[0].Name);
            // LocalBranchHead second (type 2)
            Assert.Equal(DecoratorType.LocalBranchHead, commit.Decorators[1].Type);
            Assert.Equal("develop", commit.Decorators[1].Name);
            // RemoteBranchHead third (type 4)
            Assert.Equal(DecoratorType.RemoteBranchHead, commit.Decorators[2].Type);
            Assert.Equal("origin/main", commit.Decorators[2].Name);
            // Tag last (type 5)
            Assert.Equal(DecoratorType.Tag, commit.Decorators[3].Type);
            Assert.Equal("v1.0", commit.Decorators[3].Name);
        }

        [Fact]
        public void ParseDecorators_SameType_SortedByNameNumerically()
        {
            var commit = new Commit();
            commit.ParseDecorators("tag: refs/tags/v10, tag: refs/tags/v2, tag: refs/tags/v1");

            Assert.Equal(3, commit.Decorators.Count);
            Assert.All(commit.Decorators, d => Assert.Equal(DecoratorType.Tag, d.Type));
            // NumericSort: v1 < v2 < v10
            Assert.Equal("v1", commit.Decorators[0].Name);
            Assert.Equal("v2", commit.Decorators[1].Name);
            Assert.Equal("v10", commit.Decorators[2].Name);
        }

        [Fact]
        public void ParseDecorators_IsMerged_OnlyTrueForHeadTypes()
        {
            // HEAD -> sets IsMerged = true
            var commit1 = new Commit();
            commit1.ParseDecorators("HEAD -> refs/heads/main");
            Assert.True(commit1.IsMerged);

            // bare HEAD sets IsMerged = true
            var commit2 = new Commit();
            commit2.ParseDecorators("HEAD");
            Assert.True(commit2.IsMerged);

            // Local branch does NOT set IsMerged
            var commit3 = new Commit();
            commit3.ParseDecorators("refs/heads/feature");
            Assert.False(commit3.IsMerged);

            // Remote branch does NOT set IsMerged
            var commit4 = new Commit();
            commit4.ParseDecorators("refs/remotes/origin/main");
            Assert.False(commit4.IsMerged);

            // Tag does NOT set IsMerged
            var commit5 = new Commit();
            commit5.ParseDecorators("tag: refs/tags/v1.0");
            Assert.False(commit5.IsMerged);
        }

        [Fact]
        public void ParseDecorators_MixedWithHead_IsMergedIsTrue()
        {
            var commit = new Commit();
            commit.ParseDecorators("refs/heads/feature, HEAD -> refs/heads/main, tag: refs/tags/v1.0");
            Assert.True(commit.IsMerged);
        }

        #endregion

        #region ParseDecorators - IsTag helper

        #endregion

        #region GetFriendlyName

        #endregion

        #region Computed Properties

        #endregion
    }
}
