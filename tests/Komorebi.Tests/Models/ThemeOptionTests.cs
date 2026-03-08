using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    public class ThemeOptionTests
    {
        #region Constructor

        [Fact]
        public void Constructor_SetsNameAndKey()
        {
            var option = new ThemeOption("Test Theme", "TestKey");
            Assert.Equal("Test Theme", option.Name);
            Assert.Equal("TestKey", option.Key);
        }

        #endregion

        #region Supported List

        [Fact]
        public void Supported_IsNotNull()
        {
            Assert.NotNull(ThemeOption.Supported);
        }

        [Fact]
        public void Supported_HasExpectedCount()
        {
            Assert.Equal(5, ThemeOption.Supported.Count);
        }

        [Fact]
        public void Supported_AllItemsHaveNonEmptyName()
        {
            foreach (var option in ThemeOption.Supported)
            {
                Assert.False(string.IsNullOrWhiteSpace(option.Name),
                    $"ThemeOption with key '{option.Key}' has empty Name");
            }
        }

        [Fact]
        public void Supported_AllItemsHaveNonEmptyKey()
        {
            foreach (var option in ThemeOption.Supported)
            {
                Assert.False(string.IsNullOrWhiteSpace(option.Key),
                    $"ThemeOption with name '{option.Name}' has empty Key");
            }
        }

        [Fact]
        public void Supported_AllKeysAreUnique()
        {
            var keys = new HashSet<string>();
            foreach (var option in ThemeOption.Supported)
            {
                Assert.True(keys.Add(option.Key),
                    $"Duplicate key found: '{option.Key}'");
            }
        }

        [Fact]
        public void Supported_AllNamesAreUnique()
        {
            var names = new HashSet<string>();
            foreach (var option in ThemeOption.Supported)
            {
                Assert.True(names.Add(option.Name),
                    $"Duplicate name found: '{option.Name}'");
            }
        }

        #endregion

        #region Constants Match Supported List

        [Fact]
        public void DefaultKey_ExistsInSupportedList()
        {
            Assert.Contains(ThemeOption.Supported,
                t => t.Key == ThemeOption.DefaultKey);
        }

        [Fact]
        public void DarkKey_ExistsInSupportedList()
        {
            Assert.Contains(ThemeOption.Supported,
                t => t.Key == ThemeOption.DarkKey);
        }

        [Fact]
        public void LightKey_ExistsInSupportedList()
        {
            Assert.Contains(ThemeOption.Supported,
                t => t.Key == ThemeOption.LightKey);
        }

        [Fact]
        public void ActiproLightKey_ExistsInSupportedList()
        {
            Assert.Contains(ThemeOption.Supported,
                t => t.Key == ThemeOption.ActiproLightKey);
        }

        [Fact]
        public void ActiproDarkKey_ExistsInSupportedList()
        {
            Assert.Contains(ThemeOption.Supported,
                t => t.Key == ThemeOption.ActiproDarkKey);
        }

        [Fact]
        public void AllConstantKeys_CoverAllSupportedEntries()
        {
            var constantKeys = new HashSet<string>
            {
                ThemeOption.DefaultKey,
                ThemeOption.DarkKey,
                ThemeOption.LightKey,
                ThemeOption.ActiproLightKey,
                ThemeOption.ActiproDarkKey,
            };

            foreach (var option in ThemeOption.Supported)
            {
                Assert.Contains(option.Key, constantKeys);
            }
        }

        #endregion

        #region Constant Values

        [Fact]
        public void DefaultKey_HasExpectedValue()
        {
            Assert.Equal("Default", ThemeOption.DefaultKey);
        }

        [Fact]
        public void DarkKey_HasExpectedValue()
        {
            Assert.Equal("Dark", ThemeOption.DarkKey);
        }

        [Fact]
        public void LightKey_HasExpectedValue()
        {
            Assert.Equal("Light", ThemeOption.LightKey);
        }

        [Fact]
        public void ActiproLightKey_HasExpectedValue()
        {
            Assert.Equal("ActiproLight", ThemeOption.ActiproLightKey);
        }

        [Fact]
        public void ActiproDarkKey_HasExpectedValue()
        {
            Assert.Equal("ActiproDark", ThemeOption.ActiproDarkKey);
        }

        #endregion

        #region Supported List Order

        [Fact]
        public void Supported_DefaultIsFirstEntry()
        {
            Assert.Equal(ThemeOption.DefaultKey, ThemeOption.Supported[0].Key);
        }

        #endregion
    }
}
