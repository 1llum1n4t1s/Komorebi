using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class BookmarksTests
    {
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

    }
}
