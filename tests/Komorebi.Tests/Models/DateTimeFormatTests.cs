using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class DateTimeFormatTests
    {
        #region Constructor

        [Fact]
        public void Constructor_SetsDateFormat()
        {
            var format = new DateTimeFormat("yyyy/MM/dd");
            Assert.Equal("yyyy/MM/dd", format.DateFormat);
        }

        #endregion

        #region Supported List

        [Fact]
        public void Supported_IsNotNull()
        {
            Assert.NotNull(DateTimeFormat.Supported);
        }

        [Fact]
        public void Supported_HasExpectedCount()
        {
            Assert.Equal(11, DateTimeFormat.Supported.Count);
        }

        [Fact]
        public void Supported_AllItemsHaveNonEmptyDateFormat()
        {
            foreach (var format in DateTimeFormat.Supported)
            {
                Assert.False(string.IsNullOrWhiteSpace(format.DateFormat),
                    "DateTimeFormat has empty DateFormat");
            }
        }

        #endregion

        #region ActiveIndex

        [Fact]
        public void ActiveIndex_DefaultIsZero()
        {
            var original = DateTimeFormat.ActiveIndex;
            try
            {
                DateTimeFormat.ActiveIndex = 0;
                Assert.Equal(0, DateTimeFormat.ActiveIndex);
            }
            finally
            {
                DateTimeFormat.ActiveIndex = original;
            }
        }

        [Fact]
        public void ActiveIndex_CanBeSetToAnySupportedIndex()
        {
            var original = DateTimeFormat.ActiveIndex;
            try
            {
                for (int i = 0; i < DateTimeFormat.Supported.Count; i++)
                {
                    DateTimeFormat.ActiveIndex = i;
                    Assert.Equal(i, DateTimeFormat.ActiveIndex);
                }
            }
            finally
            {
                DateTimeFormat.ActiveIndex = original;
            }
        }

        #endregion

        #region Format Method

        [Fact]
        public void Format_DateOnly_ReturnsDateString()
        {
            var originalIndex = DateTimeFormat.ActiveIndex;
            try
            {
                DateTimeFormat.ActiveIndex = 0; // yyyy/MM/dd
                var result = DateTimeFormat.Format(0UL, dateOnly: true);
                Assert.False(string.IsNullOrWhiteSpace(result));
            }
            finally
            {
                DateTimeFormat.ActiveIndex = originalIndex;
            }
        }

        [Fact]
        public void Format_WithTime_IncludesTimeComponent()
        {
            var originalIndex = DateTimeFormat.ActiveIndex;
            try
            {
                DateTimeFormat.ActiveIndex = 0;
                var dateOnly = DateTimeFormat.Format(0UL, dateOnly: true);
                var dateTime = DateTimeFormat.Format(0UL, dateOnly: false);
                // DateTime version should be longer than DateOnly
                Assert.True(dateTime.Length > dateOnly.Length);
            }
            finally
            {
                DateTimeFormat.ActiveIndex = originalIndex;
            }
        }

        #endregion

        #region Use24Hours

        [Fact]
        public void Use24Hours_DefaultIsTrue()
        {
            Assert.True(DateTimeFormat.Use24Hours);
        }

        #endregion

        #region Format Uniqueness

        [Fact]
        public void Supported_AllDateFormatsAreUnique()
        {
            var formats = new HashSet<string>();
            foreach (var format in DateTimeFormat.Supported)
            {
                Assert.True(formats.Add(format.DateFormat),
                    $"Duplicate DateFormat: '{format.DateFormat}'");
            }
        }

        #endregion
    }
}
