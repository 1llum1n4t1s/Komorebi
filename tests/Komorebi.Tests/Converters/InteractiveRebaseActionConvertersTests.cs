using System.Globalization;

using Avalonia.Media;

using Komorebi.Converters;
using Komorebi.Models;

namespace Komorebi.Tests.Converters
{
    public class InteractiveRebaseActionConvertersTests
    {
        #region ToName

        [Theory]
        [InlineData(InteractiveRebaseAction.Pick, "Pick")]
        [InlineData(InteractiveRebaseAction.Edit, "Edit")]
        [InlineData(InteractiveRebaseAction.Reword, "Reword")]
        [InlineData(InteractiveRebaseAction.Squash, "Squash")]
        [InlineData(InteractiveRebaseAction.Fixup, "Fixup")]
        [InlineData(InteractiveRebaseAction.Drop, "Drop")]
        public void ToName_AllActions_ReturnCorrectString(InteractiveRebaseAction action, string expected)
        {
            var result = InteractiveRebaseActionConverters.ToName.Convert(action, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        #endregion

        #region ToIconBrush

        [Fact]
        public void ToIconBrush_Pick_ReturnsGreen()
        {
            var result = InteractiveRebaseActionConverters.ToIconBrush.Convert(
                InteractiveRebaseAction.Pick, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Green, result);
        }

        [Fact]
        public void ToIconBrush_Edit_ReturnsOrange()
        {
            var result = InteractiveRebaseActionConverters.ToIconBrush.Convert(
                InteractiveRebaseAction.Edit, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Orange, result);
        }

        [Fact]
        public void ToIconBrush_Reword_ReturnsOrange()
        {
            var result = InteractiveRebaseActionConverters.ToIconBrush.Convert(
                InteractiveRebaseAction.Reword, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Orange, result);
        }

        [Fact]
        public void ToIconBrush_Squash_ReturnsLightGray()
        {
            var result = InteractiveRebaseActionConverters.ToIconBrush.Convert(
                InteractiveRebaseAction.Squash, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.LightGray, result);
        }

        [Fact]
        public void ToIconBrush_Fixup_ReturnsLightGray()
        {
            var result = InteractiveRebaseActionConverters.ToIconBrush.Convert(
                InteractiveRebaseAction.Fixup, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.LightGray, result);
        }

        [Fact]
        public void ToIconBrush_Drop_ReturnsRed()
        {
            var result = InteractiveRebaseActionConverters.ToIconBrush.Convert(
                InteractiveRebaseAction.Drop, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Red, result);
        }

        #endregion
    }
}
