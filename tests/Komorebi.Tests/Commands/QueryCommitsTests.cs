using Komorebi.Commands;
using Komorebi.Models;

namespace Komorebi.Tests.Commands
{
    public class QueryCommitsTests
    {
        // Helper to build a commit log line in the expected format:
        // SHA\0Parents\0Decorators\0Author±Email\0AuthorTime\0Committer±Email\0CommitterTime\0Subject
        private static string BuildCommitLine(
            string sha = "abc123def456abc123def456abc123def456abc123",
            string parents = "",
            string decorators = "",
            string authorName = "Alice",
            string authorEmail = "alice@example.com",
            string authorTime = "1700000000",
            string committerName = "Bob",
            string committerEmail = "bob@example.com",
            string committerTime = "1700000100",
            string subject = "Initial commit")
        {
            return $"{sha}\0{parents}\0{decorators}\0{authorName}±{authorEmail}\0{authorTime}\0{committerName}±{committerEmail}\0{committerTime}\0{subject}";
        }

        [Fact]
        public void ParseCommitLine_TagDecorator_ParsedCorrectly()
        {
            var line = BuildCommitLine(decorators: "tag: refs/tags/v1.0.0");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Single(result.Decorators);
            Assert.Equal(DecoratorType.Tag, result.Decorators[0].Type);
            Assert.Equal("v1.0.0", result.Decorators[0].Name);
        }

        [Fact]
        public void ParseCommitLine_CurrentBranchHead_ParsedAndIsMergedSet()
        {
            var line = BuildCommitLine(decorators: "HEAD -> refs/heads/main");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.True(result.IsMerged);
            Assert.Single(result.Decorators);
            Assert.Equal(DecoratorType.CurrentBranchHead, result.Decorators[0].Type);
            Assert.Equal("main", result.Decorators[0].Name);
        }

        [Fact]
        public void ParseCommitLine_HeadDetached_ParsedAndIsMergedSet()
        {
            var line = BuildCommitLine(decorators: "HEAD");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.True(result.IsMerged);
            Assert.Single(result.Decorators);
            Assert.Equal(DecoratorType.CurrentCommitHead, result.Decorators[0].Type);
        }

        [Fact]
        public void ParseCommitLine_LocalBranch_ParsedCorrectly()
        {
            var line = BuildCommitLine(decorators: "refs/heads/feature-branch");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Single(result.Decorators);
            Assert.Equal(DecoratorType.LocalBranchHead, result.Decorators[0].Type);
            Assert.Equal("feature-branch", result.Decorators[0].Name);
        }

        [Fact]
        public void ParseCommitLine_RemoteBranch_ParsedCorrectly()
        {
            var line = BuildCommitLine(decorators: "refs/remotes/origin/main");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Single(result.Decorators);
            Assert.Equal(DecoratorType.RemoteBranchHead, result.Decorators[0].Type);
            Assert.Equal("origin/main", result.Decorators[0].Name);
        }

        [Fact]
        public void ParseCommitLine_MultipleDecorators_AllParsed()
        {
            var line = BuildCommitLine(decorators: "HEAD -> refs/heads/main, tag: refs/tags/v1.0.0, refs/remotes/origin/main");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Equal(3, result.Decorators.Count);
            Assert.True(result.IsMerged);
        }

        [Fact]
        public void ParseCommitLine_HeadSlashSuffix_Skipped()
        {
            // Entries ending with /HEAD should be skipped
            var line = BuildCommitLine(decorators: "refs/remotes/origin/HEAD, refs/remotes/origin/main");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Single(result.Decorators);
            Assert.Equal(DecoratorType.RemoteBranchHead, result.Decorators[0].Type);
            Assert.Equal("origin/main", result.Decorators[0].Name);
        }

        [Fact]
        public void ParseCommitLine_NoDecorators_EmptyList()
        {
            var line = BuildCommitLine(decorators: "");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Empty(result.Decorators);
            Assert.False(result.IsMerged);
        }

        [Fact]
        public void ParseCommitLine_AllFieldsPopulated_FullParse()
        {
            var sha = "aabbccdd11223344aabbccdd11223344aabbccdd";
            var parent = "11223344aabbccdd11223344aabbccdd11223344";
            var line = BuildCommitLine(
                sha: sha,
                parents: parent,
                decorators: "HEAD -> refs/heads/develop, tag: refs/tags/v2.0.0",
                authorName: "Author Name",
                authorEmail: "author@test.com",
                authorTime: "1700000000",
                committerName: "Committer Name",
                committerEmail: "committer@test.com",
                committerTime: "1700000500",
                subject: "feat: comprehensive test");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Equal(sha, result.SHA);
            Assert.Single(result.Parents);
            Assert.Equal(parent, result.Parents[0]);
            Assert.Equal(2, result.Decorators.Count);
            Assert.True(result.IsMerged);
            Assert.Equal("Author Name", result.Author.Name);
            Assert.Equal("author@test.com", result.Author.Email);
            Assert.Equal(1700000000UL, result.AuthorTime);
            Assert.Equal("Committer Name", result.Committer.Name);
            Assert.Equal("committer@test.com", result.Committer.Email);
            Assert.Equal(1700000500UL, result.CommitterTime);
            Assert.Equal("feat: comprehensive test", result.Subject);
        }
    }
}
