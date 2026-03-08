using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    public class BookmarksTests
    {
        #region Brushes Array

        [Fact]
        public void Brushes_IsNotNull()
        {
            Assert.NotNull(Bookmarks.Brushes);
        }

        [Fact]
        public void Brushes_HasExpectedLength()
        {
            Assert.Equal(8, Bookmarks.Brushes.Length);
        }

        [Fact]
        public void Brushes_FirstElementIsNull()
        {
            Assert.Null(Bookmarks.Brushes[0]);
        }

        [Fact]
        public void Brushes_NonFirstElements_AreNotNull()
        {
            for (int i = 1; i < Bookmarks.Brushes.Length; i++)
            {
                Assert.NotNull(Bookmarks.Brushes[i]);
            }
        }

        #endregion

        #region Get Method

        [Fact]
        public void Get_ValidIndex_ReturnsBrush()
        {
            for (int i = 0; i < Bookmarks.Brushes.Length; i++)
            {
                var result = Bookmarks.Get(i);
                Assert.Equal(Bookmarks.Brushes[i], result);
            }
        }

        [Fact]
        public void Get_NegativeIndex_ReturnsNull()
        {
            Assert.Null(Bookmarks.Get(-1));
        }

        [Fact]
        public void Get_IndexEqualToLength_ReturnsNull()
        {
            Assert.Null(Bookmarks.Get(Bookmarks.Brushes.Length));
        }

        [Fact]
        public void Get_IndexGreaterThanLength_ReturnsNull()
        {
            Assert.Null(Bookmarks.Get(100));
        }

        [Fact]
        public void Get_IndexZero_ReturnsNull()
        {
            // Index 0 is null by design (no bookmark)
            Assert.Null(Bookmarks.Get(0));
        }

        [Fact]
        public void Get_Index1_ReturnsRed()
        {
            var result = Bookmarks.Get(1);
            Assert.Same(Avalonia.Media.Brushes.Red, result);
        }

        [Fact]
        public void Get_Index2_ReturnsOrange()
        {
            var result = Bookmarks.Get(2);
            Assert.Same(Avalonia.Media.Brushes.Orange, result);
        }

        #endregion
    }
}
