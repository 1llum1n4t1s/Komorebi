using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class CommitTests
    {
        #region ParseParents

        [Fact]
        public void ParseParents_EmptyString_ReturnsNoParents()
        {
            var commit = new Commit();
            commit.ParseParents(string.Empty);
            Assert.Empty(commit.Parents);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("abcdefg")]
        public void ParseParents_ShortString_LessThan8Chars_ReturnsNoParents(string data)
        {
            var commit = new Commit();
            commit.ParseParents(data);
            Assert.Empty(commit.Parents);
        }

        [Fact]
        public void ParseParents_ExactlyEightChars_ParsesSuccessfully()
        {
            var commit = new Commit();
            commit.ParseParents("abcdefgh");
            Assert.Single(commit.Parents);
            Assert.Equal("abcdefgh", commit.Parents[0]);
        }

        [Fact]
        public void ParseParents_SingleFullSHA_ParsesCorrectly()
        {
            var sha = "abc123def456abc123def456abc123def456abcd";
            var commit = new Commit();
            commit.ParseParents(sha);
            Assert.Single(commit.Parents);
            Assert.Equal(sha, commit.Parents[0]);
        }

        [Fact]
        public void ParseParents_MultipleSHAs_ParsesAll()
        {
            var sha1 = "abc123def456abc123def456abc123def456abcd";
            var sha2 = "1234567890abcdef1234567890abcdef12345678";
            var data = $"{sha1} {sha2}";
            var commit = new Commit();
            commit.ParseParents(data);
            Assert.Equal(2, commit.Parents.Count);
            Assert.Equal(sha1, commit.Parents[0]);
            Assert.Equal(sha2, commit.Parents[1]);
        }

        [Fact]
        public void ParseParents_ThreeSHAs_ParsesAll()
        {
            var sha1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var sha2 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var sha3 = "cccccccccccccccccccccccccccccccccccccccc";
            var data = $"{sha1} {sha2} {sha3}";
            var commit = new Commit();
            commit.ParseParents(data);
            Assert.Equal(3, commit.Parents.Count);
            Assert.Equal(sha1, commit.Parents[0]);
            Assert.Equal(sha2, commit.Parents[1]);
            Assert.Equal(sha3, commit.Parents[2]);
        }

        [Fact]
        public void ParseParents_ExtraSpaces_IgnoresEmptyEntries()
        {
            var sha1 = "abc123def456abc123def456abc123def456abcd";
            var sha2 = "1234567890abcdef1234567890abcdef12345678";
            var data = $"  {sha1}   {sha2}  ";
            var commit = new Commit();
            commit.ParseParents(data);
            Assert.Equal(2, commit.Parents.Count);
            Assert.Equal(sha1, commit.Parents[0]);
            Assert.Equal(sha2, commit.Parents[1]);
        }

        [Fact]
        public void ParseParents_CalledMultipleTimes_AccumulatesParents()
        {
            var sha1 = "abc123def456abc123def456abc123def456abcd";
            var sha2 = "1234567890abcdef1234567890abcdef12345678";
            var commit = new Commit();
            commit.ParseParents(sha1);
            commit.ParseParents(sha2);
            Assert.Equal(2, commit.Parents.Count);
            Assert.Equal(sha1, commit.Parents[0]);
            Assert.Equal(sha2, commit.Parents[1]);
        }

        #endregion

        #region ParseDecorators - Early Return

        [Fact]
        public void ParseDecorators_EmptyString_ReturnsNoDecorators()
        {
            var commit = new Commit();
            commit.ParseDecorators(string.Empty);
            Assert.Empty(commit.Decorators);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("ab")]
        public void ParseDecorators_ShortString_LessThan3Chars_ReturnsNoDecorators(string data)
        {
            var commit = new Commit();
            commit.ParseDecorators(data);
            Assert.Empty(commit.Decorators);
        }

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

        [Fact]
        public void ParseDecorators_Tag_WithComplexName_ParsesCorrectly()
        {
            var commit = new Commit();
            commit.ParseDecorators("tag: refs/tags/release/v2.1.0-beta");
            Assert.Single(commit.Decorators);
            Assert.Equal(DecoratorType.Tag, commit.Decorators[0].Type);
            Assert.Equal("release/v2.1.0-beta", commit.Decorators[0].Name);
        }

        [Fact]
        public void ParseDecorators_Tag_DoesNotSetIsMerged()
        {
            var commit = new Commit();
            commit.ParseDecorators("tag: refs/tags/v1.0");
            Assert.False(commit.IsMerged);
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

        [Fact]
        public void ParseDecorators_HeadArrowBranch_SetsIsMerged()
        {
            var commit = new Commit();
            commit.ParseDecorators("HEAD -> refs/heads/main");
            Assert.True(commit.IsMerged);
        }

        [Fact]
        public void ParseDecorators_HeadArrowBranch_WithSlashes_ParsesCorrectly()
        {
            var commit = new Commit();
            commit.ParseDecorators("HEAD -> refs/heads/feature/my-feature");
            Assert.Single(commit.Decorators);
            Assert.Equal(DecoratorType.CurrentBranchHead, commit.Decorators[0].Type);
            Assert.Equal("feature/my-feature", commit.Decorators[0].Name);
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

        [Fact]
        public void ParseDecorators_BareHead_SetsIsMerged()
        {
            var commit = new Commit();
            commit.ParseDecorators("HEAD");
            Assert.True(commit.IsMerged);
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

        [Fact]
        public void ParseDecorators_LocalBranch_DoesNotSetIsMerged()
        {
            var commit = new Commit();
            commit.ParseDecorators("refs/heads/feature");
            Assert.False(commit.IsMerged);
        }

        [Fact]
        public void ParseDecorators_LocalBranch_WithSlashes_ParsesCorrectly()
        {
            var commit = new Commit();
            commit.ParseDecorators("refs/heads/feature/deep/nested");
            Assert.Single(commit.Decorators);
            Assert.Equal(DecoratorType.LocalBranchHead, commit.Decorators[0].Type);
            Assert.Equal("feature/deep/nested", commit.Decorators[0].Name);
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

        [Fact]
        public void ParseDecorators_RemoteBranch_DoesNotSetIsMerged()
        {
            var commit = new Commit();
            commit.ParseDecorators("refs/remotes/origin/main");
            Assert.False(commit.IsMerged);
        }

        [Fact]
        public void ParseDecorators_RemoteBranch_WithSlashes_ParsesCorrectly()
        {
            var commit = new Commit();
            commit.ParseDecorators("refs/remotes/upstream/feature/branch");
            Assert.Single(commit.Decorators);
            Assert.Equal(DecoratorType.RemoteBranchHead, commit.Decorators[0].Type);
            Assert.Equal("upstream/feature/branch", commit.Decorators[0].Name);
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
        public void ParseDecorators_EndsWithSlashHead_IsSkipped()
        {
            var commit = new Commit();
            commit.ParseDecorators("refs/remotes/upstream/HEAD");
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
        public void ParseDecorators_SkipsRemoteHeadInMixedList()
        {
            var commit = new Commit();
            commit.ParseDecorators("HEAD -> refs/heads/main, refs/remotes/origin/HEAD, refs/remotes/origin/main");

            Assert.Equal(2, commit.Decorators.Count);
            Assert.Equal(DecoratorType.CurrentBranchHead, commit.Decorators[0].Type);
            Assert.Equal("main", commit.Decorators[0].Name);
            Assert.Equal(DecoratorType.RemoteBranchHead, commit.Decorators[1].Type);
            Assert.Equal("origin/main", commit.Decorators[1].Name);
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

        [Fact]
        public void ParseDecorators_UnrecognizedPrefix_Ignored()
        {
            // Something that doesn't match any known prefix and doesn't end with /HEAD
            var commit = new Commit();
            commit.ParseDecorators("some/unknown/ref");
            Assert.Empty(commit.Decorators);
        }

        #endregion

        #region ParseDecorators - IsTag helper

        [Fact]
        public void Decorator_IsTag_TrueForTagType()
        {
            var commit = new Commit();
            commit.ParseDecorators("tag: refs/tags/v1.0");
            Assert.True(commit.Decorators[0].IsTag);
        }

        [Fact]
        public void Decorator_IsTag_FalseForBranchType()
        {
            var commit = new Commit();
            commit.ParseDecorators("refs/heads/main");
            Assert.False(commit.Decorators[0].IsTag);
        }

        #endregion

        #region GetFriendlyName

        [Fact]
        public void GetFriendlyName_WithLocalBranch_ReturnsBranchName()
        {
            var commit = new Commit { SHA = "abc123def456abc123def456abc123def456abcd" };
            commit.ParseDecorators("refs/heads/feature-branch");
            Assert.Equal("feature-branch", commit.GetFriendlyName());
        }

        [Fact]
        public void GetFriendlyName_WithRemoteBranch_ReturnsBranchName()
        {
            var commit = new Commit { SHA = "abc123def456abc123def456abc123def456abcd" };
            commit.ParseDecorators("refs/remotes/origin/main");
            Assert.Equal("origin/main", commit.GetFriendlyName());
        }

        [Fact]
        public void GetFriendlyName_WithCurrentBranchHead_ReturnsBranchName()
        {
            var commit = new Commit { SHA = "abc123def456abc123def456abc123def456abcd" };
            commit.ParseDecorators("HEAD -> refs/heads/main");
            // CurrentBranchHead is not matched by GetFriendlyName (it checks LocalBranchHead or RemoteBranchHead)
            // But after sort, CurrentBranchHead (type 1) comes before others
            // GetFriendlyName looks for LocalBranchHead or RemoteBranchHead, NOT CurrentBranchHead
            // So if only CurrentBranchHead exists, it falls through to tag or SHA
            Assert.Equal("abc123def4", commit.GetFriendlyName());
        }

        [Fact]
        public void GetFriendlyName_WithCurrentBranchHeadAndLocalBranch_ReturnsBranchName()
        {
            var commit = new Commit { SHA = "abc123def456abc123def456abc123def456abcd" };
            commit.ParseDecorators("HEAD -> refs/heads/main, refs/heads/develop");
            // LocalBranchHead "develop" should be found
            Assert.Equal("develop", commit.GetFriendlyName());
        }

        [Fact]
        public void GetFriendlyName_WithTagOnly_ReturnsTagName()
        {
            var commit = new Commit { SHA = "abc123def456abc123def456abc123def456abcd" };
            commit.ParseDecorators("tag: refs/tags/v1.0");
            Assert.Equal("v1.0", commit.GetFriendlyName());
        }

        [Fact]
        public void GetFriendlyName_WithBranchAndTag_ReturnsBranchName()
        {
            var commit = new Commit { SHA = "abc123def456abc123def456abc123def456abcd" };
            commit.ParseDecorators("tag: refs/tags/v1.0, refs/heads/main");
            // Branch takes priority over tag
            Assert.Equal("main", commit.GetFriendlyName());
        }

        [Fact]
        public void GetFriendlyName_NoDecorators_ReturnsSHAFirst10Chars()
        {
            var commit = new Commit { SHA = "abc123def456abc123def456abc123def456abcd" };
            Assert.Equal("abc123def4", commit.GetFriendlyName());
        }

        [Fact]
        public void GetFriendlyName_NoDecorators_Uses10CharPrefix()
        {
            var commit = new Commit { SHA = "1234567890abcdef1234567890abcdef12345678" };
            Assert.Equal("1234567890", commit.GetFriendlyName());
        }

        #endregion

        #region Computed Properties

        [Fact]
        public void IsCurrentHead_WithCurrentBranchHead_ReturnsTrue()
        {
            var commit = new Commit();
            commit.ParseDecorators("HEAD -> refs/heads/main");
            Assert.True(commit.IsCurrentHead);
        }

        [Fact]
        public void IsCurrentHead_WithCurrentCommitHead_ReturnsTrue()
        {
            var commit = new Commit();
            commit.ParseDecorators("HEAD");
            Assert.True(commit.IsCurrentHead);
        }

        [Fact]
        public void IsCurrentHead_WithLocalBranchOnly_ReturnsFalse()
        {
            var commit = new Commit();
            commit.ParseDecorators("refs/heads/feature");
            Assert.False(commit.IsCurrentHead);
        }

        [Fact]
        public void IsCurrentHead_NoDecorators_ReturnsFalse()
        {
            var commit = new Commit();
            Assert.False(commit.IsCurrentHead);
        }

        [Fact]
        public void HasDecorators_WithDecorators_ReturnsTrue()
        {
            var commit = new Commit();
            commit.ParseDecorators("refs/heads/main");
            Assert.True(commit.HasDecorators);
        }

        [Fact]
        public void HasDecorators_NoDecorators_ReturnsFalse()
        {
            var commit = new Commit();
            Assert.False(commit.HasDecorators);
        }

        #endregion

        #region Default Values

        [Fact]
        public void NewCommit_HasEmptyDefaults()
        {
            var commit = new Commit();
            Assert.Equal(string.Empty, commit.SHA);
            Assert.Equal(string.Empty, commit.Subject);
            Assert.Empty(commit.Parents);
            Assert.Empty(commit.Decorators);
            Assert.False(commit.IsMerged);
            Assert.Equal(0, commit.Color);
            Assert.Equal(0.0, commit.LeftMargin);
            Assert.Equal(0UL, commit.AuthorTime);
            Assert.Equal(0UL, commit.CommitterTime);
        }

        [Fact]
        public void EmptyTreeSHA1_HasExpectedValue()
        {
            Assert.Equal("4b825dc642cb6eb9a060e54bf8d69288fbee4904", Commit.EmptyTreeSHA1);
        }

        #endregion
    }
}
