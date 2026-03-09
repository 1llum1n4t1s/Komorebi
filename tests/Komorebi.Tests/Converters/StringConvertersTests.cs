using System;
using System.Globalization;

using Avalonia.Styling;

using Komorebi.Converters;
using Komorebi.Models;

namespace Komorebi.Tests.Converters
{
    public class StringConvertersTests
    {
        #region ToShortSHA

        [Fact]
        public void ToShortSHA_NullInput_ReturnsEmptyString()
        {
            var result = StringConverters.ToShortSHA.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ToShortSHA_EmptyString_ReturnsEmptyString()
        {
            var result = StringConverters.ToShortSHA.Convert(string.Empty, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("abc", "abc")]
        [InlineData("1234567890", "1234567890")]
        public void ToShortSHA_ShortString_LessThanOrEqual10Chars_ReturnsSameString(string input, string expected)
        {
            var result = StringConverters.ToShortSHA.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("12345678901", "1234567890")]
        [InlineData("abc123def456abc123def456abc123def456abcd", "abc123def4")]
        public void ToShortSHA_LongString_MoreThan10Chars_ReturnsTruncatedTo10(string input, string expected)
        {
            var result = StringConverters.ToShortSHA.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToShortSHA_Exactly10Chars_ReturnsSameString()
        {
            var result = StringConverters.ToShortSHA.Convert("1234567890", typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("1234567890", result);
        }

        [Fact]
        public void ToShortSHA_NonStringInput_ReturnsUnsetValue()
        {
            var result = StringConverters.ToShortSHA.Convert(12345, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(Avalonia.AvaloniaProperty.UnsetValue, result);
        }

        #endregion

        #region TrimRefsPrefix

        [Fact]
        public void TrimRefsPrefix_NullInput_ReturnsEmptyString()
        {
            var result = StringConverters.TrimRefsPrefix.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void TrimRefsPrefix_EmptyString_ReturnsSameString()
        {
            var result = StringConverters.TrimRefsPrefix.Convert(string.Empty, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void TrimRefsPrefix_RefsHeads_TrimsProperly()
        {
            var result = StringConverters.TrimRefsPrefix.Convert("refs/heads/main", typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("main", result);
        }

        [Fact]
        public void TrimRefsPrefix_RefsHeads_NestedBranch_TrimsProperly()
        {
            var result = StringConverters.TrimRefsPrefix.Convert("refs/heads/feature/my-feature", typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("feature/my-feature", result);
        }

        [Fact]
        public void TrimRefsPrefix_RefsRemotes_TrimsProperly()
        {
            var result = StringConverters.TrimRefsPrefix.Convert("refs/remotes/origin/main", typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("origin/main", result);
        }

        [Fact]
        public void TrimRefsPrefix_RefsRemotes_NestedBranch_TrimsProperly()
        {
            var result = StringConverters.TrimRefsPrefix.Convert("refs/remotes/origin/feature/deep", typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("origin/feature/deep", result);
        }

        [Theory]
        [InlineData("some/other/ref")]
        [InlineData("refs/tags/v1.0")]
        [InlineData("just-a-string")]
        public void TrimRefsPrefix_NoMatchingPrefix_ReturnsSameString(string input)
        {
            var result = StringConverters.TrimRefsPrefix.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(input, result);
        }

        [Fact]
        public void TrimRefsPrefix_NonStringInput_ReturnsUnsetValue()
        {
            var result = StringConverters.TrimRefsPrefix.Convert(42, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(Avalonia.AvaloniaProperty.UnsetValue, result);
        }

        #endregion

        #region ContainsSpaces

        [Fact]
        public void ContainsSpaces_NullInput_ReturnsFalse()
        {
            var result = StringConverters.ContainsSpaces.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        [Theory]
        [InlineData("hello world", true)]
        [InlineData(" ", true)]
        [InlineData("  leading", true)]
        [InlineData("trailing ", true)]
        public void ContainsSpaces_WithSpaces_ReturnsTrue(string input, bool expected)
        {
            var result = StringConverters.ContainsSpaces.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("nospaces", false)]
        [InlineData("", false)]
        public void ContainsSpaces_WithoutSpaces_ReturnsFalse(string input, bool expected)
        {
            var result = StringConverters.ContainsSpaces.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ContainsSpaces_NonStringInput_ReturnsUnsetValue()
        {
            var result = StringConverters.ContainsSpaces.Convert(42, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(Avalonia.AvaloniaProperty.UnsetValue, result);
        }

        #endregion

        #region IsNotNullOrWhitespace

        [Fact]
        public void IsNotNullOrWhitespace_NullInput_ReturnsFalse()
        {
            var result = StringConverters.IsNotNullOrWhitespace.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(false, result);
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("\t", false)]
        [InlineData("\n", false)]
        public void IsNotNullOrWhitespace_WhitespaceOnly_ReturnsFalse(string input, bool expected)
        {
            var result = StringConverters.IsNotNullOrWhitespace.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("hello", true)]
        [InlineData(" hello ", true)]
        [InlineData("a", true)]
        public void IsNotNullOrWhitespace_NonWhitespace_ReturnsTrue(string input, bool expected)
        {
            var result = StringConverters.IsNotNullOrWhitespace.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsNotNullOrWhitespace_NonStringInput_ReturnsUnsetValue()
        {
            var result = StringConverters.IsNotNullOrWhitespace.Convert(42, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(Avalonia.AvaloniaProperty.UnsetValue, result);
        }

        #endregion

        #region ToFriendlyUpstream

        [Fact]
        public void ToFriendlyUpstream_NullInput_ReturnsEmptyString()
        {
            var result = StringConverters.ToFriendlyUpstream.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("short", "")]
        [InlineData("1234567890123", "")]
        public void ToFriendlyUpstream_ShortString_13CharsOrLess_ReturnsEmptyString(string input, string expected)
        {
            var result = StringConverters.ToFriendlyUpstream.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToFriendlyUpstream_Exactly14Chars_ReturnsCharAfter13()
        {
            // "refs/remotes/" is 13 chars, so a typical upstream like "refs/remotes/origin/main"
            var result = StringConverters.ToFriendlyUpstream.Convert("refs/remotes/origin/main", typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("origin/main", result);
        }

        [Fact]
        public void ToFriendlyUpstream_TypicalUpstream_StripsPrefix()
        {
            var result = StringConverters.ToFriendlyUpstream.Convert("refs/remotes/upstream/develop", typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("upstream/develop", result);
        }

        [Fact]
        public void ToFriendlyUpstream_NonStringInput_ReturnsUnsetValue()
        {
            var result = StringConverters.ToFriendlyUpstream.Convert(42, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(Avalonia.AvaloniaProperty.UnsetValue, result);
        }

        #endregion

        #region ToLocale

        [Fact]
        public void ToLocale_Convert_ValidKey_ReturnsMatchingLocale()
        {
            var converter = StringConverters.ToLocale;
            var result = converter.Convert("en_US", typeof(Locale), null, CultureInfo.InvariantCulture);
            Assert.NotNull(result);
            Assert.IsType<Locale>(result);
            Assert.Equal("en_US", ((Locale)result).Key);
            Assert.Equal("English", ((Locale)result).Name);
        }

        [Fact]
        public void ToLocale_Convert_InvalidKey_ReturnsNull()
        {
            var converter = StringConverters.ToLocale;
            var result = converter.Convert("nonexistent", typeof(Locale), null, CultureInfo.InvariantCulture);
            Assert.Null(result);
        }

        [Fact]
        public void ToLocale_Convert_NullValue_ReturnsNull()
        {
            var converter = StringConverters.ToLocale;
            var result = converter.Convert(null, typeof(Locale), null, CultureInfo.InvariantCulture);
            Assert.Null(result);
        }

        [Theory]
        [InlineData("de_DE", "Deutsch")]
        [InlineData("ja_JP", "\u65e5\u672c\u8a9e")]
        [InlineData("zh_CN", "\u7b80\u4f53\u4e2d\u6587")]
        [InlineData("fil_PH", "Filipino (Tagalog)")]
        public void ToLocale_Convert_AllSupportedKeys_ReturnCorrectName(string key, string expectedName)
        {
            var converter = StringConverters.ToLocale;
            var result = converter.Convert(key, typeof(Locale), null, CultureInfo.InvariantCulture);
            Assert.NotNull(result);
            Assert.Equal(expectedName, ((Locale)result).Name);
        }

        [Fact]
        public void ToLocale_ConvertBack_ValidLocale_ReturnsKey()
        {
            var converter = StringConverters.ToLocale;
            var locale = new Locale("English", "en_US");
            var result = converter.ConvertBack(locale, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("en_US", result);
        }

        [Fact]
        public void ToLocale_ConvertBack_NullValue_ReturnsNull()
        {
            var converter = StringConverters.ToLocale;
            var result = converter.ConvertBack(null, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Null(result);
        }

        [Fact]
        public void ToLocale_ConvertBack_NonLocaleType_ReturnsNull()
        {
            var converter = StringConverters.ToLocale;
            var result = converter.ConvertBack("not a locale", typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Null(result);
        }

        #endregion

        #region ToTheme

        [Fact]
        public void ToTheme_Convert_Dark_ReturnsDarkVariant()
        {
            var converter = StringConverters.ToTheme;
            var result = converter.Convert("Dark", typeof(ThemeVariant), null, CultureInfo.InvariantCulture);
            Assert.Equal(ThemeVariant.Dark, result);
        }

        [Fact]
        public void ToTheme_Convert_Light_ReturnsLightVariant()
        {
            var converter = StringConverters.ToTheme;
            var result = converter.Convert("Light", typeof(ThemeVariant), null, CultureInfo.InvariantCulture);
            Assert.Equal(ThemeVariant.Light, result);
        }

        [Fact]
        public void ToTheme_Convert_NullValue_ReturnsDefault()
        {
            var converter = StringConverters.ToTheme;
            var result = converter.Convert(null, typeof(ThemeVariant), null, CultureInfo.InvariantCulture);
            Assert.Equal(ThemeVariant.Default, result);
        }

        [Fact]
        public void ToTheme_Convert_EmptyValue_ReturnsDefault()
        {
            var converter = StringConverters.ToTheme;
            var result = converter.Convert("", typeof(ThemeVariant), null, CultureInfo.InvariantCulture);
            Assert.Equal(ThemeVariant.Default, result);
        }

        [Fact]
        public void ToTheme_Convert_UnknownValue_ReturnsDefault()
        {
            var converter = StringConverters.ToTheme;
            var result = converter.Convert("nonexistent", typeof(ThemeVariant), null, CultureInfo.InvariantCulture);
            Assert.Equal(ThemeVariant.Default, result);
        }

        [Fact]
        public void ToTheme_ConvertBack_DarkVariant_ReturnsDark()
        {
            var converter = StringConverters.ToTheme;
            var result = converter.ConvertBack(ThemeVariant.Dark, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("Dark", result);
        }

        [Fact]
        public void ToTheme_ConvertBack_NullValue_ReturnsNull()
        {
            var converter = StringConverters.ToTheme;
            var result = converter.ConvertBack(null, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Null(result);
        }

        #endregion

        #region FormatByResourceKey

        [Fact]
        public void FormatByResourceKey_ConvertBack_ThrowsNotImplementedException()
        {
            var converter = StringConverters.FormatByResourceKey;
            Assert.Throws<NotImplementedException>(() =>
                converter.ConvertBack("value", typeof(string), null, CultureInfo.InvariantCulture));
        }

        #endregion
    }
}
