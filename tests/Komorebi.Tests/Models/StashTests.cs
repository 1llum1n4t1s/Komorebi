using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    public class StashTests
    {
        #region Default Values

        [Fact]
        public void NewStash_HasEmptyDefaults()
        {
            var stash = new Stash();
            Assert.Equal("", stash.Name);
            Assert.Equal("", stash.SHA);
            Assert.Empty(stash.Parents);
            Assert.Equal(0UL, stash.Time);
            Assert.Equal("", stash.Message);
        }

        #endregion

        #region Subject Property

        [Fact]
        public void Subject_SingleLineMessage_ReturnsEntireMessage()
        {
            var stash = new Stash { Message = "WIP: working on feature" };
            Assert.Equal("WIP: working on feature", stash.Subject);
        }

        [Fact]
        public void Subject_MultiLineMessage_ReturnsFirstLine()
        {
            var stash = new Stash { Message = "WIP: feature\nMore details here\nAnd more" };
            Assert.Equal("WIP: feature", stash.Subject);
        }

        [Fact]
        public void Subject_EmptyMessage_ReturnsEmpty()
        {
            var stash = new Stash { Message = "" };
            Assert.Equal("", stash.Subject);
        }

        [Fact]
        public void Subject_MessageWithLeadingWhitespace_TrimsFirstLine()
        {
            var stash = new Stash { Message = "  WIP  \nDetails" };
            Assert.Equal("WIP", stash.Subject);
        }

        [Fact]
        public void Subject_MessageWithOnlyNewline_ReturnsEmpty()
        {
            var stash = new Stash { Message = "\nSecond line" };
            Assert.Equal("", stash.Subject);
        }

        #endregion

        #region Time Property

        [Fact]
        public void Time_CanBeSetAndRetrieved()
        {
            var stash = new Stash { Time = 1736942400 };
            Assert.Equal(1736942400UL, stash.Time);
        }

        #endregion

        #region Parents

        [Fact]
        public void Parents_CanBeSet()
        {
            var stash = new Stash { Parents = new List<string> { "abc123", "def456" } };
            Assert.Equal(2, stash.Parents.Count);
            Assert.Equal("abc123", stash.Parents[0]);
        }

        #endregion
    }
}
