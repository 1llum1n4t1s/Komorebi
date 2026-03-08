using System.Globalization;

using Avalonia;

using Komorebi.Converters;

namespace Komorebi.Tests.Converters
{
    public class IntConvertersTests
    {
        #region IsGreaterThanZero

        [Theory]
        [InlineData(1, true)]
        [InlineData(5, true)]
        [InlineData(100, true)]
        public void IsGreaterThanZero_PositiveValues_ReturnsTrue(int input, bool expected)
        {
            var result = IntConverters.IsGreaterThanZero.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(-100, false)]
        public void IsGreaterThanZero_ZeroOrNegative_ReturnsFalse(int input, bool expected)
        {
            var result = IntConverters.IsGreaterThanZero.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        #endregion

        #region IsGreaterThanFour

        [Theory]
        [InlineData(5, true)]
        [InlineData(10, true)]
        [InlineData(100, true)]
        public void IsGreaterThanFour_GreaterThanFour_ReturnsTrue(int input, bool expected)
        {
            var result = IntConverters.IsGreaterThanFour.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(4, false)]
        [InlineData(3, false)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        public void IsGreaterThanFour_FourOrLess_ReturnsFalse(int input, bool expected)
        {
            var result = IntConverters.IsGreaterThanFour.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        #endregion

        #region IsZero

        [Fact]
        public void IsZero_Zero_ReturnsTrue()
        {
            var result = IntConverters.IsZero.Convert(0, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(true, result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(100)]
        public void IsZero_NonZero_ReturnsFalse(int input)
        {
            var result = IntConverters.IsZero.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        #endregion

        #region IsNotOne

        [Theory]
        [InlineData(0, true)]
        [InlineData(2, true)]
        [InlineData(-1, true)]
        [InlineData(100, true)]
        public void IsNotOne_NotOne_ReturnsTrue(int input, bool expected)
        {
            var result = IntConverters.IsNotOne.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsNotOne_One_ReturnsFalse()
        {
            var result = IntConverters.IsNotOne.Convert(1, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        #endregion

        #region ToTreeMargin

        [Fact]
        public void ToTreeMargin_Zero_ReturnsZeroMargin()
        {
            var result = IntConverters.ToTreeMargin.Convert(0, typeof(Thickness), null, CultureInfo.InvariantCulture);
            Assert.Equal(new Thickness(0, 0, 0, 0), result);
        }

        [Fact]
        public void ToTreeMargin_One_Returns16LeftMargin()
        {
            var result = IntConverters.ToTreeMargin.Convert(1, typeof(Thickness), null, CultureInfo.InvariantCulture);
            Assert.Equal(new Thickness(16, 0, 0, 0), result);
        }

        [Fact]
        public void ToTreeMargin_Three_Returns48LeftMargin()
        {
            var result = IntConverters.ToTreeMargin.Convert(3, typeof(Thickness), null, CultureInfo.InvariantCulture);
            Assert.Equal(new Thickness(48, 0, 0, 0), result);
        }

        [Fact]
        public void ToTreeMargin_Negative_ReturnsNegativeLeftMargin()
        {
            var result = IntConverters.ToTreeMargin.Convert(-1, typeof(Thickness), null, CultureInfo.InvariantCulture);
            Assert.Equal(new Thickness(-16, 0, 0, 0), result);
        }

        #endregion

        // Note: ToBookmarkBrush is skipped because it depends on Application.Current resource lookup.
        // Note: ToUnsolvedDesc is skipped because it depends on App.Text() which requires app initialization.
    }
}
