using System.Globalization;

using Avalonia.Media;

using Komorebi.Converters;
using Komorebi.Models;

namespace Komorebi.Tests.Converters
{
    public class FilterModeConvertersTests
    {
        #region ToBorderBrush

        [Fact]
        public void ToBorderBrush_Included_ReturnsGreen()
        {
            var result = FilterModeConverters.ToBorderBrush.Convert(
                FilterMode.Included, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Green, result);
        }

        [Fact]
        public void ToBorderBrush_Excluded_ReturnsRed()
        {
            var result = FilterModeConverters.ToBorderBrush.Convert(
                FilterMode.Excluded, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Red, result);
        }

        [Fact]
        public void ToBorderBrush_None_ReturnsTransparent()
        {
            var result = FilterModeConverters.ToBorderBrush.Convert(
                FilterMode.None, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Transparent, result);
        }

        #endregion
    }
}
