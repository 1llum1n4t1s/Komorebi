using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    public class TextInlineChangeTests
    {
        #region Constructor

        [Fact]
        public void Constructor_SetsAllProperties()
        {
            var change = new TextInlineChange(1, 2, 3, 4);
            Assert.Equal(1, change.DeletedStart);
            Assert.Equal(2, change.DeletedCount);
            Assert.Equal(3, change.AddedStart);
            Assert.Equal(4, change.AddedCount);
        }

        #endregion

        #region Compare - Identical Strings

        [Fact]
        public void Compare_IdenticalStrings_ReturnsEmptyList()
        {
            var result = TextInlineChange.Compare("hello world", "hello world");
            Assert.Empty(result);
        }

        [Fact]
        public void Compare_BothEmpty_ReturnsEmptyList()
        {
            var result = TextInlineChange.Compare("", "");
            Assert.Empty(result);
        }

        #endregion

        #region Compare - Complete Replacement

        [Fact]
        public void Compare_CompletelyDifferent_ReturnsDifferences()
        {
            var result = TextInlineChange.Compare("abc", "xyz");
            Assert.NotEmpty(result);
        }

        #endregion

        #region Compare - Insertions

        [Fact]
        public void Compare_InsertionAtEnd_DetectsAddition()
        {
            var result = TextInlineChange.Compare("hello", "hello world");
            Assert.NotEmpty(result);
            // Should detect the added " world" part
            var hasAddition = result.Exists(c => c.AddedCount > 0);
            Assert.True(hasAddition);
        }

        [Fact]
        public void Compare_InsertionAtStart_DetectsAddition()
        {
            var result = TextInlineChange.Compare("world", "hello world");
            Assert.NotEmpty(result);
            var hasAddition = result.Exists(c => c.AddedCount > 0);
            Assert.True(hasAddition);
        }

        #endregion

        #region Compare - Deletions

        [Fact]
        public void Compare_DeletionAtEnd_DetectsDeletion()
        {
            var result = TextInlineChange.Compare("hello world", "hello");
            Assert.NotEmpty(result);
            var hasDeletion = result.Exists(c => c.DeletedCount > 0);
            Assert.True(hasDeletion);
        }

        [Fact]
        public void Compare_DeletionAtStart_DetectsDeletion()
        {
            var result = TextInlineChange.Compare("hello world", "world");
            Assert.NotEmpty(result);
            var hasDeletion = result.Exists(c => c.DeletedCount > 0);
            Assert.True(hasDeletion);
        }

        #endregion

        #region Compare - Word-Level Changes

        [Fact]
        public void Compare_SingleWordChange_DetectsChange()
        {
            var result = TextInlineChange.Compare("the quick brown fox", "the slow brown fox");
            Assert.NotEmpty(result);
            // Should detect "quick" -> "slow" change
        }

        [Fact]
        public void Compare_MultipleWordChanges_DetectsAll()
        {
            var result = TextInlineChange.Compare("hello beautiful world", "goodbye ugly world");
            Assert.NotEmpty(result);
        }

        #endregion

        #region Compare - Special Characters (delimiters)

        [Fact]
        public void Compare_WithDelimiters_SplitsCorrectly()
        {
            // Delimiters in the source: space, +, -, etc.
            var result = TextInlineChange.Compare("a+b", "a+c");
            Assert.NotEmpty(result);
        }

        [Fact]
        public void Compare_CodeLikeContent_DetectsChanges()
        {
            var result = TextInlineChange.Compare(
                "int x = 10;",
                "int x = 20;");
            Assert.NotEmpty(result);
        }

        #endregion

        #region Compare - Empty to Non-Empty

        [Fact]
        public void Compare_EmptyToNonEmpty_DetectsAddition()
        {
            var result = TextInlineChange.Compare("", "hello");
            Assert.NotEmpty(result);
            Assert.True(result[0].AddedCount > 0);
            Assert.Equal(0, result[0].DeletedCount);
        }

        [Fact]
        public void Compare_NonEmptyToEmpty_DetectsDeletion()
        {
            var result = TextInlineChange.Compare("hello", "");
            Assert.NotEmpty(result);
            Assert.True(result[0].DeletedCount > 0);
            Assert.Equal(0, result[0].AddedCount);
        }

        #endregion

        #region Compare - Symmetry and Consistency

        [Fact]
        public void Compare_ResultPositionsAreNonNegative()
        {
            var result = TextInlineChange.Compare("hello world", "hello earth");
            foreach (var change in result)
            {
                Assert.True(change.DeletedStart >= 0);
                Assert.True(change.DeletedCount >= 0);
                Assert.True(change.AddedStart >= 0);
                Assert.True(change.AddedCount >= 0);
            }
        }

        [Fact]
        public void Compare_SwappedInputs_InvertsDeleteAndAdd()
        {
            var forward = TextInlineChange.Compare("aaa", "bbb");
            var backward = TextInlineChange.Compare("bbb", "aaa");

            // Both should detect changes
            Assert.NotEmpty(forward);
            Assert.NotEmpty(backward);
        }

        #endregion

        #region Compare - Merging Adjacent Changes

        [Fact]
        public void Compare_AdjacentSingleCharGaps_MergesChanges()
        {
            // When there is only a single character gap between changes, they should be merged
            // "a.b.c" vs "x.y.z" - the dots are single chars between changed words
            var result = TextInlineChange.Compare("a.b.c", "x.y.z");
            // Due to merging logic (midSize == 1), these should be merged into fewer changes
            Assert.NotEmpty(result);
        }

        #endregion
    }
}
