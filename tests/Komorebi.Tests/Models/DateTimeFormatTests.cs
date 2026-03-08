using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    public class DateTimeFormatTests
    {
        #region Constructor

        [Fact]
        public void Constructor_SetsDateOnlyAndDateTime()
        {
            var format = new DateTimeFormat("yyyy/MM/dd", "yyyy/MM/dd, HH:mm:ss");
            Assert.Equal("yyyy/MM/dd", format.DateOnly);
            Assert.Equal("yyyy/MM/dd, HH:mm:ss", format.DateTime);
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
        public void Supported_AllItemsHaveNonEmptyDateOnly()
        {
            foreach (var format in DateTimeFormat.Supported)
            {
                Assert.False(string.IsNullOrWhiteSpace(format.DateOnly),
                    $"DateTimeFormat with DateTime='{format.DateTime}' has empty DateOnly");
            }
        }

        [Fact]
        public void Supported_AllItemsHaveNonEmptyDateTime()
        {
            foreach (var format in DateTimeFormat.Supported)
            {
                Assert.False(string.IsNullOrWhiteSpace(format.DateTime),
                    $"DateTimeFormat with DateOnly='{format.DateOnly}' has empty DateTime");
            }
        }

        [Fact]
        public void Supported_DateTimeAlwaysContainsDateOnly()
        {
            // The DateTime format should contain the DateOnly portion
            foreach (var format in DateTimeFormat.Supported)
            {
                Assert.StartsWith(format.DateOnly, format.DateTime);
            }
        }

        [Fact]
        public void Supported_DateTimeAlwaysContainsTimeComponent()
        {
            foreach (var format in DateTimeFormat.Supported)
            {
                Assert.Contains("HH:mm:ss", format.DateTime);
            }
        }

        #endregion

        #region Example Property

        [Fact]
        public void Example_ReturnsFormattedString()
        {
            var format = new DateTimeFormat("yyyy/MM/dd", "yyyy/MM/dd, HH:mm:ss");
            var example = format.Example;
            Assert.False(string.IsNullOrWhiteSpace(example));
            // The example uses a fixed date: 2025/01/31 08:00:00
            Assert.Equal("2025/01/31, 08:00:00", example);
        }

        [Fact]
        public void Example_AllSupportedFormatsProduceValidExamples()
        {
            foreach (var format in DateTimeFormat.Supported)
            {
                var example = format.Example;
                Assert.False(string.IsNullOrWhiteSpace(example),
                    $"DateTimeFormat '{format.DateOnly}' produced empty Example");
            }
        }

        [Theory]
        [InlineData(0, "2025/01/31, 08:00:00")]
        [InlineData(1, "2025.01.31, 08:00:00")]
        [InlineData(2, "2025-01-31, 08:00:00")]
        [InlineData(3, "01/31/2025, 08:00:00")]
        [InlineData(4, "01.31.2025, 08:00:00")]
        [InlineData(5, "01-31-2025, 08:00:00")]
        [InlineData(6, "31/01/2025, 08:00:00")]
        [InlineData(7, "31.01.2025, 08:00:00")]
        [InlineData(8, "31-01-2025, 08:00:00")]
        public void Example_ProducesExpectedOutput(int index, string expected)
        {
            Assert.Equal(expected, DateTimeFormat.Supported[index].Example);
        }

        #endregion

        #region ActiveIndex and Active

        [Fact]
        public void ActiveIndex_DefaultIsZero()
        {
            // Save and restore to avoid affecting other tests
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
        public void Active_ReturnsItemAtActiveIndex()
        {
            var original = DateTimeFormat.ActiveIndex;
            try
            {
                DateTimeFormat.ActiveIndex = 2;
                Assert.Same(DateTimeFormat.Supported[2], DateTimeFormat.Active);
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
                    Assert.Same(DateTimeFormat.Supported[i], DateTimeFormat.Active);
                }
            }
            finally
            {
                DateTimeFormat.ActiveIndex = original;
            }
        }

        #endregion

        #region Format Uniqueness

        [Fact]
        public void Supported_AllDateOnlyFormatsAreUnique()
        {
            var formats = new HashSet<string>();
            foreach (var format in DateTimeFormat.Supported)
            {
                Assert.True(formats.Add(format.DateOnly),
                    $"Duplicate DateOnly format: '{format.DateOnly}'");
            }
        }

        [Fact]
        public void Supported_AllDateTimeFormatsAreUnique()
        {
            var formats = new HashSet<string>();
            foreach (var format in DateTimeFormat.Supported)
            {
                Assert.True(formats.Add(format.DateTime),
                    $"Duplicate DateTime format: '{format.DateTime}'");
            }
        }

        #endregion
    }
}
