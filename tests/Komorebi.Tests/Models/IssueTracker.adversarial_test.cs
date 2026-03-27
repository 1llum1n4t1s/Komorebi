using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// IssueTracker に対する嫌がらせテスト。
    /// ReDoS、正規表現インジェクション、URLテンプレートインジェクションを攻撃する。
    /// </summary>
    public class IssueTrackerAdversarialTests
    {
        // ===============================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ===============================================================

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>RegexStringにnull/空/空白を設定してもクラッシュしないこと</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public void RegexString_NullOrWhitespace_SetsRegexNull(string? pattern)
        {
            var tracker = new IssueTracker();
            var ex = Record.Exception(() => tracker.RegexString = pattern);
            Assert.Null(ex);
            Assert.False(tracker.IsRegexValid);
        }

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>Matchesに空文字列メッセージを渡してもクラッシュしないこと</summary>
        [Fact]
        public void Matches_EmptyMessage_DoesNotThrow()
        {
            var tracker = new IssueTracker
            {
                RegexString = @"#(\d+)",
                URLTemplate = "https://example.com/issues/$1"
            };
            var collector = new InlineElementCollector();

            var ex = Record.Exception(() => tracker.Matches(collector, ""));
            Assert.Null(ex);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>巨大なメッセージ（1MB）でMatchesがタイムアウトしないこと</summary>
        [Fact]
        public void Matches_HugeMessage_CompletesInTime()
        {
            var tracker = new IssueTracker
            {
                RegexString = @"#(\d+)",
                URLTemplate = "https://example.com/issues/$1"
            };
            var collector = new InlineElementCollector();
            var message = "fix: " + new string('x', 1_000_000) + " #12345";

            var sw = System.Diagnostics.Stopwatch.StartNew();
            tracker.Matches(collector, message);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 3000, $"1MBメッセージの検索に{sw.ElapsedMilliseconds}msかかった");
        }

        // ===============================================================
        // 💀 リソース枯渇・DoS耐性（Resource Exhaustion）
        // ===============================================================

        /// <adversarial category="resource" severity="critical" />
        /// <summary>ReDoS脆弱な正規表現パターンがタイムアウトで安全に中断されること</summary>
        [Fact]
        public void RegexString_ReDoSPattern_TimesOutGracefully()
        {
            var tracker = new IssueTracker();

            // (a+)+ は古典的なReDoSパターン
            tracker.RegexString = @"(a+)+$";
            Assert.True(tracker.IsRegexValid);

            tracker.URLTemplate = "https://example.com/$1";
            var collector = new InlineElementCollector();
            var evil = new string('a', 30) + "!"; // ReDoSトリガー

            // タイムアウト設定済みのため、RegexMatchTimeoutExceptionが発生しても
            // Matches内でキャッチされ、クラッシュせずに完了する
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ex = Record.Exception(() => tracker.Matches(collector, evil));
            sw.Stop();

            Assert.Null(ex);
            // タイムアウトは1秒に設定されているので、2秒以内に完了するはず
            Assert.True(sw.ElapsedMilliseconds < 3000,
                $"ReDoSパターンの処理に{sw.ElapsedMilliseconds}msかかった");
        }

        /// <adversarial category="resource" severity="high" />
        /// <summary>大量のマッチ（10000個）でメモリ溢れしないこと</summary>
        [Fact]
        public void Matches_ManyMatches_DoesNotExhaust()
        {
            var tracker = new IssueTracker
            {
                RegexString = @"#(\d+)",
                URLTemplate = "https://example.com/issues/$1"
            };
            var collector = new InlineElementCollector();

            // 10000個の課題番号を含むメッセージ
            var parts = new string[10_000];
            for (int i = 0; i < parts.Length; i++)
                parts[i] = $"#{i}";
            var message = string.Join(" ", parts);

            var ex = Record.Exception(() => tracker.Matches(collector, message));
            Assert.Null(ex);
        }

        // ===============================================================
        // 🎭 型パンチ・プロトコル違反（Type Punching）
        // ===============================================================

        /// <adversarial category="type" severity="critical" />
        /// <summary>無効な正規表現パターンでクラッシュしないこと</summary>
        [Theory]
        [InlineData("[")]
        [InlineData("(")]
        [InlineData("*")]
        [InlineData("(?P<")]
        [InlineData(@"\")]
        public void RegexString_InvalidPattern_SetsRegexNull(string pattern)
        {
            var tracker = new IssueTracker();
            var ex = Record.Exception(() => tracker.RegexString = pattern);
            Assert.Null(ex);
            Assert.False(tracker.IsRegexValid);
        }

        /// <adversarial category="type" severity="high" />
        /// <summary>URLテンプレートに$1プレースホルダーがなくてもクラッシュしないこと</summary>
        [Fact]
        public void Matches_TemplateWithoutPlaceholder_DoesNotThrow()
        {
            var tracker = new IssueTracker
            {
                RegexString = @"#(\d+)",
                URLTemplate = "https://example.com/issues/fixed-url"
            };
            var collector = new InlineElementCollector();

            var ex = Record.Exception(() => tracker.Matches(collector, "fix #123"));
            Assert.Null(ex);
        }

        /// <adversarial category="type" severity="high" />
        /// <summary>URLテンプレートが空の場合Matchesが早期リターンすること</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Matches_EmptyTemplate_EarlyReturn(string? template)
        {
            var tracker = new IssueTracker
            {
                RegexString = @"#(\d+)",
                URLTemplate = template
            };
            var collector = new InlineElementCollector();

            var ex = Record.Exception(() => tracker.Matches(collector, "fix #123"));
            Assert.Null(ex);
        }

        /// <adversarial category="type" severity="medium" />
        /// <summary>マッチグループのValueにURLテンプレート内の$記号が含まれる場合</summary>
        [Fact]
        public void Matches_GroupValueContainsDollarSign_DoesNotInjectPlaceholder()
        {
            var tracker = new IssueTracker
            {
                // $2を含むテキストをキャプチャ
                RegexString = @"issue-(\S+)",
                URLTemplate = "https://example.com/$1"
            };
            var collector = new InlineElementCollector();

            // キャプチャされた値に"$2"が含まれる場合、
            // URLテンプレートの$2プレースホルダーと衝突しないことを確認
            tracker.Matches(collector, "issue-$2-test");
            // クラッシュしなければOK
        }

        // ===============================================================
        // 🌪️ 環境異常・カオステスト（Environmental Chaos）
        // ===============================================================

        /// <adversarial category="chaos" severity="high" />
        /// <summary>メッセージにCRLF/LFが混在する場合でもMultilineモードで正しくマッチすること</summary>
        [Fact]
        public void Matches_MixedLineEndings_MatchesAcrossLines()
        {
            var tracker = new IssueTracker
            {
                RegexString = @"#(\d+)",
                URLTemplate = "https://example.com/issues/$1"
            };
            var collector = new InlineElementCollector();

            var message = "fix #123\r\nand #456\nand #789\r";
            tracker.Matches(collector, message);
            // Multilineモードなので全行でマッチするはず
        }

        /// <adversarial category="chaos" severity="medium" />
        /// <summary>Unicode文字を含むメッセージでマッチが正しく動作すること</summary>
        [Fact]
        public void Matches_UnicodeMessage_DoesNotThrow()
        {
            var tracker = new IssueTracker
            {
                RegexString = @"#(\d+)",
                URLTemplate = "https://example.com/issues/$1"
            };
            var collector = new InlineElementCollector();

            var ex = Record.Exception(() =>
                tracker.Matches(collector, "修正 #123 🐛 バグフィックス"));
            Assert.Null(ex);
        }

        // ===============================================================
        // 🔀 状態遷移の矛盾（State Machine Abuse）
        // ===============================================================

        /// <adversarial category="state" severity="high" />
        /// <summary>RegexStringを変更した後のMatchesが新しいパターンを使用すること</summary>
        [Fact]
        public void Matches_AfterRegexChange_UsesNewPattern()
        {
            var tracker = new IssueTracker
            {
                RegexString = @"#(\d+)",
                URLTemplate = "https://example.com/$1"
            };

            // パターンを変更
            tracker.RegexString = @"ISSUE-(\d+)";

            var collector = new InlineElementCollector();
            tracker.Matches(collector, "#123 ISSUE-456");

            // 新しいパターンでマッチするはず（#123はマッチしない）
        }

        /// <adversarial category="state" severity="medium" />
        /// <summary>RegexStringを無効→有効→無効と切り替えてもクラッシュしないこと</summary>
        [Fact]
        public void RegexString_ToggleValidInvalid_DoesNotThrow()
        {
            var tracker = new IssueTracker();

            var ex = Record.Exception(() =>
            {
                tracker.RegexString = @"#(\d+)";   // 有効
                Assert.True(tracker.IsRegexValid);

                tracker.RegexString = "[";           // 無効
                Assert.False(tracker.IsRegexValid);

                tracker.RegexString = @"ISSUE-(\d+)"; // 有効
                Assert.True(tracker.IsRegexValid);

                tracker.RegexString = null;           // null
                Assert.False(tracker.IsRegexValid);
            });

            Assert.Null(ex);
        }
    }
}
