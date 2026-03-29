using System.Globalization;

using Komorebi.Converters;

namespace Komorebi.Tests.Converters
{
    public class PathConvertersTests
    {
        #region PureFileName

        [Fact]
        public void PureFileName_NullInput_ReturnsEmptyString()
        {
            var result = PathConverters.PureFileName.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("", result);
        }

        [Fact]
        public void PureFileName_EmptyString_ReturnsEmptyString()
        {
            var result = PathConverters.PureFileName.Convert(string.Empty, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("file.txt", "file.txt")]
        [InlineData("document.pdf", "document.pdf")]
        public void PureFileName_JustFileName_ReturnsSame(string input, string expected)
        {
            var result = PathConverters.PureFileName.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void PureFileName_WithDirectory_ReturnsOnlyFileName()
        {
            // Use Path.Combine to be OS-independent
            var path = Path.Combine("some", "directory", "file.txt");
            var result = PathConverters.PureFileName.Convert(path, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("file.txt", result);
        }

        [Fact]
        public void PureFileName_UnixStylePath_ReturnsOnlyFileName()
        {
            var result = PathConverters.PureFileName.Convert("/home/user/file.txt", typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("file.txt", result);
        }

        [Fact]
        public void PureFileName_NoExtension_ReturnsFileName()
        {
            var path = Path.Combine("dir", "Makefile");
            var result = PathConverters.PureFileName.Convert(path, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("Makefile", result);
        }

        #endregion

        #region PureDirectoryName

        [Fact]
        public void PureDirectoryName_NullInput_ReturnsEmptyString()
        {
            var result = PathConverters.PureDirectoryName.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("", result);
        }

        [Fact]
        public void PureDirectoryName_EmptyString_ReturnsEmptyString()
        {
            var result = PathConverters.PureDirectoryName.Convert(string.Empty, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void PureDirectoryName_JustFileName_ReturnsEmptyString()
        {
            var result = PathConverters.PureDirectoryName.Convert("file.txt", typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void PureDirectoryName_WithDirectory_ReturnsDirectoryOnly()
        {
            var path = Path.Combine("some", "directory", "file.txt");
            var result = PathConverters.PureDirectoryName.Convert(path, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(Path.Combine("some", "directory"), result);
        }

        #endregion

        // Note: RelativeToHome is skipped because it depends on Native.OS which
        // may behave differently across platforms and requires native interop.
    }
}
