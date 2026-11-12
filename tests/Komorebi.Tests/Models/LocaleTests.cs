using Komorebi.Models;

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
            // Keys should follow xx_YY or xxx_YY format (language_COUNTRY) or
            // a short language-only code (e.g., "la", "sa") for languages without country variant
            foreach (var locale in Locale.Supported)
            {
                if (locale.Key.Contains('_'))
                {
                    var parts = locale.Key.Split('_');
                    Assert.Equal(2, parts.Length);
                    Assert.True(parts[0].Length >= 2 && parts[0].Length <= 3,
                        $"Language code '{parts[0]}' in key '{locale.Key}' should be 2-3 chars");
                    Assert.Equal(2, parts[1].Length);
                    Assert.Equal(parts[1], parts[1].ToUpperInvariant());
                }
                else
                {
                    // 言語コードのみ（la, sa等）
                    Assert.True(locale.Key.Length >= 2 && locale.Key.Length <= 3,
                        $"Language-only key '{locale.Key}' should be 2-3 chars");
                }
            }
        }

        #endregion

        #region Expected Locales

        [Theory]
        [InlineData("ca_ES", "Català")]
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
        [InlineData("sa", "\u0938\u0902\u0938\u094d\u0915\u0943\u0924\u092e\u094d (Sanskrit)")]
        [InlineData("la", "Latina (Latin)")]
        public void Supported_ContainsExpectedLocale(string expectedKey, string expectedName)
        {
            var locale = Locale.Supported.Find(l => l.Key == expectedKey);
            Assert.NotNull(locale);
            Assert.Equal(expectedName, locale.Name);
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
                "ja_JP", "ta_IN", "ko_KR", "la", "sa", "ca_ES"
            };

            var actualKeys = new HashSet<string>();
            foreach (var locale in Locale.Supported)
            {
                actualKeys.Add(locale.Key);
            }

            Assert.Equal(expectedKeys, actualKeys);
        }

        #endregion

        #region Placeholders

        /// <summary>
        /// 全ロケールで、キーごとの書式プレースホルダ（{0}, {1:N0} 等の番号）の集合が en_US と一致すること。
        /// 番号が欠けると App.Text の引数が表示されず、余分な番号は string.Format の FormatException になる。
        /// localization-check.js はキーの有無しか見ないため、ここで内容の整合を検査する。
        /// </summary>
        [Fact]
        public void AllLocales_PlaceholdersMatchEnglish()
        {
            var dir = FindLocalesDirectory();
            var english = LoadStrings(Path.Combine(dir, "en_US.axaml"));
            var mismatches = new List<string>();
            foreach (var file in Directory.GetFiles(dir, "*.axaml"))
            {
                if (Path.GetFileName(file) == "en_US.axaml")
                    continue;
                mismatches.AddRange(FindPlaceholderMismatches(english, LoadStrings(file))
                    .Select(m => $"{Path.GetFileName(file)}: {m}"));
            }

            Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
        }

        /// <summary>
        /// 検査そのものが不一致を検出できること（欠落・余分な番号を入れた一時コピーで失敗する）を確認する。
        /// </summary>
        [Fact]
        public void PlaceholderCheck_DetectsMismatchInModifiedCopy()
        {
            var dir = FindLocalesDirectory();
            var english = LoadStrings(Path.Combine(dir, "en_US.axaml"));
            var temp = Path.Combine(Path.GetTempPath(), $"komorebi-locale-{Guid.NewGuid():N}.axaml");
            try
            {
                var doc = System.Xml.Linq.XDocument.Load(Path.Combine(dir, "ja_JP.axaml"), System.Xml.Linq.LoadOptions.PreserveWhitespace);
                var x = System.Xml.Linq.XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
                var strings = doc.Root!.Elements().Where(e => e.Name.LocalName == "String").ToList();
                var withArg = strings.First(e => e.Value.Contains("{0}"));
                var withoutArg = strings.First(e => !e.Value.Contains('{'));
                withArg.Value = withArg.Value.Replace("{0}", string.Empty);   // 欠落
                withoutArg.Value += " {3}";                                       // 余分
                doc.Save(temp);

                var mismatches = FindPlaceholderMismatches(english, LoadStrings(temp)).ToList();
                Assert.Contains(mismatches, m => m.StartsWith((string)withArg.Attribute(x + "Key")! + ":", StringComparison.Ordinal));
                Assert.Contains(mismatches, m => m.StartsWith((string)withoutArg.Attribute(x + "Key")! + ":", StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(temp);
            }
        }

        /// <summary>英語にあるキーについて、番号集合が異なるものを列挙する（英語に無いキーは localization-check の担当）。</summary>
        private static IEnumerable<string> FindPlaceholderMismatches(Dictionary<string, string> english, Dictionary<string, string> locale)
        {
            foreach (var (key, value) in locale)
            {
                if (!english.TryGetValue(key, out var source))
                    continue;

                var expected = Placeholders(source);
                var actual = Placeholders(value);
                if (!expected.SetEquals(actual))
                    yield return $"{key}: en_US={{{string.Join(",", expected.Order())}}} locale={{{string.Join(",", actual.Order())}}}";
            }
        }

        /// <summary>{n} / {n,align} / {n:format} の番号を集める。</summary>
        private static HashSet<int> Placeholders(string text)
        {
            var set = new HashSet<int>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\{(\d+)(?:[,:][^}]*)?\}"))
                set.Add(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            return set;
        }

        /// <summary>ロケール XML の x:String をキー→値で読む（XML の実体参照は XDocument が解決する）。</summary>
        private static Dictionary<string, string> LoadStrings(string file)
        {
            var x = System.Xml.Linq.XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var doc = System.Xml.Linq.XDocument.Load(file);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var element in doc.Root!.Elements().Where(e => e.Name.LocalName == "String"))
            {
                var key = (string?)element.Attribute(x + "Key");
                if (key is not null)
                    result[key] = element.Value;
            }
            return result;
        }

        /// <summary>テスト実行ディレクトリから上位へたどってリポジトリの src/Resources/Locales を探す。</summary>
        private static string FindLocalesDirectory()
        {
            for (var dir = AppContext.BaseDirectory; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            {
                var candidate = Path.Combine(dir, "src", "Resources", "Locales");
                if (File.Exists(Path.Combine(candidate, "en_US.axaml")))
                    return candidate;
            }

            throw new DirectoryNotFoundException("src/Resources/Locales が見つかりません。");
        }

        #endregion
    }
}
