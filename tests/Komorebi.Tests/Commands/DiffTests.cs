using Komorebi.Commands;
using Komorebi.Models;

namespace Komorebi.Tests.Commands
{
    public class DiffTests
    {
        [Fact]
        public void ParseDiffOutput_EmptyString_ReturnsNullTextDiff()
        {
            var result = Diff.ParseDiffOutput(string.Empty);

            Assert.Null(result.TextDiff);
            Assert.False(result.IsBinary);
            Assert.False(result.IsLFS);
        }

        [Fact]
        public void ParseDiffOutput_BinaryFile_DetectedCorrectly()
        {
            var output = "diff --git a/image.png b/image.png\n" +
                         "index abcdef1..1234567 100644\n" +
                         "Binary files a/image.png and b/image.png differ\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.True(result.IsBinary);
            Assert.Null(result.TextDiff);
        }

        [Fact]
        public void ParseDiffOutput_SimpleAddition_ParsedCorrectly()
        {
            var output = "diff --git a/file.txt b/file.txt\n" +
                         "index abcdef1..1234567 100644\n" +
                         "--- a/file.txt\n" +
                         "+++ b/file.txt\n" +
                         "@@ -1,3 +1,4 @@\n" +
                         " line one\n" +
                         " line two\n" +
                         "+line three added\n" +
                         " line four\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.NotNull(result.TextDiff);
            Assert.False(result.IsBinary);
            Assert.Equal("abcdef1", result.OldHash);
            Assert.Equal("1234567", result.NewHash);

            var lines = result.TextDiff.Lines;
            // Indicator + normal + normal + added + normal = 5 lines
            Assert.Equal(5, lines.Count);

            // First line is indicator
            Assert.Equal(TextDiffLineType.Indicator, lines[0].Type);

            // Normal lines
            Assert.Equal(TextDiffLineType.Normal, lines[1].Type);
            Assert.Equal("line one", lines[1].Content);
            Assert.Equal(1, lines[1].OldLineNumber);
            Assert.Equal(1, lines[1].NewLineNumber);

            Assert.Equal(TextDiffLineType.Normal, lines[2].Type);
            Assert.Equal("line two", lines[2].Content);

            // Added line
            Assert.Equal(TextDiffLineType.Added, lines[3].Type);
            Assert.Equal("line three added", lines[3].Content);
            Assert.Equal(0, lines[3].OldLineNumber);
            Assert.Equal(3, lines[3].NewLineNumber);

            // Trailing normal
            Assert.Equal(TextDiffLineType.Normal, lines[4].Type);
            Assert.Equal("line four", lines[4].Content);
        }

        [Fact]
        public void ParseDiffOutput_SimpleDeletion_ParsedCorrectly()
        {
            var output = "diff --git a/file.txt b/file.txt\n" +
                         "index abcdef1..1234567 100644\n" +
                         "--- a/file.txt\n" +
                         "+++ b/file.txt\n" +
                         "@@ -1,3 +1,2 @@\n" +
                         " line one\n" +
                         "-line two deleted\n" +
                         " line three\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.NotNull(result.TextDiff);
            var lines = result.TextDiff.Lines;
            Assert.Equal(4, lines.Count);

            // Deleted line
            var deletedLine = lines.First(l => l.Type == TextDiffLineType.Deleted);
            Assert.Equal("line two deleted", deletedLine.Content);
            Assert.Equal(2, deletedLine.OldLineNumber);
            Assert.Equal(0, deletedLine.NewLineNumber);
        }

        [Fact]
        public void ParseDiffOutput_Modification_ParsedCorrectly()
        {
            var output = "diff --git a/file.txt b/file.txt\n" +
                         "index abcdef1..1234567 100644\n" +
                         "--- a/file.txt\n" +
                         "+++ b/file.txt\n" +
                         "@@ -1,3 +1,3 @@\n" +
                         " line one\n" +
                         "-old line two\n" +
                         "+new line two\n" +
                         " line three\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.NotNull(result.TextDiff);
            var lines = result.TextDiff.Lines;

            var deletedLines = lines.Where(l => l.Type == TextDiffLineType.Deleted).ToList();
            var addedLines = lines.Where(l => l.Type == TextDiffLineType.Added).ToList();

            Assert.Single(deletedLines);
            Assert.Single(addedLines);
            Assert.Equal("old line two", deletedLines[0].Content);
            Assert.Equal("new line two", addedLines[0].Content);
        }

        [Fact]
        public void ParseDiffOutput_OldMode_Parsed()
        {
            var output = "diff --git a/file.txt b/file.txt\n" +
                         "old mode 100644\n" +
                         "new mode 100755\n" +
                         "index abcdef1..1234567\n" +
                         "--- a/file.txt\n" +
                         "+++ b/file.txt\n" +
                         "@@ -1,1 +1,1 @@\n" +
                         " unchanged\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.Equal("100644", result.OldMode);
            Assert.Equal("100755", result.NewMode);
        }

        [Fact]
        public void ParseDiffOutput_DeletedFileMode_Parsed()
        {
            var output = "diff --git a/file.txt b/file.txt\n" +
                         "deleted file mode 100644\n" +
                         "index abcdef1..0000000\n" +
                         "@@ -1,2 +0,0 @@\n" +
                         "-line one\n" +
                         "-line two\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.Equal("100644", result.OldMode);
        }

        [Fact]
        public void ParseDiffOutput_NewFileMode_Parsed()
        {
            var output = "diff --git a/file.txt b/file.txt\n" +
                         "new file mode 100644\n" +
                         "index 0000000..abcdef1\n" +
                         "--- /dev/null\n" +
                         "+++ b/file.txt\n" +
                         "@@ -0,0 +1,2 @@\n" +
                         "+line one\n" +
                         "+line two\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.Equal("100644", result.NewMode);
        }

        [Fact]
        public void ParseDiffOutput_NoNewlineAtEndOfFile_FlagSet()
        {
            var output = "diff --git a/file.txt b/file.txt\n" +
                         "index abcdef1..1234567 100644\n" +
                         "--- a/file.txt\n" +
                         "+++ b/file.txt\n" +
                         "@@ -1,1 +1,1 @@\n" +
                         "-old content\n" +
                         "+new content\n" +
                         "\\ No newline at end of file\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.NotNull(result.TextDiff);
            // The last added line should have NoNewLineEndOfFile = true
            var lastLine = result.TextDiff.Lines.Last();
            Assert.True(lastLine.NoNewLineEndOfFile);
        }

        [Fact]
        public void ParseDiffOutput_MultipleHunks_ParsedCorrectly()
        {
            var output = "diff --git a/file.txt b/file.txt\n" +
                         "index abcdef1..1234567 100644\n" +
                         "--- a/file.txt\n" +
                         "+++ b/file.txt\n" +
                         "@@ -1,2 +1,2 @@\n" +
                         "-old first\n" +
                         "+new first\n" +
                         " unchanged\n" +
                         "@@ -10,2 +10,2 @@\n" +
                         "-old tenth\n" +
                         "+new tenth\n" +
                         " also unchanged\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.NotNull(result.TextDiff);
            var indicators = result.TextDiff.Lines.Where(l => l.Type == TextDiffLineType.Indicator).ToList();
            Assert.Equal(2, indicators.Count);
        }

        [Fact]
        public void ParseDiffOutput_LFSNewFile_Detected()
        {
            var output = "diff --git a/large.bin b/large.bin\n" +
                         "index 0000000..abcdef1\n" +
                         "--- /dev/null\n" +
                         "+++ b/large.bin\n" +
                         "@@ -0,0 +1,3 @@\n" +
                         "+version https://git-lfs.github.com/spec/v1\n" +
                         "+oid sha256:abc123def456\n" +
                         "+size 1024\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.True(result.IsLFS);
            Assert.Null(result.TextDiff);
            Assert.NotNull(result.LFSDiff);
            Assert.Equal("abc123def456", result.LFSDiff.New.Oid);
            Assert.Equal(1024L, result.LFSDiff.New.Size);
        }

        [Fact]
        public void ParseDiffOutput_LFSDeletedFile_Detected()
        {
            var output = "diff --git a/large.bin b/large.bin\n" +
                         "index abcdef1..0000000\n" +
                         "--- a/large.bin\n" +
                         "+++ /dev/null\n" +
                         "@@ -1,3 +0,0 @@\n" +
                         "-version https://git-lfs.github.com/spec/v1\n" +
                         "-oid sha256:abc123def456\n" +
                         "-size 2048\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.True(result.IsLFS);
            Assert.Null(result.TextDiff);
            Assert.NotNull(result.LFSDiff);
            Assert.Equal("abc123def456", result.LFSDiff.Old.Oid);
            Assert.Equal(2048L, result.LFSDiff.Old.Size);
        }

        [Fact]
        public void ParseDiffOutput_LFSModifiedFile_Detected()
        {
            var output = "diff --git a/large.bin b/large.bin\n" +
                         "index abcdef1..1234567\n" +
                         "--- a/large.bin\n" +
                         "+++ b/large.bin\n" +
                         "@@ -1,3 +1,3 @@\n" +
                         " version https://git-lfs.github.com/spec/v1\n" +
                         "-oid sha256:oldoid123\n" +
                         "-size 1024\n" +
                         "+oid sha256:newoid456\n" +
                         "+size 2048\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.True(result.IsLFS);
            Assert.Null(result.TextDiff);
            Assert.NotNull(result.LFSDiff);
            Assert.Equal("oldoid123", result.LFSDiff.Old.Oid);
            Assert.Equal(1024L, result.LFSDiff.Old.Size);
            Assert.Equal("newoid456", result.LFSDiff.New.Oid);
            Assert.Equal(2048L, result.LFSDiff.New.Size);
        }

        [Fact]
        public void ParseDiffOutput_LFSModifiedWithSharedSize_ParsedCorrectly()
        {
            var output = "diff --git a/large.bin b/large.bin\n" +
                         "index abcdef1..1234567\n" +
                         "--- a/large.bin\n" +
                         "+++ b/large.bin\n" +
                         "@@ -1,3 +1,3 @@\n" +
                         " version https://git-lfs.github.com/spec/v1\n" +
                         "-oid sha256:oldoid\n" +
                         "+oid sha256:newoid\n" +
                         " size 4096\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.True(result.IsLFS);
            Assert.NotNull(result.LFSDiff);
            Assert.Equal(4096L, result.LFSDiff.Old.Size);
            Assert.Equal(4096L, result.LFSDiff.New.Size);
        }

        [Fact]
        public void ParseDiffOutput_HashExtraction_Correct()
        {
            var output = "diff --git a/file.txt b/file.txt\n" +
                         "index a1b2c3d..e4f5a6b 100644\n" +
                         "--- a/file.txt\n" +
                         "+++ b/file.txt\n" +
                         "@@ -1,1 +1,1 @@\n" +
                         " content\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.Equal("a1b2c3d", result.OldHash);
            Assert.Equal("e4f5a6b", result.NewHash);
        }

        [Fact]
        public void ParseDiffOutput_EmptyLineInDiff_TreatedAsNormal()
        {
            var output = "diff --git a/file.txt b/file.txt\n" +
                         "index abcdef1..1234567 100644\n" +
                         "--- a/file.txt\n" +
                         "+++ b/file.txt\n" +
                         "@@ -1,3 +1,3 @@\n" +
                         " line one\n" +
                         "\n" +
                         " line three\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.NotNull(result.TextDiff);
            // Indicator + normal + empty normal + normal = 4
            Assert.Equal(4, result.TextDiff.Lines.Count);

            var emptyLine = result.TextDiff.Lines[2];
            Assert.Equal(TextDiffLineType.Normal, emptyLine.Type);
            Assert.Equal("", emptyLine.Content);
        }

        [Fact]
        public void ParseDiffOutput_MaxLineNumber_CalculatedCorrectly()
        {
            var output = "diff --git a/file.txt b/file.txt\n" +
                         "index abcdef1..1234567 100644\n" +
                         "--- a/file.txt\n" +
                         "+++ b/file.txt\n" +
                         "@@ -10,3 +20,4 @@\n" +
                         " unchanged\n" +
                         "-deleted\n" +
                         "+added one\n" +
                         "+added two\n" +
                         " trailing\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.NotNull(result.TextDiff);
            // oldLine ends at 10+3=13, newLine ends at 20+4=24
            Assert.True(result.TextDiff.MaxLineNumber > 0);
        }

        [Fact]
        public void ParseDiffOutput_InlineHighlights_Generated()
        {
            var output = "diff --git a/file.txt b/file.txt\n" +
                         "index abcdef1..1234567 100644\n" +
                         "--- a/file.txt\n" +
                         "+++ b/file.txt\n" +
                         "@@ -1,1 +1,1 @@\n" +
                         "-hello world\n" +
                         "+hello earth\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.NotNull(result.TextDiff);
            var deleted = result.TextDiff.Lines.Where(l => l.Type == TextDiffLineType.Deleted).ToList();
            var added = result.TextDiff.Lines.Where(l => l.Type == TextDiffLineType.Added).ToList();

            Assert.Single(deleted);
            Assert.Single(added);

            // Inline highlights should exist for the differing portion
            Assert.NotEmpty(deleted[0].Highlights);
            Assert.NotEmpty(added[0].Highlights);
        }

        [Fact]
        public void ParseDiffOutput_OnlyNonMatchingLines_ReturnsNullTextDiff()
        {
            // Output that does not match index or indicator patterns
            var output = "some random text\n" +
                         "more random text\n";

            var result = Diff.ParseDiffOutput(output);

            Assert.Null(result.TextDiff);
        }
    }
}
