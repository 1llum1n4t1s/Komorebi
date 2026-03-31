using System.Reflection;

namespace Komorebi.Tests.Commands
{
    /// <summary>
    /// Config クラスに対する嫌がらせテスト。
    /// ParseConfigOutput のパース境界値、エンコーディング攻撃、巨大入力を検証する。
    /// </summary>
    public class ConfigAdversarialTests
    {
        /// <summary>
        /// privateメソッド ParseConfigOutput をリフレクション経由で呼び出すヘルパー。
        /// </summary>
        private static Dictionary<string, string> InvokeParseConfigOutput(
            string stdout,
            IEqualityComparer<string>? comparer = null)
        {
            var method = typeof(Komorebi.Commands.Config)
                .GetMethod("ParseConfigOutput", BindingFlags.NonPublic | BindingFlags.Static)!;

            return (Dictionary<string, string>)method.Invoke(null, [stdout, comparer])!;
        }

        // ===============================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ===============================================================

        /// <adversarial category="boundary" severity="critical"
        ///   description="空文字列を渡した場合に空辞書が返ること"
        ///   expected="空のDictionary" />
        [Fact]
        public void ParseConfigOutput_EmptyString_ReturnsEmptyDict()
        {
            var result = InvokeParseConfigOutput("");
            Assert.Empty(result);
        }

        /// <adversarial category="boundary" severity="critical"
        ///   description="改行のみの入力でクラッシュしないこと"
        ///   expected="空のDictionary" />
        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        [InlineData("\n\n\n")]
        [InlineData("\r\n\r\n")]
        public void ParseConfigOutput_OnlyNewlines_ReturnsEmptyDict(string input)
        {
            var result = InvokeParseConfigOutput(input);
            Assert.Empty(result);
        }

        /// <adversarial category="boundary" severity="high"
        ///   description="=が含まれない行は無視されること"
        ///   expected="=なし行はスキップ" />
        [Theory]
        [InlineData("noequalsign")]
        [InlineData("just a plain line")]
        [InlineData("core.autocrlf")]
        public void ParseConfigOutput_LineWithoutEquals_IsIgnored(string line)
        {
            var result = InvokeParseConfigOutput(line);
            Assert.Empty(result);
        }

        /// <adversarial category="boundary" severity="high"
        ///   description="=が複数ある行で最初の=だけで分割されること（Split('=', 2)の検証）"
        ///   expected="値部分に=が含まれる" />
        [Fact]
        public void ParseConfigOutput_MultipleEquals_SplitsOnFirstOnly()
        {
            // git configの値にURLなどが含まれるケース: key=https://example.com?a=b
            var input = "remote.origin.url=https://example.com?a=b&c=d";
            var result = InvokeParseConfigOutput(input);

            Assert.Single(result);
            Assert.Equal("https://example.com?a=b&c=d", result["remote.origin.url"]);
        }

        /// <adversarial category="boundary" severity="high"
        ///   description="キーが空で値がある行（=value形式）が辞書に入ること"
        ///   expected="空文字キーとして格納される" />
        [Fact]
        public void ParseConfigOutput_EmptyKey_StoresWithEmptyKey()
        {
            var input = "=somevalue";
            var result = InvokeParseConfigOutput(input);

            Assert.Single(result);
            Assert.True(result.ContainsKey(""));
            Assert.Equal("somevalue", result[""]);
        }

        /// <adversarial category="boundary" severity="medium"
        ///   description="値が空の行（key=形式）が空文字値として格納されること"
        ///   expected="空文字値として格納" />
        [Fact]
        public void ParseConfigOutput_EmptyValue_StoresEmptyString()
        {
            var input = "core.editor=";
            var result = InvokeParseConfigOutput(input);

            Assert.Single(result);
            Assert.Equal("", result["core.editor"]);
        }

        /// <adversarial category="boundary" severity="critical"
        ///   description="100KB超の巨大出力でタイムアウトしないこと"
        ///   expected="1秒以内に完了" />
        [Fact]
        public void ParseConfigOutput_HugeOutput_CompletesInTime()
        {
            // 100KB超のgit config出力をシミュレート
            var lines = new System.Text.StringBuilder();
            for (var i = 0; i < 5000; i++)
            {
                lines.AppendLine($"section{i}.key{i}=value{i}_with_some_padding_data_to_increase_size");
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = InvokeParseConfigOutput(lines.ToString());
            sw.Stop();

            Assert.Equal(5000, result.Count);
            Assert.True(sw.ElapsedMilliseconds < 1000, $"100KB超のパースに{sw.ElapsedMilliseconds}msかかった");
        }

        // ===============================================================
        // 🎭 型・エンコーディング攻撃（Type Confusion）
        // ===============================================================

        /// <adversarial category="type" severity="high"
        ///   description="Unicode文字を含むキーと値が正しくパースされること"
        ///   expected="Unicode値がそのまま保持される" />
        [Fact]
        public void ParseConfigOutput_UnicodeKeysAndValues_ParsedCorrectly()
        {
            var input = "user.name=太郎🎉\nuser.email=taro@例え.jp";
            var result = InvokeParseConfigOutput(input);

            Assert.Equal(2, result.Count);
            Assert.Equal("太郎🎉", result["user.name"]);
            Assert.Equal("taro@例え.jp", result["user.email"]);
        }

        /// <adversarial category="type" severity="medium"
        ///   description="バイナリ風コンテンツ（制御文字入り）で例外が出ないこと"
        ///   expected="例外なしで辞書が返る" />
        [Fact]
        public void ParseConfigOutput_BinaryContent_DoesNotThrow()
        {
            // 制御文字やヌルバイトを含むstdout
            var input = "key1=val\x00ue\nkey2=\x01\x02\x03=data";
            var ex = Record.Exception(() => InvokeParseConfigOutput(input));
            Assert.Null(ex);
        }

        // ===============================================================
        // 🌪️ カオス入力（Chaos Engineering）
        // ===============================================================

        /// <adversarial category="chaos" severity="high"
        ///   description="CRLF改行とLF改行が混在した出力で正しくパースされること"
        ///   expected="全行が正しくパースされる" />
        [Fact]
        public void ParseConfigOutput_MixedLineEndings_AllLinesParsed()
        {
            // Windows（CRLF）とUnix（LF）が混在
            var input = "key1=val1\r\nkey2=val2\nkey3=val3\r\nkey4=val4\n";
            var result = InvokeParseConfigOutput(input);

            Assert.Equal(4, result.Count);
            Assert.Equal("val1", result["key1"]);
            Assert.Equal("val2", result["key2"]);
            Assert.Equal("val3", result["key3"]);
            Assert.Equal("val4", result["key4"]);
        }

        /// <adversarial category="chaos" severity="medium"
        ///   description="途中で切れた出力（最後の行に改行なし＋=なし）でクラッシュしないこと"
        ///   expected="完全な行のみパースされる" />
        [Fact]
        public void ParseConfigOutput_TruncatedOutput_PartialLineIgnored()
        {
            // gitプロセスがkillされて出力が途中で切れた場合をシミュレート
            var input = "key1=val1\nkey2=val2\ntruncated_key_no_eq";
            var result = InvokeParseConfigOutput(input);

            Assert.Equal(2, result.Count);
            Assert.False(result.ContainsKey("truncated_key_no_eq"));
        }

        /// <adversarial category="chaos" severity="high"
        ///   description="重複キーで後勝ちになること（git configの仕様通り）"
        ///   expected="最後の値で上書き" />
        [Fact]
        public void ParseConfigOutput_DuplicateKeys_LastWins()
        {
            // gitは同名キーが複数行出力される場合がある
            var input = "user.name=Alice\nuser.name=Bob\nuser.name=Charlie";
            var result = InvokeParseConfigOutput(input);

            Assert.Single(result);
            Assert.Equal("Charlie", result["user.name"]);
        }

        // ===============================================================
        // 🔀 状態・比較子テスト（State & Comparer）
        // ===============================================================

        /// <adversarial category="state" severity="high"
        ///   description="OrdinalIgnoreCaseコンパレータで大文字小文字が同一キーとして扱われること"
        ///   expected="後勝ちで統合される" />
        [Fact]
        public void ParseConfigOutput_CaseInsensitiveComparer_MergesKeys()
        {
            var input = "User.Name=Alice\nuser.name=Bob";
            var result = InvokeParseConfigOutput(input, StringComparer.OrdinalIgnoreCase);

            // 大文字小文字を区別しない → 同一キーとして後勝ち
            Assert.Single(result);
            Assert.Equal("Bob", result["user.name"]);
            Assert.Equal("Bob", result["User.Name"]); // どちらのケースでもアクセス可能
        }

        /// <adversarial category="state" severity="medium"
        ///   description="デフォルト（コンパレータなし）では大文字小文字が区別されること"
        ///   expected="別キーとして格納" />
        [Fact]
        public void ParseConfigOutput_DefaultComparer_CaseSensitive()
        {
            var input = "User.Name=Alice\nuser.name=Bob";
            var result = InvokeParseConfigOutput(input);

            Assert.Equal(2, result.Count);
            Assert.Equal("Alice", result["User.Name"]);
            Assert.Equal("Bob", result["user.name"]);
        }

        // ===============================================================
        // 🔀 Get / SetAsync の引数境界テスト
        // ===============================================================

        // ===============================================================
        // 🌪️ ParseConfigOutput に対するカオス・パフォーマンス攻撃
        // ===============================================================

        /// <adversarial category="chaos" severity="high"
        ///   description="値にCRLFが含まれるgit configのmultiline値をシミュレート"
        ///   expected="改行で分割されるため、multiline値は正しく処理されない（仕様上の制限）" />
        [Fact]
        public void ParseConfigOutput_ValueWithEmbeddedNewline_SplitsIntoMultipleEntries()
        {
            // git config -l は通常1行1エントリだが、
            // 値にリテラル改行が含まれる設定（例: commit.template）は
            // 複数行に分割されて出力される可能性がある
            var input = "commit.template=line1\nline2\nuser.name=test";
            var result = InvokeParseConfigOutput(input);

            // "line2" は=がないので無視される
            // commit.template=line1 と user.name=test の2件
            Assert.Equal(2, result.Count);
            Assert.Equal("line1", result["commit.template"]);
            Assert.Equal("test", result["user.name"]);
        }

        /// <adversarial category="chaos" severity="medium"
        ///   description="行頭・行末の空白が保持されること（trimされない）"
        ///   expected="キーと値に空白が含まれたまま格納" />
        [Fact]
        public void ParseConfigOutput_LeadingTrailingSpaces_PreservedInKeyAndValue()
        {
            var input = "  key  =  value  ";
            var result = InvokeParseConfigOutput(input);

            // Split('=', 2) はtrimしない → 空白が保持される
            Assert.Single(result);
            Assert.True(result.ContainsKey("  key  "), "キーの前後空白が保持されるべき");
            Assert.Equal("  value  ", result["  key  "]);
        }

        /// <adversarial category="boundary" severity="medium"
        ///   description="=だけの行でキーも値も空文字として格納されること"
        ///   expected="空キー=空値" />
        [Fact]
        public void ParseConfigOutput_EqualsOnly_EmptyKeyEmptyValue()
        {
            var input = "=";
            var result = InvokeParseConfigOutput(input);

            Assert.Single(result);
            Assert.Equal("", result[""]);
        }

        /// <adversarial category="chaos" severity="low"
        ///   description="CRだけの改行（旧Mac形式）で正しく分割されること"
        ///   expected="CR単独でも行分割される" />
        [Fact]
        public void ParseConfigOutput_CarriageReturnOnly_SplitsCorrectly()
        {
            // Split(['\r', '\n'], ...) は \r 単独でも分割する
            var input = "key1=val1\rkey2=val2\rkey3=val3";
            var result = InvokeParseConfigOutput(input);

            Assert.Equal(3, result.Count);
        }
    }
}
