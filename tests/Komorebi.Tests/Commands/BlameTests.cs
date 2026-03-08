using Komorebi.Commands;

namespace Komorebi.Tests.Commands
{
    public class BlameTests
    {
        [Fact]
        public void ParseBlameOutput_EmptyString_ReturnsEmptyBlameData()
        {
            var result = Blame.ParseBlameOutput(string.Empty);

            Assert.False(result.IsBinary);
            Assert.Empty(result.LineInfos);
            Assert.Equal(string.Empty, result.Content);
        }

        [Fact]
        public void ParseBlameOutput_SingleLine_ParsesAllFields()
        {
            // Format: ^?SHA filename (author timestamp timezone linenum) content
            var output = "abc1234def56 src/Main.cs (John Doe 1700000000 +0000 1) Console.WriteLine(\"Hello\");";

            var result = Blame.ParseBlameOutput(output);

            Assert.False(result.IsBinary);
            Assert.Single(result.LineInfos);

            var info = result.LineInfos[0];
            Assert.Equal("abc1234def56", info.CommitSHA);
            Assert.Equal("src/Main.cs", info.File);
            Assert.Equal("John Doe", info.Author);
            Assert.Equal(1700000000UL, info.Timestamp);
            Assert.True(info.IsFirstInGroup);
            Assert.Contains("Console.WriteLine(\"Hello\");", result.Content);
        }

        [Fact]
        public void ParseBlameOutput_MultipleLines_SameCommit_GroupsCorrectly()
        {
            var lines = new[]
            {
                "abc1234def56 src/Main.cs (John Doe 1700000000 +0000 1) line one",
                "abc1234def56 src/Main.cs (John Doe 1700000000 +0000 2) line two",
                "abc1234def56 src/Main.cs (John Doe 1700000000 +0000 3) line three",
            };
            var output = string.Join("\n", lines);

            var result = Blame.ParseBlameOutput(output);

            Assert.Equal(3, result.LineInfos.Count);
            Assert.True(result.LineInfos[0].IsFirstInGroup);
            Assert.False(result.LineInfos[1].IsFirstInGroup);
            Assert.False(result.LineInfos[2].IsFirstInGroup);
        }

        [Fact]
        public void ParseBlameOutput_DifferentCommits_EachIsFirstInGroup()
        {
            var lines = new[]
            {
                "abc1234def56 src/Main.cs (Alice 1700000000 +0000 1) line one",
                "def5678abc12 src/Main.cs (Bob   1700000100 +0000 2) line two",
                "abc1234def56 src/Main.cs (Alice 1700000000 +0000 3) line three",
            };
            var output = string.Join("\n", lines);

            var result = Blame.ParseBlameOutput(output);

            Assert.Equal(3, result.LineInfos.Count);
            Assert.True(result.LineInfos[0].IsFirstInGroup);
            Assert.True(result.LineInfos[1].IsFirstInGroup);
            Assert.True(result.LineInfos[2].IsFirstInGroup);
        }

        [Fact]
        public void ParseBlameOutput_BinaryFile_DetectedAndLineInfosCleared()
        {
            // A line containing a NUL character signals binary
            var output = "abc1234def56 src/Main.cs (John Doe 1700000000 +0000 1) some text\n" +
                         "def5678abc12 src/img.bin (John Doe 1700000100 +0000 2) binary\0data";

            var result = Blame.ParseBlameOutput(output);

            Assert.True(result.IsBinary);
            Assert.Empty(result.LineInfos);
        }

        [Fact]
        public void ParseBlameOutput_BinaryFileOnly_DetectedImmediately()
        {
            var output = "abc1234 file.bin (Author 1700000000 +0000 1) \0binary";

            var result = Blame.ParseBlameOutput(output);

            Assert.True(result.IsBinary);
            Assert.Empty(result.LineInfos);
        }

        [Fact]
        public void ParseBlameOutput_NonMatchingLines_AreSkipped()
        {
            var output = "This is not a blame line\n" +
                         "abc1234def56 src/Main.cs (John Doe 1700000000 +0000 1) real content\n" +
                         "Another non-matching line";

            var result = Blame.ParseBlameOutput(output);

            Assert.Single(result.LineInfos);
            Assert.Equal("abc1234def56", result.LineInfos[0].CommitSHA);
        }

        [Fact]
        public void ParseBlameOutput_BoundaryCommit_UnifiesSHALength()
        {
            // Lines starting with ^ indicate boundary commits
            var lines = new[]
            {
                "^abcdef12 src/Main.cs (Alice 1700000000 +0000 1) boundary line",
                "1234567890abcdef src/Main.cs (Bob   1700000100 +0000 2) normal line",
            };
            var output = string.Join("\n", lines);

            var result = Blame.ParseBlameOutput(output);

            Assert.Equal(2, result.LineInfos.Count);
            // The ^ prefix commit "abcdef12" has length 8
            // The longer commit should be trimmed to 8 chars
            Assert.Equal("abcdef12", result.LineInfos[0].CommitSHA);
            Assert.Equal("12345678", result.LineInfos[1].CommitSHA);
        }

        [Fact]
        public void ParseBlameOutput_ContentBuiltCorrectly()
        {
            var lines = new[]
            {
                "abc1234def56 src/Main.cs (John Doe 1700000000 +0000 1) first line",
                "abc1234def56 src/Main.cs (John Doe 1700000000 +0000 2) second line",
            };
            var output = string.Join("\n", lines);

            var result = Blame.ParseBlameOutput(output);

            // Content should contain both lines with line endings
            Assert.Contains("first line", result.Content);
            Assert.Contains("second line", result.Content);
        }

        [Fact]
        public void ParseBlameOutput_CarriageReturnHandled()
        {
            var output = "abc1234def56 src/Main.cs (John Doe 1700000000 +0000 1) line one\r\n" +
                         "def5678abc12 src/Main.cs (Bob   1700000100 +0000 2) line two";

            var result = Blame.ParseBlameOutput(output);

            Assert.Equal(2, result.LineInfos.Count);
        }

        [Fact]
        public void ParseBlameOutput_WhitespaceInFilename_Trimmed()
        {
            var output = "abc1234def56   src/Main.cs   (John Doe 1700000000 +0000 1) content";

            var result = Blame.ParseBlameOutput(output);

            Assert.Single(result.LineInfos);
            Assert.Equal("src/Main.cs", result.LineInfos[0].File);
        }
    }
}
