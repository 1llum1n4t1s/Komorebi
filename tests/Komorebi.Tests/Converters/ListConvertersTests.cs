using System.Collections;
using System.Collections.Generic;
using System.Globalization;

using Komorebi.Converters;
using Komorebi.Models;

namespace Komorebi.Tests.Converters
{
    public class ListConvertersTests
    {
        #region ToCount

        [Fact]
        public void ToCount_NullList_ReturnsZeroCount()
        {
            var result = ListConverters.ToCount.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("(0)", result);
        }

        [Fact]
        public void ToCount_EmptyList_ReturnsZeroCount()
        {
            var list = new ArrayList();
            var result = ListConverters.ToCount.Convert(list, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("(0)", result);
        }

        [Fact]
        public void ToCount_SingleItemList_ReturnsOneCount()
        {
            var list = new ArrayList { "item1" };
            var result = ListConverters.ToCount.Convert(list, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("(1)", result);
        }

        [Fact]
        public void ToCount_MultipleItems_ReturnsCorrectCount()
        {
            var list = new ArrayList { "a", "b", "c", "d", "e" };
            var result = ListConverters.ToCount.Convert(list, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("(5)", result);
        }

        [Fact]
        public void ToCount_GenericList_ReturnsCorrectCount()
        {
            IList list = new List<string> { "hello", "world" };
            var result = ListConverters.ToCount.Convert(list, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("(2)", result);
        }

        #endregion

        #region IsNullOrEmpty

        [Fact]
        public void IsNullOrEmpty_NullList_ReturnsTrue()
        {
            var result = ListConverters.IsNullOrEmpty.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(true, result);
        }

        [Fact]
        public void IsNullOrEmpty_EmptyList_ReturnsTrue()
        {
            var list = new ArrayList();
            var result = ListConverters.IsNullOrEmpty.Convert(list, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(true, result);
        }

        [Fact]
        public void IsNullOrEmpty_NonEmptyList_ReturnsFalse()
        {
            var list = new ArrayList { "item" };
            var result = ListConverters.IsNullOrEmpty.Convert(list, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        #endregion

        #region IsNotNullOrEmpty

        [Fact]
        public void IsNotNullOrEmpty_NullList_ReturnsFalse()
        {
            var result = ListConverters.IsNotNullOrEmpty.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        [Fact]
        public void IsNotNullOrEmpty_EmptyList_ReturnsFalse()
        {
            var list = new ArrayList();
            var result = ListConverters.IsNotNullOrEmpty.Convert(list, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        [Fact]
        public void IsNotNullOrEmpty_NonEmptyList_ReturnsTrue()
        {
            var list = new ArrayList { "item" };
            var result = ListConverters.IsNotNullOrEmpty.Convert(list, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(true, result);
        }

        #endregion

        #region IsOnlyTop100Shows

        [Fact]
        public void IsOnlyTop100Shows_NullList_ReturnsFalse()
        {
            var result = ListConverters.IsOnlyTop100Shows.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        [Fact]
        public void IsOnlyTop100Shows_Exactly100Items_ReturnsFalse()
        {
            var list = new ArrayList();
            for (int i = 0; i < 100; i++)
                list.Add(i);
            var result = ListConverters.IsOnlyTop100Shows.Convert(list, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        [Fact]
        public void IsOnlyTop100Shows_101Items_ReturnsTrue()
        {
            var list = new ArrayList();
            for (int i = 0; i < 101; i++)
                list.Add(i);
            var result = ListConverters.IsOnlyTop100Shows.Convert(list, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(true, result);
        }

        [Fact]
        public void IsOnlyTop100Shows_50Items_ReturnsFalse()
        {
            var list = new ArrayList();
            for (int i = 0; i < 50; i++)
                list.Add(i);
            var result = ListConverters.IsOnlyTop100Shows.Convert(list, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        #endregion

        #region Top100Changes

        [Fact]
        public void Top100Changes_NullList_ReturnsNull()
        {
            var result = ListConverters.Top100Changes.Convert(null, typeof(List<Change>), null, CultureInfo.InvariantCulture);
            Assert.Null(result);
        }

        [Fact]
        public void Top100Changes_LessThan100Items_ReturnsSameList()
        {
            var list = new List<Change>();
            for (int i = 0; i < 50; i++)
                list.Add(new Change());
            var result = ListConverters.Top100Changes.Convert(list, typeof(List<Change>), null, CultureInfo.InvariantCulture);
            Assert.Same(list, result);
        }

        [Fact]
        public void Top100Changes_Exactly100Items_ReturnsSameList()
        {
            var list = new List<Change>();
            for (int i = 0; i < 100; i++)
                list.Add(new Change());
            var result = ListConverters.Top100Changes.Convert(list, typeof(List<Change>), null, CultureInfo.InvariantCulture);
            Assert.Same(list, result);
        }

        [Fact]
        public void Top100Changes_MoreThan100Items_ReturnsFirst100()
        {
            var list = new List<Change>();
            for (int i = 0; i < 200; i++)
                list.Add(new Change());
            var result = (List<Change>)ListConverters.Top100Changes.Convert(list, typeof(List<Change>), null, CultureInfo.InvariantCulture);
            Assert.NotNull(result);
            Assert.Equal(100, result.Count);
            Assert.NotSame(list, result);
        }

        #endregion
    }
}
