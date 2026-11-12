using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// Commit.ParseDecorators / ParseParents / GetFriendlyName に対する嫌がらせテスト。
    /// Substringの境界値、デコレーション解析のエッジケース、ソートの安定性を攻撃する。
    /// </summary>
    public class CommitAdversarialTests
    {
        // ===============================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ===============================================================

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>2文字以下の入力で早期リターンすること（Length &lt; 3チェック）</summary>
        [Theory]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("ab")]
        public void ParseDecorators_TooShort_ReturnsEmpty(string data)
        {
            var commit = new Commit();
            commit.ParseDecorators(data);
            Assert.Empty(commit.Decorators);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>タグ名にヌルバイトが含まれる場合でもクラッシュしないこと</summary>
        [Fact]
        public void ParseDecorators_TagWithNullByte_DoesNotThrow()
        {
            var commit = new Commit();
            var ex = Record.Exception(() => commit.ParseDecorators("tag: refs/tags/v1.0\x00evil"));
            Assert.Null(ex);
            Assert.Single(commit.Decorators);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>巨大なデコレーション文字列（100KB）でクラッシュしないこと</summary>
        [Fact]
        public void ParseDecorators_HugeInput_DoesNotThrow()
        {
            var huge = "tag: refs/tags/" + new string('a', 100_000);
            var commit = new Commit();

            var ex = Record.Exception(() => commit.ParseDecorators(huge));

            Assert.Null(ex);
            Assert.Single(commit.Decorators);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>カンマだけのデコレーション文字列でクラッシュしないこと</summary>
        [Theory]
        [InlineData(",,,")]
        [InlineData("   ,   ,   ")]
        public void ParseDecorators_OnlyCommas_DoesNotThrow(string data)
        {
            var commit = new Commit();
            var ex = Record.Exception(() => commit.ParseDecorators(data));
            Assert.Null(ex);
            Assert.Empty(commit.Decorators);
        }

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>
        /// SHA が空の Commit でも GetFriendlyName が例外にならず空文字を返すこと。
        /// 中断中の操作の HEAD ファイル（CHERRY_PICK_HEAD 等）が空のとき、InProgressContexts が作る代用コミットがこの状態になる。
        /// </summary>
        [Fact]
        public void GetFriendlyName_EmptySHA_ReturnsEmpty()
        {
            var commit = new Commit { SHA = "" };
            Assert.Equal(string.Empty, commit.GetFriendlyName());
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>
        /// SHA が 10 文字以下なら例外にならず短いまま返し、10 文字を超えれば先頭 10 文字に短縮すること
        /// （HEAD ファイルが途中で切れている場合の代用コミット）。
        /// </summary>
        [Theory]
        [InlineData("", "")]
        [InlineData("abc", "abc")]
        [InlineData("123456789", "123456789")]
        [InlineData("1234567890", "1234567890")]
        [InlineData("1234567890a", "1234567890")]
        public void GetFriendlyName_ShortSHA_ReturnsAsIs(string sha, string expected)
        {
            var commit = new Commit { SHA = sha };
            Assert.Equal(expected, commit.GetFriendlyName());
        }

        // ===============================================================
        // 🎭 型パンチ・プロトコル違反（Type Punching）
        // ===============================================================

        /// <adversarial category="type" severity="high" />
        /// <summary>認識されないプレフィックスのデコレーションは無視されること</summary>
        [Theory]
        [InlineData("unknown/prefix/branch")]
        [InlineData("STASH: refs/stash")]
        [InlineData("notes/commits")]
        public void ParseDecorators_UnknownPrefix_Ignored(string data)
        {
            var commit = new Commit();
            commit.ParseDecorators(data);
            Assert.Empty(commit.Decorators);
        }

        /// <adversarial category="type" severity="high" />
        /// <summary>大文字小文字が異なるプレフィックス（TAG:）は無視されること</summary>
        [Fact]
        public void ParseDecorators_CaseSensitivePrefix_Ignored()
        {
            var commit = new Commit();
            // Ordinal比較なので大文字のTAGは認識されないはず
            commit.ParseDecorators("TAG: refs/tags/v1.0");
            Assert.Empty(commit.Decorators);
        }

        // ===============================================================
        // 🌪️ 環境異常・カオステスト（Environmental Chaos）
        // ===============================================================

        /// <adversarial category="chaos" severity="high" />
        /// <summary>Unicode文字を含むブランチ名が正しくパースされること</summary>
        [Fact]
        public void ParseDecorators_UnicodeTagName_Preserved()
        {
            var commit = new Commit();
            commit.ParseDecorators("tag: refs/tags/日本語タグ🏷️");
            Assert.Single(commit.Decorators);
            Assert.Equal("日本語タグ🏷️", commit.Decorators[0].Name);
        }

        /// <adversarial category="chaos" severity="high" />
        /// <summary>ゼロ幅文字を含むブランチ名でクラッシュしないこと</summary>
        [Fact]
        public void ParseDecorators_ZeroWidthChars_DoesNotThrow()
        {
            var commit = new Commit();
            var ex = Record.Exception(() =>
                commit.ParseDecorators("refs/heads/\u200B\u200Bbranch\u200B"));
            Assert.Null(ex);
            Assert.Single(commit.Decorators);
        }

        /// <adversarial category="chaos" severity="medium" />
        /// <summary>RTL制御文字を含むリモートブランチ名でクラッシュしないこと</summary>
        [Fact]
        public void ParseDecorators_RTLOverride_DoesNotThrow()
        {
            var commit = new Commit();
            var ex = Record.Exception(() =>
                commit.ParseDecorators("refs/remotes/origin/\u202Eevil"));
            Assert.Null(ex);
            Assert.Single(commit.Decorators);
        }

        /// <adversarial category="chaos" severity="high" />
        /// <summary>サロゲートペア（絵文字）を含むタグ名が正しく処理されること</summary>
        [Fact]
        public void ParseDecorators_SurrogatePairs_DoesNotThrow()
        {
            var commit = new Commit();
            var ex = Record.Exception(() =>
                commit.ParseDecorators("tag: refs/tags/release-👨‍👩‍👧‍👦"));
            Assert.Null(ex);
            Assert.Single(commit.Decorators);
        }

        // ===============================================================
        // ⚡ 並行性・レースコンディション（Concurrency Chaos）
        // ===============================================================

        // ParseDecorators_CalledMultipleTimes_AccumulatesDecorators は Commit.adversarial_v2_test.cs の同名テスト
        // （ブランチ・タグ・リモートの 3 回呼び出しで蓄積を確認）と同じ契約の重複だったため、v2 側へ統合した。

        // ===============================================================
        // 🔀 状態遷移の矛盾（State Machine Abuse）
        // ===============================================================

        /// <adversarial category="state" severity="high" />
        /// <summary>HEADデコレーションでIsMergedがtrueに設定されること</summary>
        [Fact]
        public void ParseDecorators_HEAD_SetsIsMerged()
        {
            var commit = new Commit();
            Assert.False(commit.IsMerged);

            commit.ParseDecorators("HEAD");
            Assert.True(commit.IsMerged);
        }

        /// <adversarial category="state" severity="high" />
        /// <summary>IsMergedが一度trueになったら、HEADなしのデコレーションでもfalseに戻らないこと</summary>
        [Fact]
        public void ParseDecorators_IsMergedNeverReverts()
        {
            var commit = new Commit();
            commit.ParseDecorators("HEAD");
            Assert.True(commit.IsMerged);

            // HEADを含まないデコレーションを追加してもIsMergedは維持される
            commit.ParseDecorators("tag: refs/tags/v1.0");
            Assert.True(commit.IsMerged);
        }

        /// <adversarial category="state" severity="medium" />
        /// <summary>複数の同一タイプのデコレーションがソート順序を維持すること</summary>
        [Fact]
        public void ParseDecorators_MultipleSameType_SortedCorrectly()
        {
            var commit = new Commit();
            commit.ParseDecorators("tag: refs/tags/v2.0, tag: refs/tags/v1.0, tag: refs/tags/v10.0");

            Assert.Equal(3, commit.Decorators.Count);
            // NumericSortによりv1.0 < v2.0 < v10.0の順
            Assert.Equal("v1.0", commit.Decorators[0].Name);
            Assert.Equal("v2.0", commit.Decorators[1].Name);
            Assert.Equal("v10.0", commit.Decorators[2].Name);
        }

        // ===============================================================
        // 💀 リソース枯渇・DoS耐性（Resource Exhaustion）
        // ===============================================================

        /// <adversarial category="resource" severity="high" />
        /// <summary>大量のカンマ区切りデコレーション（1000個）でパフォーマンスが劣化しないこと</summary>
        [Fact]
        public void ParseDecorators_ThousandDecorators_CompletesInTime()
        {
            var parts = new string[1000];
            for (int i = 0; i < 1000; i++)
                parts[i] = $"tag: refs/tags/v{i}.0";
            var data = string.Join(", ", parts);

            var commit = new Commit();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            commit.ParseDecorators(data);
            sw.Stop();

            Assert.Equal(1000, commit.Decorators.Count);
            Assert.True(sw.ElapsedMilliseconds < 3000, $"1000デコレーションの解析に{sw.ElapsedMilliseconds}msかかった");
        }
    }
}
