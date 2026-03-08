using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    public class InlineElementTests
    {
        #region Constructor

        [Fact]
        public void Constructor_SetsAllProperties()
        {
            var element = new InlineElement(InlineElementType.Link, 5, 10, "https://example.com");
            Assert.Equal(InlineElementType.Link, element.Type);
            Assert.Equal(5, element.Start);
            Assert.Equal(10, element.Length);
            Assert.Equal("https://example.com", element.Link);
        }

        [Theory]
        [InlineData(InlineElementType.Keyword)]
        [InlineData(InlineElementType.Link)]
        [InlineData(InlineElementType.CommitSHA)]
        [InlineData(InlineElementType.Code)]
        public void Constructor_AllElementTypes(InlineElementType type)
        {
            var element = new InlineElement(type, 0, 1, "test");
            Assert.Equal(type, element.Type);
        }

        #endregion

        #region IsIntersecting

        [Fact]
        public void IsIntersecting_SameStart_ReturnsTrue()
        {
            var element = new InlineElement(InlineElementType.Keyword, 5, 10, "");
            Assert.True(element.IsIntersecting(5, 3));
        }

        [Fact]
        public void IsIntersecting_OverlapFromBefore_ReturnsTrue()
        {
            // Element at [5..15), check range [3..8) => overlaps at 5
            var element = new InlineElement(InlineElementType.Keyword, 5, 10, "");
            Assert.True(element.IsIntersecting(3, 5));
        }

        [Fact]
        public void IsIntersecting_OverlapFromAfter_ReturnsTrue()
        {
            // Element at [5..15), check range [10..20) => overlaps at 10
            var element = new InlineElement(InlineElementType.Keyword, 5, 10, "");
            Assert.True(element.IsIntersecting(10, 10));
        }

        [Fact]
        public void IsIntersecting_CompletelyBefore_ReturnsFalse()
        {
            // Element at [5..15), check range [0..4) => no overlap
            var element = new InlineElement(InlineElementType.Keyword, 5, 10, "");
            Assert.False(element.IsIntersecting(0, 4));
        }

        [Fact]
        public void IsIntersecting_CompletelyAfter_ReturnsFalse()
        {
            // Element at [5..15), check range [15..20) => no overlap
            var element = new InlineElement(InlineElementType.Keyword, 5, 10, "");
            Assert.False(element.IsIntersecting(15, 5));
        }

        [Fact]
        public void IsIntersecting_AdjacentBefore_ReturnsFalse()
        {
            // Element at [5..15), check range [0..5) => touching but not overlapping
            var element = new InlineElement(InlineElementType.Keyword, 5, 10, "");
            Assert.False(element.IsIntersecting(0, 5));
        }

        [Fact]
        public void IsIntersecting_ContainedInside_ReturnsTrue()
        {
            // Element at [5..15), check range [7..10) => fully inside
            var element = new InlineElement(InlineElementType.Keyword, 5, 10, "");
            Assert.True(element.IsIntersecting(7, 3));
        }

        [Fact]
        public void IsIntersecting_FullyContaining_ReturnsTrue()
        {
            // Element at [5..15), check range [0..20) => fully contains
            var element = new InlineElement(InlineElementType.Keyword, 5, 10, "");
            Assert.True(element.IsIntersecting(0, 20));
        }

        [Fact]
        public void IsIntersecting_ZeroLengthAtStart_ReturnsTrue()
        {
            // Same start position with zero length
            var element = new InlineElement(InlineElementType.Keyword, 5, 10, "");
            Assert.True(element.IsIntersecting(5, 0));
        }

        #endregion
    }

    public class InlineElementCollectorTests
    {
        #region Basic Operations

        [Fact]
        public void Count_EmptyCollector_ReturnsZero()
        {
            var collector = new InlineElementCollector();
            Assert.Equal(0, collector.Count);
        }

        [Fact]
        public void Add_IncreasesCount()
        {
            var collector = new InlineElementCollector();
            collector.Add(new InlineElement(InlineElementType.Keyword, 0, 5, ""));
            Assert.Equal(1, collector.Count);
        }

        [Fact]
        public void Indexer_ReturnsCorrectElement()
        {
            var collector = new InlineElementCollector();
            var element = new InlineElement(InlineElementType.Link, 10, 5, "http://test.com");
            collector.Add(element);
            Assert.Same(element, collector[0]);
        }

        [Fact]
        public void Clear_ResetsCount()
        {
            var collector = new InlineElementCollector();
            collector.Add(new InlineElement(InlineElementType.Keyword, 0, 5, ""));
            collector.Add(new InlineElement(InlineElementType.Link, 10, 5, ""));
            collector.Clear();
            Assert.Equal(0, collector.Count);
        }

        #endregion

        #region Intersect

        [Fact]
        public void Intersect_NoElements_ReturnsNull()
        {
            var collector = new InlineElementCollector();
            Assert.Null(collector.Intersect(5, 10));
        }

        [Fact]
        public void Intersect_OverlappingElement_ReturnsElement()
        {
            var collector = new InlineElementCollector();
            var element = new InlineElement(InlineElementType.Keyword, 5, 10, "");
            collector.Add(element);
            Assert.Same(element, collector.Intersect(7, 3));
        }

        [Fact]
        public void Intersect_NonOverlapping_ReturnsNull()
        {
            var collector = new InlineElementCollector();
            collector.Add(new InlineElement(InlineElementType.Keyword, 5, 10, ""));
            Assert.Null(collector.Intersect(20, 5));
        }

        [Fact]
        public void Intersect_MultipleElements_ReturnsFirstOverlap()
        {
            var collector = new InlineElementCollector();
            var first = new InlineElement(InlineElementType.Keyword, 0, 5, "first");
            var second = new InlineElement(InlineElementType.Link, 10, 5, "second");
            collector.Add(first);
            collector.Add(second);

            // Should return the first matching element
            Assert.Same(first, collector.Intersect(3, 10));
        }

        #endregion

        #region Sort

        [Fact]
        public void Sort_OrdersByStart()
        {
            var collector = new InlineElementCollector();
            collector.Add(new InlineElement(InlineElementType.Keyword, 20, 5, "c"));
            collector.Add(new InlineElement(InlineElementType.Keyword, 5, 5, "a"));
            collector.Add(new InlineElement(InlineElementType.Keyword, 10, 5, "b"));
            collector.Sort();

            Assert.Equal(5, collector[0].Start);
            Assert.Equal(10, collector[1].Start);
            Assert.Equal(20, collector[2].Start);
        }

        #endregion
    }
}
