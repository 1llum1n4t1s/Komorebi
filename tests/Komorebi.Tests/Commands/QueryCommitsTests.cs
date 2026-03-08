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
        public void ParseCommitLine_EmptyString_ReturnsNull()
        {
            var result = QueryCommits.ParseCommitLine(string.Empty);
            Assert.Null(result);
        }

        [Fact]
        public void ParseCommitLine_WrongFieldCount_ReturnsNull()
        {
            // Only 3 fields instead of 8
            var result = QueryCommits.ParseCommitLine("abc\0def\0ghi");
            Assert.Null(result);
        }

        [Fact]
        public void ParseCommitLine_TooManyFields_ReturnsNull()
        {
            // 9 fields instead of 8
            var result = QueryCommits.ParseCommitLine("1\02\03\04\05\06\07\08\09");
            Assert.Null(result);
        }

        [Fact]
        public void ParseCommitLine_ValidLine_ParsesSHA()
        {
            var line = BuildCommitLine(sha: "deadbeef1234567890abcdef1234567890abcdef");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Equal("deadbeef1234567890abcdef1234567890abcdef", result.SHA);
        }

        [Fact]
        public void ParseCommitLine_ValidLine_ParsesSubject()
        {
            var line = BuildCommitLine(subject: "feat: add new feature");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Equal("feat: add new feature", result.Subject);
        }

        [Fact]
        public void ParseCommitLine_ValidLine_ParsesAuthor()
        {
            var line = BuildCommitLine(authorName: "John Doe", authorEmail: "john@example.com");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Equal("John Doe", result.Author.Name);
            Assert.Equal("john@example.com", result.Author.Email);
        }

        [Fact]
        public void ParseCommitLine_ValidLine_ParsesCommitter()
        {
            var line = BuildCommitLine(committerName: "Jane Smith", committerEmail: "jane@example.com");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Equal("Jane Smith", result.Committer.Name);
            Assert.Equal("jane@example.com", result.Committer.Email);
        }

        [Fact]
        public void ParseCommitLine_ValidLine_ParsesAuthorTime()
        {
            var line = BuildCommitLine(authorTime: "1609459200");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Equal(1609459200UL, result.AuthorTime);
        }

        [Fact]
        public void ParseCommitLine_ValidLine_ParsesCommitterTime()
        {
            var line = BuildCommitLine(committerTime: "1609459300");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Equal(1609459300UL, result.CommitterTime);
        }

        [Fact]
        public void ParseCommitLine_SingleParent_ParsedCorrectly()
        {
            var parentSha = "1234567890abcdef1234567890abcdef12345678";
            var line = BuildCommitLine(parents: parentSha);

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Single(result.Parents);
            Assert.Equal(parentSha, result.Parents[0]);
        }

        [Fact]
        public void ParseCommitLine_MultipleParents_MergeCommit()
        {
            var parent1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var parent2 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var line = BuildCommitLine(parents: $"{parent1} {parent2}");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Equal(2, result.Parents.Count);
            Assert.Equal(parent1, result.Parents[0]);
            Assert.Equal(parent2, result.Parents[1]);
        }

        [Fact]
        public void ParseCommitLine_NoParents_EmptyList()
        {
            var line = BuildCommitLine(parents: "");

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Empty(result.Parents);
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
        public void ParseCommitLine_SubjectWithSpecialCharacters_PreservedExactly()
        {
            var subject = "fix: handle 'special' chars & \"quotes\" <brackets>";
            var line = BuildCommitLine(subject: subject);

            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Equal(subject, result.Subject);
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
