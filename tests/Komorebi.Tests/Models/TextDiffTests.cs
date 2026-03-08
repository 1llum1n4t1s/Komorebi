using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class TextDiffTests
    {
        #region TextRange

        [Fact]
        public void TextRange_Constructor_SetsStartAndEnd()
        {
            var range = new TextRange(5, 3);
            Assert.Equal(5, range.Start);
            Assert.Equal(7, range.End); // 5 + 3 - 1
        }

        [Fact]
        public void TextRange_ZeroLength_EndIsOneLessThanStart()
        {
            // size=0 means End = Start - 1, which is a degenerate range
            var range = new TextRange(10, 0);
            Assert.Equal(10, range.Start);
            Assert.Equal(9, range.End);
        }

        [Fact]
        public void TextRange_SingleCharacter_StartEqualsEnd()
        {
            var range = new TextRange(3, 1);
            Assert.Equal(3, range.Start);
            Assert.Equal(3, range.End);
        }

        [Fact]
        public void TextRange_LargeRange_CalculatesCorrectly()
        {
            var range = new TextRange(0, 1000);
            Assert.Equal(0, range.Start);
            Assert.Equal(999, range.End);
        }

        [Fact]
        public void TextRange_StartAtZero_Works()
        {
            var range = new TextRange(0, 5);
            Assert.Equal(0, range.Start);
            Assert.Equal(4, range.End);
        }

        #endregion

        #region TextDiffLine - Construction

        [Fact]
        public void TextDiffLine_DefaultConstructor_HasDefaults()
        {
            var line = new TextDiffLine();
            Assert.Equal(TextDiffLineType.None, line.Type);
            Assert.Equal("", line.Content);
            Assert.Equal(0, line.OldLineNumber);
            Assert.Equal(0, line.NewLineNumber);
            Assert.Empty(line.Highlights);
            Assert.False(line.NoNewLineEndOfFile);
        }

        [Fact]
        public void TextDiffLine_ParameterizedConstructor_SetsAll()
        {
            var line = new TextDiffLine(TextDiffLineType.Added, "hello world", 0, 42);
            Assert.Equal(TextDiffLineType.Added, line.Type);
            Assert.Equal("hello world", line.Content);
            Assert.Equal(0, line.OldLineNumber);
            Assert.Equal(42, line.NewLineNumber);
        }

        [Fact]
        public void TextDiffLine_OldLine_ZeroReturnsEmpty()
        {
            var line = new TextDiffLine(TextDiffLineType.Added, "x", 0, 5);
            Assert.Equal(string.Empty, line.OldLine);
        }

        [Fact]
        public void TextDiffLine_OldLine_NonZeroReturnsString()
        {
            var line = new TextDiffLine(TextDiffLineType.Deleted, "x", 10, 0);
            Assert.Equal("10", line.OldLine);
        }

        [Fact]
        public void TextDiffLine_NewLine_ZeroReturnsEmpty()
        {
            var line = new TextDiffLine(TextDiffLineType.Deleted, "x", 5, 0);
            Assert.Equal(string.Empty, line.NewLine);
        }

        [Fact]
        public void TextDiffLine_NewLine_NonZeroReturnsString()
        {
            var line = new TextDiffLine(TextDiffLineType.Added, "x", 0, 7);
            Assert.Equal("7", line.NewLine);
        }

        [Fact]
        public void TextDiffLine_NormalLine_BothLineNumbersPresent()
        {
            var line = new TextDiffLine(TextDiffLineType.Normal, "code", 3, 3);
            Assert.Equal("3", line.OldLine);
            Assert.Equal("3", line.NewLine);
        }

        #endregion

        #region TextDiffSelection

        [Fact]
        public void TextDiffSelection_DefaultValues()
        {
            var sel = new TextDiffSelection();
            Assert.Equal(0, sel.StartLine);
            Assert.Equal(0, sel.EndLine);
            Assert.False(sel.HasChanges);
            Assert.Equal(0, sel.IgnoredAdds);
            Assert.Equal(0, sel.IgnoredDeletes);
        }

        #endregion

        #region TextDiff.MakeSelection

        [Fact]
        public void MakeSelection_AllNormalLines_NoChanges()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -1,3 +1,3 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Normal, "line1", 1, 1));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Normal, "line2", 2, 2));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Normal, "line3", 3, 3));

            var sel = diff.MakeSelection(1, 4, false, false);
            Assert.False(sel.HasChanges);
            Assert.Equal(0, sel.IgnoredAdds);
            Assert.Equal(0, sel.IgnoredDeletes);
        }

        [Fact]
        public void MakeSelection_WithAddedLine_Combined_HasChanges()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -1,1 +1,2 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Normal, "line1", 1, 1));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "new line", 0, 2));

            var sel = diff.MakeSelection(1, 3, true, false);
            Assert.True(sel.HasChanges);
        }

        [Fact]
        public void MakeSelection_WithDeletedLine_Combined_HasChanges()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -1,2 +1,1 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Normal, "line1", 1, 1));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Deleted, "removed", 2, 0));

            var sel = diff.MakeSelection(1, 3, true, false);
            Assert.True(sel.HasChanges);
        }

        [Fact]
        public void MakeSelection_AddedLine_OldSide_NotCombined_NoChanges()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -1,1 +1,2 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "new line", 0, 1));

            // isOldSide=true, isCombined=false: added lines don't count as changes on the old side
            var sel = diff.MakeSelection(1, 2, false, true);
            Assert.False(sel.HasChanges);
        }

        [Fact]
        public void MakeSelection_DeletedLine_NewSide_NotCombined_NoChanges()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -1,2 +1,1 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Deleted, "removed", 1, 0));

            // isOldSide=false, isCombined=false: deleted lines don't count as changes on the new side
            var sel = diff.MakeSelection(1, 2, false, false);
            Assert.False(sel.HasChanges);
        }

        [Fact]
        public void MakeSelection_AddedLine_NewSide_NotCombined_HasChanges()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -1,1 +1,2 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "new line", 0, 1));

            // isOldSide=false, isCombined=false: added lines are changes on the new side
            var sel = diff.MakeSelection(1, 2, false, false);
            Assert.True(sel.HasChanges);
        }

        [Fact]
        public void MakeSelection_DeletedLine_OldSide_NotCombined_HasChanges()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -1,2 +1,1 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Deleted, "removed", 1, 0));

            // isOldSide=true, isCombined=false: deleted lines are changes on the old side
            var sel = diff.MakeSelection(1, 2, false, true);
            Assert.True(sel.HasChanges);
        }

        [Fact]
        public void MakeSelection_CountsIgnoredAddsAndDeletes_BeforeSelection()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -1,5 +1,5 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "add1", 0, 1));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Deleted, "del1", 1, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "add2", 0, 2));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Normal, "normal", 2, 3));

            // Select only line 5 (1-indexed) which is the Normal line
            var sel = diff.MakeSelection(5, 5, true, false);
            Assert.Equal(2, sel.IgnoredAdds);
            Assert.Equal(1, sel.IgnoredDeletes);
        }

        [Fact]
        public void MakeSelection_SelectFromStart_NoIgnored()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -1,2 +1,2 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "new", 0, 1));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Deleted, "old", 1, 0));

            var sel = diff.MakeSelection(1, 3, true, false);
            Assert.Equal(0, sel.IgnoredAdds);
            Assert.Equal(0, sel.IgnoredDeletes);
        }

        #endregion

        #region TextInlineChange.Compare - Identical Strings

        [Fact]
        public void Compare_IdenticalStrings_ReturnsEmpty()
        {
            var result = TextInlineChange.Compare("hello", "hello");
            Assert.Empty(result);
        }

        [Fact]
        public void Compare_BothEmpty_ReturnsEmpty()
        {
            var result = TextInlineChange.Compare("", "");
            Assert.Empty(result);
        }

        #endregion

        #region TextInlineChange.Compare - Completely Different

        [Fact]
        public void Compare_CompletelyDifferent_SingleWordEach_ReturnsSingleChange()
        {
            var result = TextInlineChange.Compare("abc", "xyz");
            Assert.NotEmpty(result);
            // The entire old text is deleted, entire new text is added
            var total = result.Sum(c => c.DeletedCount);
            Assert.Equal(3, total); // "abc" has length 3
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.Equal(3, totalAdded); // "xyz" has length 3
        }

        #endregion

        #region TextInlineChange.Compare - Simple Addition

        [Fact]
        public void Compare_EmptyOld_AllAdded()
        {
            var result = TextInlineChange.Compare("", "hello");
            Assert.NotEmpty(result);
            Assert.True(result.All(c => c.DeletedCount == 0));
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.Equal(5, totalAdded);
        }

        [Fact]
        public void Compare_EmptyNew_AllDeleted()
        {
            var result = TextInlineChange.Compare("hello", "");
            Assert.NotEmpty(result);
            Assert.True(result.All(c => c.AddedCount == 0));
            var totalDeleted = result.Sum(c => c.DeletedCount);
            Assert.Equal(5, totalDeleted);
        }

        #endregion

        #region TextInlineChange.Compare - Word-Level Changes

        [Fact]
        public void Compare_SingleWordChange_InMiddle()
        {
            // "hello world foo" -> "hello earth foo"
            // The algorithm splits on delimiters (space is a delimiter)
            var result = TextInlineChange.Compare("hello world foo", "hello earth foo");
            Assert.NotEmpty(result);
            // "world" deleted, "earth" added
            Assert.Contains(result, c => c.DeletedCount > 0);
            Assert.Contains(result, c => c.AddedCount > 0);
        }

        [Fact]
        public void Compare_AppendWord()
        {
            // "hello" -> "hello world"
            var result = TextInlineChange.Compare("hello", "hello world");
            Assert.NotEmpty(result);
            // The space and "world" should be added
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.True(totalAdded > 0);
        }

        [Fact]
        public void Compare_RemoveWord()
        {
            // "hello world" -> "hello"
            var result = TextInlineChange.Compare("hello world", "hello");
            Assert.NotEmpty(result);
            // The space and "world" should be deleted
            var totalDeleted = result.Sum(c => c.DeletedCount);
            Assert.True(totalDeleted > 0);
        }

        #endregion

        #region TextInlineChange.Compare - Delimiter-Based Chunking

        [Fact]
        public void Compare_ChangePunctuation()
        {
            // Delimiters like '.' are separate chunks
            var result = TextInlineChange.Compare("a.b", "a.c");
            Assert.NotEmpty(result);
            // 'b' changed to 'c', 'a' and '.' unchanged
            var totalDeleted = result.Sum(c => c.DeletedCount);
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.Equal(1, totalDeleted); // 'b' length 1
            Assert.Equal(1, totalAdded);   // 'c' length 1
        }

        [Fact]
        public void Compare_ChangeInsideBrackets()
        {
            // "func(old)" -> "func(new)"
            var result = TextInlineChange.Compare("func(old)", "func(new)");
            Assert.NotEmpty(result);
            // "old" deleted, "new" added; "func", "(", ")" unchanged
            var totalDeleted = result.Sum(c => c.DeletedCount);
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.Equal(3, totalDeleted); // "old"
            Assert.Equal(3, totalAdded);   // "new"
        }

        [Fact]
        public void Compare_OnlyDelimiterChange()
        {
            // "a+b" -> "a-b"
            var result = TextInlineChange.Compare("a+b", "a-b");
            Assert.NotEmpty(result);
            // '+' changed to '-'
            var totalDeleted = result.Sum(c => c.DeletedCount);
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.Equal(1, totalDeleted);
            Assert.Equal(1, totalAdded);
        }

        #endregion

        #region TextInlineChange.Compare - Position Correctness

        [Fact]
        public void Compare_DeletedStart_PositionIsCorrect()
        {
            // "abc def" -> "xyz def"
            // 'abc' starts at position 0
            var result = TextInlineChange.Compare("abc def", "xyz def");
            Assert.NotEmpty(result);
            var firstChange = result[0];
            Assert.Equal(0, firstChange.DeletedStart);
            Assert.Equal(3, firstChange.DeletedCount);
        }

        [Fact]
        public void Compare_AddedStart_PositionIsCorrect()
        {
            // "abc def" -> "xyz def"
            // 'xyz' starts at position 0
            var result = TextInlineChange.Compare("abc def", "xyz def");
            Assert.NotEmpty(result);
            var firstChange = result[0];
            Assert.Equal(0, firstChange.AddedStart);
            Assert.Equal(3, firstChange.AddedCount);
        }

        [Fact]
        public void Compare_ChangeAtEnd_CorrectPositions()
        {
            // "hello world" -> "hello earth"
            // "world" starts at position 6 in old, "earth" starts at position 6 in new
            var result = TextInlineChange.Compare("hello world", "hello earth");
            Assert.NotEmpty(result);
            var change = result[0];
            Assert.Equal(6, change.DeletedStart); // "world" position
            Assert.Equal(5, change.DeletedCount);
            Assert.Equal(6, change.AddedStart);   // "earth" position
            Assert.Equal(5, change.AddedCount);
        }

        #endregion

        #region TextInlineChange.Compare - Merging Adjacent Changes

        [Fact]
        public void Compare_AdjacentSmallChanges_AreMerged()
        {
            // When two changes are separated by a single character in both old and new,
            // they get merged (midSizeOld == 1 && midSizeNew == 1)
            // "a.b" -> "x.y" -- 'a' and 'b' are different, separated by '.' (1 chunk each side)
            var result = TextInlineChange.Compare("a.b", "x.y");
            // All three chunks (a, ., b) vs (x, ., y) -- the gap between 'a'->'x' and 'b'->'y' is '.' (1 chunk)
            // So they should be merged into a single change
            Assert.Single(result);
            Assert.Equal(3, result[0].DeletedCount); // "a.b" all 3 chars
            Assert.Equal(3, result[0].AddedCount);   // "x.y" all 3 chars
        }

        #endregion

        #region TextInlineChange.Compare - Symmetry

        [Fact]
        public void Compare_Symmetry_DeletedAndAddedSwap()
        {
            var forward = TextInlineChange.Compare("old text", "new text");
            var reverse = TextInlineChange.Compare("new text", "old text");

            // Forward: deleted="old", added="new"
            // Reverse: deleted="new", added="old"
            var fwdDeleted = forward.Sum(c => c.DeletedCount);
            var fwdAdded = forward.Sum(c => c.AddedCount);
            var revDeleted = reverse.Sum(c => c.DeletedCount);
            var revAdded = reverse.Sum(c => c.AddedCount);

            Assert.Equal(fwdDeleted, revAdded);
            Assert.Equal(fwdAdded, revDeleted);
        }

        #endregion

        #region TextInlineChange.Compare - Code-Like Strings

        [Fact]
        public void Compare_CodeChange_VariableRename()
        {
            var result = TextInlineChange.Compare("var oldName = getValue();", "var newName = getValue();");
            Assert.NotEmpty(result);
            // "oldName" -> "newName", rest identical
            var totalDeleted = result.Sum(c => c.DeletedCount);
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.Equal(7, totalDeleted); // "oldName"
            Assert.Equal(7, totalAdded);   // "newName"
        }

        [Fact]
        public void Compare_CodeChange_OperatorChange()
        {
            var result = TextInlineChange.Compare("x = a + b;", "x = a - b;");
            Assert.NotEmpty(result);
            // '+' -> '-'
            var totalDeleted = result.Sum(c => c.DeletedCount);
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.Equal(1, totalDeleted);
            Assert.Equal(1, totalAdded);
        }

        [Fact]
        public void Compare_CodeChange_StringLiteral()
        {
            var result = TextInlineChange.Compare("print(\"hello\")", "print(\"world\")");
            Assert.NotEmpty(result);
            // "hello" -> "world"
            var totalDeleted = result.Sum(c => c.DeletedCount);
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.Equal(5, totalDeleted);
            Assert.Equal(5, totalAdded);
        }

        #endregion

        #region TextInlineChange.Compare - Whitespace

        [Fact]
        public void Compare_LeadingSpaceAdded()
        {
            var result = TextInlineChange.Compare("hello", " hello");
            Assert.NotEmpty(result);
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.True(totalAdded > 0);
        }

        [Fact]
        public void Compare_TabVsSpaces()
        {
            var result = TextInlineChange.Compare("\thello", "    hello");
            Assert.NotEmpty(result);
            // Tab is a delimiter, spaces are delimiters -- they differ
        }

        #endregion

        #region TextInlineChange - Property Access

        [Fact]
        public void TextInlineChange_Constructor_SetsProperties()
        {
            var change = new TextInlineChange(10, 5, 20, 3);
            Assert.Equal(10, change.DeletedStart);
            Assert.Equal(5, change.DeletedCount);
            Assert.Equal(20, change.AddedStart);
            Assert.Equal(3, change.AddedCount);
        }

        [Fact]
        public void TextInlineChange_PropertiesAreMutable()
        {
            var change = new TextInlineChange(0, 0, 0, 0);
            change.DeletedStart = 5;
            change.DeletedCount = 10;
            change.AddedStart = 15;
            change.AddedCount = 20;
            Assert.Equal(5, change.DeletedStart);
            Assert.Equal(10, change.DeletedCount);
            Assert.Equal(15, change.AddedStart);
            Assert.Equal(20, change.AddedCount);
        }

        #endregion

        #region DiffResult

        [Fact]
        public void DiffResult_DefaultValues()
        {
            var result = new DiffResult();
            Assert.False(result.IsBinary);
            Assert.False(result.IsLFS);
            Assert.Equal(string.Empty, result.OldHash);
            Assert.Equal(string.Empty, result.NewHash);
            Assert.Equal(string.Empty, result.OldMode);
            Assert.Equal(string.Empty, result.NewMode);
            Assert.Null(result.TextDiff);
            Assert.Null(result.LFSDiff);
        }

        [Fact]
        public void DiffResult_FileModeChange_BothEmpty_ReturnsEmpty()
        {
            var result = new DiffResult();
            Assert.Equal(string.Empty, result.FileModeChange);
        }

        [Fact]
        public void DiffResult_FileModeChange_BothSet_ReturnsFormatted()
        {
            var result = new DiffResult
            {
                OldMode = "100644",
                NewMode = "100755"
            };
            Assert.Equal("100644 \u2192 100755", result.FileModeChange);
        }

        [Fact]
        public void DiffResult_FileModeChange_OldEmpty_ShowsZero()
        {
            var result = new DiffResult
            {
                OldMode = "",
                NewMode = "100644"
            };
            Assert.Equal("0 \u2192 100644", result.FileModeChange);
        }

        [Fact]
        public void DiffResult_FileModeChange_NewEmpty_ShowsZero()
        {
            var result = new DiffResult
            {
                OldMode = "100644",
                NewMode = ""
            };
            Assert.Equal("100644 \u2192 0", result.FileModeChange);
        }

        [Fact]
        public void DiffResult_FileModeChange_OldNull_NewNull_ReturnsEmpty()
        {
            // When both are null (not just empty), it should still return empty
            var result = new DiffResult
            {
                OldMode = null,
                NewMode = null
            };
            Assert.Equal(string.Empty, result.FileModeChange);
        }

        [Fact]
        public void DiffResult_FileModeChange_OneNullOneSet_ReturnsFormatted()
        {
            var result = new DiffResult
            {
                OldMode = null,
                NewMode = "100644"
            };
            Assert.Equal("0 \u2192 100644", result.FileModeChange);
        }

        #endregion

        #region TextDiff.GenerateNewPatchFromSelection

        [Fact]
        public void GenerateNewPatchFromSelection_Forward_WritesCorrectPatch()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -0,0 +1,3 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "line1", 0, 1));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "line2", 0, 2));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "line3", 0, 3));

            var change = new Change { Path = "test.txt" };
            var selection = new TextDiffSelection { StartLine = 2, EndLine = 4 };

            var tempFile = Path.GetTempFileName();
            try
            {
                diff.GenerateNewPatchFromSelection(change, "", selection, false, tempFile);
                var content = File.ReadAllText(tempFile);

                Assert.Contains("diff --git a/test.txt b/test.txt", content);
                Assert.Contains("new file mode 100644", content);
                Assert.Contains("+line1", content);
                Assert.Contains("+line2", content);
                Assert.Contains("+line3", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateNewPatchFromSelection_Tracked_NoNewFileMode()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -0,0 +1,1 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "line1", 0, 1));

            var change = new Change { Path = "test.txt" };
            var selection = new TextDiffSelection { StartLine = 1, EndLine = 2 };

            var tempFile = Path.GetTempFileName();
            try
            {
                diff.GenerateNewPatchFromSelection(change, "abcdef12", selection, false, tempFile);
                var content = File.ReadAllText(tempFile);

                Assert.DoesNotContain("new file mode", content);
                Assert.Contains("index 00000000...abcdef12", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateNewPatchFromSelection_Revert_WritesRevertPatch()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -0,0 +1,3 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "line1", 0, 1));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "line2", 0, 2));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "line3", 0, 3));

            var change = new Change { Path = "test.txt" };
            var selection = new TextDiffSelection { StartLine = 2, EndLine = 3 };

            var tempFile = Path.GetTempFileName();
            try
            {
                diff.GenerateNewPatchFromSelection(change, "", selection, true, tempFile);
                var content = File.ReadAllText(tempFile);

                Assert.Contains("diff --git a/test.txt b/test.txt", content);
                Assert.Contains("--- a/test.txt", content);
                // In revert mode, selected lines are prefixed with '+', unselected added lines with ' '
                Assert.Contains("+line2", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateNewPatchFromSelection_SkipsNonAddedLines()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -0,0 +1,2 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Normal, "unchanged", 1, 1));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "added", 0, 2));

            var change = new Change { Path = "test.txt" };
            var selection = new TextDiffSelection { StartLine = 1, EndLine = 3 };

            var tempFile = Path.GetTempFileName();
            try
            {
                diff.GenerateNewPatchFromSelection(change, "", selection, false, tempFile);
                var content = File.ReadAllText(tempFile);

                // Normal lines should be skipped in forward mode
                Assert.DoesNotContain("+unchanged", content);
                Assert.Contains("+added", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        #endregion

        #region TextDiff.GeneratePatchFromSelection

        [Fact]
        public void GeneratePatchFromSelection_BasicPatch_HasCorrectHeaders()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -1,3 +1,3 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Normal, "line1", 1, 1));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Deleted, "old", 2, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Added, "new", 0, 2));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Normal, "line3", 3, 3));

            var change = new Change { Path = "test.txt" };
            var selection = new TextDiffSelection
            {
                StartLine = 1,
                EndLine = 5,
                IgnoredAdds = 0,
                IgnoredDeletes = 0
            };

            var tempFile = Path.GetTempFileName();
            try
            {
                diff.GeneratePatchFromSelection(change, "abc123", selection, false, tempFile);
                var content = File.ReadAllText(tempFile);

                Assert.Contains("diff --git a/test.txt b/test.txt", content);
                Assert.Contains("index 00000000...abc123 100644", content);
                Assert.Contains("--- a/test.txt", content);
                Assert.Contains("+++ b/test.txt", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GeneratePatchFromSelection_WithOriginalPath_UsesOriginalInHeader()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -1,1 +1,1 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Normal, "line1", 1, 1));

            var change = new Change { Path = "new_name.txt", OriginalPath = "old_name.txt" };
            var selection = new TextDiffSelection { StartLine = 1, EndLine = 2 };

            var tempFile = Path.GetTempFileName();
            try
            {
                diff.GeneratePatchFromSelection(change, "abc123", selection, false, tempFile);
                var content = File.ReadAllText(tempFile);

                Assert.Contains("--- a/old_name.txt", content);
                Assert.Contains("+++ b/new_name.txt", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GeneratePatchFromSelection_NoOriginalPath_UsesPathForBoth()
        {
            var diff = new TextDiff();
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Indicator, "@@ -1,1 +1,1 @@", 0, 0));
            diff.Lines.Add(new TextDiffLine(TextDiffLineType.Normal, "line1", 1, 1));

            var change = new Change { Path = "test.txt", OriginalPath = "" };
            var selection = new TextDiffSelection { StartLine = 1, EndLine = 2 };

            var tempFile = Path.GetTempFileName();
            try
            {
                diff.GeneratePatchFromSelection(change, "abc123", selection, false, tempFile);
                var content = File.ReadAllText(tempFile);

                Assert.Contains("--- a/test.txt", content);
                Assert.Contains("+++ b/test.txt", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        #endregion

        #region TextDiff - MaxLineNumber

        [Fact]
        public void TextDiff_MaxLineNumber_DefaultIsZero()
        {
            var diff = new TextDiff();
            Assert.Equal(0, diff.MaxLineNumber);
        }

        #endregion

        #region TextDiff - Lines Collection

        [Fact]
        public void TextDiff_Lines_DefaultIsEmpty()
        {
            var diff = new TextDiff();
            Assert.Empty(diff.Lines);
        }

        [Fact]
        public void TextDiff_Lines_CanAddAndRetrieve()
        {
            var diff = new TextDiff();
            var line = new TextDiffLine(TextDiffLineType.Normal, "test", 1, 1);
            diff.Lines.Add(line);
            Assert.Single(diff.Lines);
            Assert.Same(line, diff.Lines[0]);
        }

        #endregion

        #region LFSDiff

        [Fact]
        public void LFSDiff_DefaultValues()
        {
            var lfs = new LFSDiff();
            Assert.NotNull(lfs.Old);
            Assert.NotNull(lfs.New);
        }

        #endregion

        #region BinaryDiff

        [Fact]
        public void BinaryDiff_DefaultValues()
        {
            var bin = new BinaryDiff();
            Assert.Equal(0, bin.OldSize);
            Assert.Equal(0, bin.NewSize);
        }

        [Fact]
        public void BinaryDiff_SetValues()
        {
            var bin = new BinaryDiff { OldSize = 1024, NewSize = 2048 };
            Assert.Equal(1024, bin.OldSize);
            Assert.Equal(2048, bin.NewSize);
        }

        #endregion

        #region TextInlineChange.Compare - Edge Cases

        [Fact]
        public void Compare_SingleChar_Difference()
        {
            var result = TextInlineChange.Compare("a", "b");
            Assert.NotEmpty(result);
            Assert.Equal(1, result.Sum(c => c.DeletedCount));
            Assert.Equal(1, result.Sum(c => c.AddedCount));
        }

        [Fact]
        public void Compare_SingleChar_Same()
        {
            var result = TextInlineChange.Compare("a", "a");
            Assert.Empty(result);
        }

        [Fact]
        public void Compare_LongIdenticalPrefix_SmallDiffAtEnd()
        {
            // Use a space delimiter to separate prefix from diff part,
            // so the chunker creates separate chunks for the identical prefix and the diff
            var prefix = new string('x', 100);
            var result = TextInlineChange.Compare(prefix + " old", prefix + " new");
            Assert.NotEmpty(result);
            var totalDeleted = result.Sum(c => c.DeletedCount);
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.Equal(3, totalDeleted);
            Assert.Equal(3, totalAdded);
        }

        [Fact]
        public void Compare_LongIdenticalSuffix_SmallDiffAtStart()
        {
            var suffix = new string('y', 100);
            var result = TextInlineChange.Compare("old " + suffix, "new " + suffix);
            Assert.NotEmpty(result);
            var totalDeleted = result.Sum(c => c.DeletedCount);
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.Equal(3, totalDeleted);
            Assert.Equal(3, totalAdded);
        }

        [Fact]
        public void Compare_AllDelimiters()
        {
            // Each char is its own chunk
            var result = TextInlineChange.Compare("+-*/", "=!,:;");
            Assert.NotEmpty(result);
        }

        [Fact]
        public void Compare_MultipleSpaces_AsSeparateChunks()
        {
            // Spaces are delimiters, each space is its own chunk
            var result = TextInlineChange.Compare("a  b", "a   b");
            Assert.NotEmpty(result);
            // One space added
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.Equal(1, totalAdded);
        }

        #endregion

        #region TextDiffLineType Enum

        [Fact]
        public void TextDiffLineType_HasExpectedValues()
        {
            Assert.Equal(0, (int)TextDiffLineType.None);
            Assert.Equal(1, (int)TextDiffLineType.Normal);
            Assert.Equal(2, (int)TextDiffLineType.Indicator);
            Assert.Equal(3, (int)TextDiffLineType.Added);
            Assert.Equal(4, (int)TextDiffLineType.Deleted);
        }

        #endregion

        #region TextDiffLine - Highlights

        [Fact]
        public void TextDiffLine_Highlights_DefaultEmpty()
        {
            var line = new TextDiffLine();
            Assert.NotNull(line.Highlights);
            Assert.Empty(line.Highlights);
        }

        [Fact]
        public void TextDiffLine_Highlights_CanAddRanges()
        {
            var line = new TextDiffLine(TextDiffLineType.Added, "hello world", 0, 1);
            line.Highlights.Add(new TextRange(6, 5));
            Assert.Single(line.Highlights);
            Assert.Equal(6, line.Highlights[0].Start);
            Assert.Equal(10, line.Highlights[0].End);
        }

        [Fact]
        public void TextDiffLine_NoNewLineEndOfFile_DefaultFalse()
        {
            var line = new TextDiffLine();
            Assert.False(line.NoNewLineEndOfFile);
        }

        [Fact]
        public void TextDiffLine_NoNewLineEndOfFile_CanBeSet()
        {
            var line = new TextDiffLine();
            line.NoNewLineEndOfFile = true;
            Assert.True(line.NoNewLineEndOfFile);
        }

        #endregion

        #region SubmoduleDiff

        [Fact]
        public void SubmoduleDiff_DefaultValues()
        {
            var sub = new SubmoduleDiff();
            Assert.Null(sub.Old);
            Assert.Null(sub.New);
        }

        #endregion

        #region TextInlineChange.Compare - Realistic Diff Scenarios

        [Fact]
        public void Compare_CSharpMethodSignatureChange()
        {
            var oldLine = "public void Process(string input)";
            var newLine = "public bool Process(string input, int count)";
            var result = TextInlineChange.Compare(oldLine, newLine);
            Assert.NotEmpty(result);
            // 'void' changed to 'bool', and ', int count' added
        }

        [Fact]
        public void Compare_IndentationChange()
        {
            // Tabs and spaces are delimiters
            var result = TextInlineChange.Compare("    code()", "        code()");
            Assert.NotEmpty(result);
            // Four additional spaces
            var totalAdded = result.Sum(c => c.AddedCount);
            Assert.Equal(4, totalAdded);
        }

        [Fact]
        public void Compare_EmptyToSingleDelimiter()
        {
            var result = TextInlineChange.Compare("", " ");
            Assert.NotEmpty(result);
            Assert.Equal(1, result.Sum(c => c.AddedCount));
            Assert.Equal(0, result.Sum(c => c.DeletedCount));
        }

        [Fact]
        public void Compare_SingleDelimiterToEmpty()
        {
            var result = TextInlineChange.Compare(" ", "");
            Assert.NotEmpty(result);
            Assert.Equal(0, result.Sum(c => c.AddedCount));
            Assert.Equal(1, result.Sum(c => c.DeletedCount));
        }

        #endregion
    }
}
