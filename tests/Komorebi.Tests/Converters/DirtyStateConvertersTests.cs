using System.Globalization;

using Avalonia.Media;

using Komorebi.Converters;
using Komorebi.Models;

namespace Komorebi.Tests.Converters
{
    public class DirtyStateConvertersTests
    {
        #region ToBrush

        [Fact]
        public void ToBrush_HasLocalChanges_ReturnsGray()
        {
            var result = DirtyStateConverters.ToBrush.Convert(
                DirtyState.HasLocalChanges, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Gray, result);
        }

        [Fact]
        public void ToBrush_HasPendingPullOrPush_ReturnsRoyalBlue()
        {
            var result = DirtyStateConverters.ToBrush.Convert(
                DirtyState.HasPendingPullOrPush, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.RoyalBlue, result);
        }

        [Fact]
        public void ToBrush_None_ReturnsTransparent()
        {
            var result = DirtyStateConverters.ToBrush.Convert(
                DirtyState.None, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Transparent, result);
        }

        [Fact]
        public void ToBrush_BothFlags_HasLocalChangesTakesPriority()
        {
            // HasLocalChanges is checked first, so when both flags are set, Gray should be returned
            var combined = DirtyState.HasLocalChanges | DirtyState.HasPendingPullOrPush;
            var result = DirtyStateConverters.ToBrush.Convert(
                combined, typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Gray, result);
        }

        #endregion

        // Note: ToDesc is skipped because it depends on App.Text() which requires app initialization.
    }
}
