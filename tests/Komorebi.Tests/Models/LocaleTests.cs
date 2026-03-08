using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    public class LocaleTests
    {
        #region Constructor

        [Fact]
        public void Constructor_SetsNameAndKey()
        {
            var locale = new Locale("TestLang", "te_ST");
            Assert.Equal("TestLang", locale.Name);
            Assert.Equal("te_ST", locale.Key);
        }

        #endregion

        #region Supported List

        [Fact]
        public void Supported_IsNotNull()
        {
            Assert.NotNull(Locale.Supported);
        }

        [Fact]
        public void Supported_HasExpectedCount()
        {
            // 15 locales as documented: de_DE, en_US, es_ES, fr_FR, id_ID, fil_PH, it_IT,
            // pt_BR, uk_UA, ru_RU, zh_CN, zh_TW, ja_JP, ta_IN, ko_KR
            Assert.Equal(15, Locale.Supported.Count);
        }

        [Fact]
        public void Supported_AllItemsHaveNonEmptyName()
        {
            foreach (var locale in Locale.Supported)
            {
                Assert.False(string.IsNullOrWhiteSpace(locale.Name),
                    $"Locale with key '{locale.Key}' has empty Name");
            }
        }

        [Fact]
        public void Supported_AllItemsHaveNonEmptyKey()
        {
            foreach (var locale in Locale.Supported)
            {
                Assert.False(string.IsNullOrWhiteSpace(locale.Key),
                    $"Locale with name '{locale.Name}' has empty Key");
            }
        }

        [Fact]
        public void Supported_AllKeysAreUnique()
        {
            var keys = new HashSet<string>();
            foreach (var locale in Locale.Supported)
            {
                Assert.True(keys.Add(locale.Key),
                    $"Duplicate locale key: '{locale.Key}'");
            }
        }

        [Fact]
        public void Supported_AllNamesAreUnique()
        {
            var names = new HashSet<string>();
            foreach (var locale in Locale.Supported)
            {
                Assert.True(names.Add(locale.Name),
                    $"Duplicate locale name: '{locale.Name}'");
            }
        }

        #endregion

        #region Key Format Validation

        [Fact]
        public void Supported_AllKeysFollowLocaleFormat()
        {
            // Keys should follow xx_YY or xxx_YY format (language_COUNTRY)
            foreach (var locale in Locale.Supported)
            {
                Assert.Contains("_", locale.Key);
                var parts = locale.Key.Split('_');
                Assert.Equal(2, parts.Length);
                Assert.True(parts[0].Length >= 2 && parts[0].Length <= 3,
                    $"Language code '{parts[0]}' in key '{locale.Key}' should be 2-3 chars");
                Assert.Equal(2, parts[1].Length);
                Assert.Equal(parts[1], parts[1].ToUpperInvariant());
            }
        }

        #endregion

        #region Expected Locales

        [Theory]
        [InlineData("de_DE", "Deutsch")]
        [InlineData("en_US", "English")]
        [InlineData("es_ES", "Espa\u00f1ol")]
        [InlineData("fr_FR", "Fran\u00e7ais")]
        [InlineData("id_ID", "Bahasa Indonesia")]
        [InlineData("fil_PH", "Filipino (Tagalog)")]
        [InlineData("it_IT", "Italiano")]
        [InlineData("pt_BR", "Portugu\u00eas (Brasil)")]
        [InlineData("uk_UA", "\u0423\u043a\u0440\u0430\u0457\u043d\u0441\u044c\u043a\u0430")]
        [InlineData("ru_RU", "\u0420\u0443\u0441\u0441\u043a\u0438\u0439")]
        [InlineData("zh_CN", "\u7b80\u4f53\u4e2d\u6587")]
        [InlineData("zh_TW", "\u7e41\u9ad4\u4e2d\u6587")]
        [InlineData("ja_JP", "\u65e5\u672c\u8a9e")]
        [InlineData("ta_IN", "\u0ba4\u0bae\u0bbf\u0bb4\u0bcd (Tamil)")]
        [InlineData("ko_KR", "\ud55c\uad6d\uc5b4")]
        public void Supported_ContainsExpectedLocale(string expectedKey, string expectedName)
        {
            var locale = Locale.Supported.Find(l => l.Key == expectedKey);
            Assert.NotNull(locale);
            Assert.Equal(expectedName, locale.Name);
        }

        [Fact]
        public void Supported_ContainsEnglishLocale()
        {
            Assert.Contains(Locale.Supported, l => l.Key == "en_US");
        }

        #endregion

        #region Locale Files Correspondence

        [Fact]
        public void Supported_KeysMatchExpectedLocaleFiles()
        {
            // These are the locale files that should exist in Resources/Locales/
            var expectedKeys = new HashSet<string>
            {
                "de_DE", "en_US", "es_ES", "fr_FR", "id_ID", "fil_PH",
                "it_IT", "pt_BR", "uk_UA", "ru_RU", "zh_CN", "zh_TW",
                "ja_JP", "ta_IN", "ko_KR"
            };

            var actualKeys = new HashSet<string>();
            foreach (var locale in Locale.Supported)
            {
                actualKeys.Add(locale.Key);
            }

            Assert.Equal(expectedKeys, actualKeys);
        }

        #endregion
    }
}
