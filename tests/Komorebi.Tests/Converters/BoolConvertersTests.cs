using System.Globalization;

using Avalonia.Media;

using Komorebi.Converters;

namespace Komorebi.Tests.Converters
{
    public class BoolConvertersTests
    {
        #region IsBoldToFontWeight

        [Fact]
        public void IsBoldToFontWeight_True_ReturnsBold()
        {
            var result = BoolConverters.IsBoldToFontWeight.Convert(true, typeof(FontWeight), null, CultureInfo.InvariantCulture);
            Assert.Equal(FontWeight.Bold, result);
        }

        [Fact]
        public void IsBoldToFontWeight_False_ReturnsRegular()
        {
            var result = BoolConverters.IsBoldToFontWeight.Convert(false, typeof(FontWeight), null, CultureInfo.InvariantCulture);
            Assert.Equal(FontWeight.Regular, result);
        }

        #endregion

        #region IsMergedToOpacity

        [Fact]
        public void IsMergedToOpacity_True_Returns1()
        {
            var result = BoolConverters.IsMergedToOpacity.Convert(true, typeof(double), null, CultureInfo.InvariantCulture);
            Assert.Equal(1.0, result);
        }

        [Fact]
        public void IsMergedToOpacity_False_Returns0Point65()
        {
            var result = BoolConverters.IsMergedToOpacity.Convert(false, typeof(double), null, CultureInfo.InvariantCulture);
            Assert.Equal(0.65, result);
        }

        #endregion

        // Note: IsWarningToBrush is skipped because it depends on Application.Current resource lookup
        // which requires a full Avalonia application context (headless Avalonia).
    }
}
