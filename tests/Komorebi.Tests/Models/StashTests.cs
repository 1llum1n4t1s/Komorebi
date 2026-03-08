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

        #region TimeStr Property

        [Fact]
        public void TimeStr_ZeroTime_ProducesValidString()
        {
            var stash = new Stash { Time = 0 };
            var timeStr = stash.TimeStr;
            Assert.False(string.IsNullOrWhiteSpace(timeStr));
        }

        [Fact]
        public void TimeStr_KnownTimestamp_ContainsExpectedDate()
        {
            // 2025-01-15 12:00:00 UTC = 1736942400
            var stash = new Stash { Time = 1736942400 };
            var timeStr = stash.TimeStr;
            // Should contain 2025 somewhere in the formatted string
            Assert.Contains("2025", timeStr);
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
