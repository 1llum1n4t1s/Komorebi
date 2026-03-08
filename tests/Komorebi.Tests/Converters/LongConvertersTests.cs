using System.Globalization;

using Komorebi.Converters;

namespace Komorebi.Tests.Converters
{
    public class LongConvertersTests
    {
        #region ToFileSize - Bytes

        [Theory]
        [InlineData(0L, "0 B")]
        [InlineData(1L, "1 B")]
        [InlineData(512L, "512 B")]
        [InlineData(1023L, "1,023 B")]
        public void ToFileSize_LessThan1KB_ReturnsBytesFormat(long input, string expected)
        {
            var result = LongConverters.ToFileSize.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        #endregion

        #region ToFileSize - KB

        [Fact]
        public void ToFileSize_Exactly1KB_ReturnsKBFormat()
        {
            var result = LongConverters.ToFileSize.Convert(1024L, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.NotNull(result);
            var str = (string)result;
            Assert.Contains("KB", str);
            Assert.Contains("1,024", str);
        }

        [Fact]
        public void ToFileSize_1500Bytes_ReturnsKBFormat()
        {
            var result = LongConverters.ToFileSize.Convert(1500L, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.NotNull(result);
            var str = (string)result;
            Assert.Contains("KB", str);
            Assert.Contains("1,500", str);
        }

        #endregion

        #region ToFileSize - MB

        [Fact]
        public void ToFileSize_Exactly1MB_ReturnsMBFormat()
        {
            var result = LongConverters.ToFileSize.Convert(1024L * 1024L, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.NotNull(result);
            var str = (string)result;
            Assert.Contains("MB", str);
        }

        [Fact]
        public void ToFileSize_5MB_ReturnsMBFormat()
        {
            var bytes = 5L * 1024L * 1024L;
            var result = LongConverters.ToFileSize.Convert(bytes, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.NotNull(result);
            var str = (string)result;
            Assert.Contains("MB", str);
        }

        #endregion

        #region ToFileSize - GB

        [Fact]
        public void ToFileSize_Exactly1GB_ReturnsGBFormat()
        {
            var result = LongConverters.ToFileSize.Convert(1024L * 1024L * 1024L, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.NotNull(result);
            var str = (string)result;
            Assert.Contains("GB", str);
        }

        [Fact]
        public void ToFileSize_2Point5GB_ReturnsGBFormat()
        {
            var bytes = (long)(2.5 * 1024 * 1024 * 1024);
            var result = LongConverters.ToFileSize.Convert(bytes, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.NotNull(result);
            var str = (string)result;
            Assert.Contains("GB", str);
        }

        #endregion

        #region ToFileSize - Boundary Values

        [Fact]
        public void ToFileSize_1023Bytes_ShowsBytes()
        {
            var result = Assert.IsType<string>(LongConverters.ToFileSize.Convert(1023L, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Contains("B", result);
            Assert.DoesNotContain("KB", result);
        }

        [Fact]
        public void ToFileSize_1024Bytes_ShowsKB()
        {
            var result = Assert.IsType<string>(LongConverters.ToFileSize.Convert(1024L, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Contains("KB", result);
        }

        [Fact]
        public void ToFileSize_OneBelowMB_ShowsKB()
        {
            var result = Assert.IsType<string>(LongConverters.ToFileSize.Convert(1024L * 1024L - 1, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Contains("KB", result);
        }

        [Fact]
        public void ToFileSize_ExactlyMB_ShowsMB()
        {
            var result = Assert.IsType<string>(LongConverters.ToFileSize.Convert(1024L * 1024L, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Contains("MB", result);
        }

        [Fact]
        public void ToFileSize_OneBelowGB_ShowsMB()
        {
            var result = Assert.IsType<string>(LongConverters.ToFileSize.Convert(1024L * 1024L * 1024L - 1, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Contains("MB", result);
        }

        [Fact]
        public void ToFileSize_ExactlyGB_ShowsGB()
        {
            var result = Assert.IsType<string>(LongConverters.ToFileSize.Convert(1024L * 1024L * 1024L, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Contains("GB", result);
        }

        #endregion
    }
}
