using System.Globalization;

using Avalonia;

using Komorebi.Converters;

namespace Komorebi.Tests.Converters
{
    public class DoubleConvertersTests
    {
        #region Increase

        [Theory]
        [InlineData(0.0, 1.0)]
        [InlineData(1.0, 2.0)]
        [InlineData(-1.0, 0.0)]
        [InlineData(99.5, 100.5)]
        public void Increase_ReturnsValuePlusOne(double input, double expected)
        {
            var result = DoubleConverters.Increase.Convert(input, typeof(double), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        #endregion

        #region Decrease

        [Theory]
        [InlineData(0.0, -1.0)]
        [InlineData(1.0, 0.0)]
        [InlineData(2.5, 1.5)]
        [InlineData(-1.0, -2.0)]
        public void Decrease_ReturnsValueMinusOne(double input, double expected)
        {
            var result = DoubleConverters.Decrease.Convert(input, typeof(double), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        #endregion

        #region ToPercentage

        [Theory]
        [InlineData(0.0, "0%")]
        [InlineData(0.5, "50%")]
        [InlineData(1.0, "100%")]
        [InlineData(0.333, "33%")]
        [InlineData(0.999, "100%")]
        [InlineData(0.001, "0%")]
        public void ToPercentage_ReturnsFormattedPercentage(double input, string expected)
        {
            var result = DoubleConverters.ToPercentage.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToPercentage_GreaterThan1_ReturnsOver100Percent()
        {
            var result = DoubleConverters.ToPercentage.Convert(1.5, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("150%", result);
        }

        [Fact]
        public void ToPercentage_Negative_ReturnsNegativePercentage()
        {
            var result = DoubleConverters.ToPercentage.Convert(-0.25, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("-25%", result);
        }

        #endregion

        #region OneMinusToPercentage

        [Theory]
        [InlineData(0.0, "100%")]
        [InlineData(0.5, "50%")]
        [InlineData(1.0, "0%")]
        [InlineData(0.25, "75%")]
        [InlineData(0.75, "25%")]
        public void OneMinusToPercentage_ReturnsComplementPercentage(double input, string expected)
        {
            var result = DoubleConverters.OneMinusToPercentage.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void OneMinusToPercentage_GreaterThan1_ReturnsNegativePercentage()
        {
            var result = DoubleConverters.OneMinusToPercentage.Convert(1.5, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("-50%", result);
        }

        #endregion

        #region ToLeftMargin

        [Fact]
        public void ToLeftMargin_Zero_ReturnsZeroThickness()
        {
            var result = DoubleConverters.ToLeftMargin.Convert(0.0, typeof(Thickness), null, CultureInfo.InvariantCulture);
            Assert.Equal(new Thickness(0, 0, 0, 0), result);
        }

        [Fact]
        public void ToLeftMargin_PositiveValue_ReturnsLeftMarginOnly()
        {
            var result = DoubleConverters.ToLeftMargin.Convert(10.5, typeof(Thickness), null, CultureInfo.InvariantCulture);
            Assert.Equal(new Thickness(10.5, 0, 0, 0), result);
        }

        [Fact]
        public void ToLeftMargin_NegativeValue_ReturnsNegativeLeftMargin()
        {
            var result = DoubleConverters.ToLeftMargin.Convert(-5.0, typeof(Thickness), null, CultureInfo.InvariantCulture);
            Assert.Equal(new Thickness(-5.0, 0, 0, 0), result);
        }

        #endregion
    }
}
