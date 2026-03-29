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
        public void Compare_File10_AfterFile2()
        {
            var result = NumericSort.Compare("file10", "file2");
            Assert.True(result > 0);
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
            var result = NumericSort.Compare("file10", "file10");
            Assert.Equal(0, result);
        }

        [Fact]
        public void Compare_MultipleNumericSegments()
        {
            // "v1.2.10" should come after "v1.2.9"
            var result = NumericSort.Compare("v1.2.9", "v1.2.10");
            Assert.True(result < 0);
        }

        [Fact]
        public void Compare_LargeNumbers()
        {
            var result = NumericSort.Compare("item100", "item1000");
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

        [Fact]
        public void Compare_AlphabeticOrder_Reverse()
        {
            var result = NumericSort.Compare("banana", "apple");
            Assert.True(result > 0);
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

        [Fact]
        public void Compare_PureNumberStrings()
        {
            var result = NumericSort.Compare("100", "99");
            Assert.True(result > 0);
        }

        [Fact]
        public void Compare_PureNumberStrings_Equal()
        {
            var result = NumericSort.Compare("42", "42");
            Assert.Equal(0, result);
        }

        [Fact]
        public void Compare_NumberWithLeadingZeros()
        {
            // "007" has same length as "007" -> ordinal compare -> equal
            var result = NumericSort.Compare("file007", "file007");
            Assert.Equal(0, result);
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
        public void Compare_LongerString_AfterShorter_WhenPrefixMatches()
        {
            var result = NumericSort.Compare("file2", "file");
            Assert.True(result > 0);
        }

        [Fact]
        public void Compare_EmptyStrings_ReturnsZero()
        {
            var result = NumericSort.Compare("", "");
            Assert.Equal(0, result);
        }

        [Fact]
        public void Compare_EmptyVsNonEmpty_ReturnsNegative()
        {
            var result = NumericSort.Compare("", "a");
            Assert.True(result < 0);
        }

        [Fact]
        public void Compare_NonEmptyVsEmpty_ReturnsPositive()
        {
            var result = NumericSort.Compare("a", "");
            Assert.True(result > 0);
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
        // Real-world file name patterns
        // -----------------------------------------------------------

        [Theory]
        [InlineData("img1.png", "img2.png", -1)]
        [InlineData("img2.png", "img10.png", -1)]
        [InlineData("img10.png", "img10.png", 0)]
        [InlineData("img20.png", "img3.png", 1)]
        [InlineData("chapter1_section2", "chapter1_section10", -1)]
        [InlineData("chapter2_section1", "chapter10_section1", -1)]
        public void Compare_FileNamePatterns(string s1, string s2, int expectedSign)
        {
            var result = NumericSort.Compare(s1, s2);

            if (expectedSign < 0)
                Assert.True(result < 0, $"Expected '{s1}' < '{s2}', but got {result}");
            else if (expectedSign > 0)
                Assert.True(result > 0, $"Expected '{s1}' > '{s2}', but got {result}");
            else
                Assert.Equal(0, result);
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
