// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class NumericSortTests
    {
        // -----------------------------------------------------------
        // Natural sort — numeric portions compared by value
        // -----------------------------------------------------------

        [Fact]
        public void Compare_File2_BeforeFile10()
        {
            var result = NumericSort.Compare("file2", "file10");
            Assert.True(result < 0);
        }

        [Fact]
        public void Compare_File1_BeforeFile2()
        {
            var result = NumericSort.Compare("file1", "file2");
            Assert.True(result < 0);
        }

        [Fact]
        public void Compare_SameNumericValues_ReturnsZero()
        {
            // 同じリテラル同士はインターンされ ReferenceEquals で即 0 になり数値チャンク比較を通らないため、
            // 内容が同じ別インスタンスの文字列を作って比較する。
            var left = new string("file10".AsSpan());
            var right = new string("file10".AsSpan());
            Assert.False(ReferenceEquals(left, right));

            var result = NumericSort.Compare(left, right);
            Assert.Equal(0, result);
        }

        [Fact]
        public void Compare_MultipleNumericSegments()
        {
            // "v1.2.10" should come after "v1.2.9"
            var result = NumericSort.Compare("v1.2.9", "v1.2.10");
            Assert.True(result < 0);
        }

        // -----------------------------------------------------------
        // Case-insensitive string comparison
        // -----------------------------------------------------------

        [Fact]
        public void Compare_CaseInsensitive_ReturnsZero()
        {
            var result = NumericSort.Compare("ABC", "abc");
            Assert.Equal(0, result);
        }

        [Fact]
        public void Compare_CaseInsensitive_WithNumbers()
        {
            var result = NumericSort.Compare("File2", "file2");
            Assert.Equal(0, result);
        }

        [Fact]
        public void Compare_AlphabeticOrder()
        {
            var result = NumericSort.Compare("apple", "banana");
            Assert.True(result < 0);
        }

        // -----------------------------------------------------------
        // Mixed digit / non-digit transitions
        // -----------------------------------------------------------

        [Fact]
        public void Compare_DigitVsLetter_AtSamePosition()
        {
            // '1' (digit) vs 'a' (letter) at position 0
            var result = NumericSort.Compare("1abc", "abc1");
            Assert.NotEqual(0, result);
        }

        // -----------------------------------------------------------
        // Length differences
        // -----------------------------------------------------------

        [Fact]
        public void Compare_ShorterString_BeforeLonger_WhenPrefixMatches()
        {
            var result = NumericSort.Compare("file", "file2");
            Assert.True(result < 0);
        }

        [Fact]
        public void Compare_EmptyVsNonEmpty_ReturnsNegative()
        {
            var result = NumericSort.Compare("", "a");
            Assert.True(result < 0);
        }

        // -----------------------------------------------------------
        // Null handling (bug fix verification)
        // -----------------------------------------------------------

        [Fact]
        public void Compare_BothNull_ReturnsZero()
        {
            var result = NumericSort.Compare(null, null);
            Assert.Equal(0, result);
        }

        [Fact]
        public void Compare_FirstNull_ReturnsNegative()
        {
            var result = NumericSort.Compare(null, "abc");
            Assert.True(result < 0);
        }

        [Fact]
        public void Compare_SecondNull_ReturnsPositive()
        {
            var result = NumericSort.Compare("abc", null);
            Assert.True(result > 0);
        }

        // -----------------------------------------------------------
        // Sorting a list using NumericSort
        // -----------------------------------------------------------

        [Fact]
        public void Compare_SortsList_InNaturalOrder()
        {
            var files = new List<string>
            {
                "file10.txt",
                "file2.txt",
                "file1.txt",
                "file20.txt",
                "file3.txt"
            };

            files.Sort(NumericSort.Compare);

            Assert.Equal(new[]
            {
                "file1.txt",
                "file2.txt",
                "file3.txt",
                "file10.txt",
                "file20.txt"
            }, files);
        }
    }
}
