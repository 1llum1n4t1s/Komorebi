namespace Komorebi.Tests.Models
{
    public class LocaleTests
    {
        #region Constructor

        #endregion

        #region Supported List

        #endregion

        #region Key Format Validation

        #endregion

        #region Expected Locales

        #endregion

        #region Locale Files Correspondence

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
