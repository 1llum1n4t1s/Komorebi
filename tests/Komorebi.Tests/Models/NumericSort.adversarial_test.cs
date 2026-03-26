using System.Diagnostics;
using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// NumericSortクラスに対するadversarialテスト。
    /// Unicode数字、巨大数値、空文字列、制御文字を攻撃する。
    /// </summary>
    public class NumericSortAdversarialTests
    {
        // ================================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ================================================================

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// 空文字列同士の比較で 0 を返すこと
        /// </summary>
        [Fact]
        public void Compare_BothEmpty_ReturnsZero()
        {
            Assert.Equal(0, NumericSort.Compare("", ""));
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// 空文字列 vs 非空文字列の比較で一貫した結果を返すこと
        /// </summary>
        [Fact]
        public void Compare_EmptyVsNonEmpty_EmptyIsSmaller()
        {
            Assert.True(NumericSort.Compare("", "a") < 0);
            Assert.True(NumericSort.Compare("a", "") > 0);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// 単一文字の比較が正しいこと
        /// </summary>
        [Theory]
        [InlineData("a", "b", -1)]
        [InlineData("b", "a", 1)]
        [InlineData("a", "a", 0)]
        [InlineData("1", "2", -1)]
        [InlineData("9", "1", 1)]
        public void Compare_SingleChars_CorrectOrder(string s1, string s2, int expectedSign)
        {
            var result = NumericSort.Compare(s1, s2);
            Assert.Equal(expectedSign, System.Math.Sign(result));
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// 非常に長い数値チャンクの比較（桁数が異なる場合は桁数で決まる）
        /// </summary>
        [Fact]
        public void Compare_VeryLongNumberChunks_ComparesCorrectly()
        {
            // 100桁の「1」で始まる数値 vs 99桁の「9」で始まる数値
            // 桁数が多い方が大きい
            var big = "1" + new string('0', 99);   // 100桁
            var small = new string('9', 99);        // 99桁
            Assert.True(NumericSort.Compare("file" + big, "file" + small) > 0);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// ヌルバイトを含む文字列でクラッシュしないこと
        /// </summary>
        [Fact]
        public void Compare_NullByteInString_DoesNotCrash()
        {
            _ = NumericSort.Compare("file\0name1", "file\0name2");
        }

        /// <summary>
        /// @adversarial @category boundary @severity low
        /// 100KBの文字列でもクラッシュせず合理的な時間で完了すること
        /// </summary>
        [Fact]
        public void Compare_HugeStrings_CompletesInReasonableTime()
        {
            var s1 = new string('a', 100_000);
            var s2 = new string('a', 100_000);
            var sw = Stopwatch.StartNew();
            var result = NumericSort.Compare(s1, s2);
            sw.Stop();
            Assert.Equal(0, result);
            Assert.True(sw.ElapsedMilliseconds < 1000, $"比較に{sw.ElapsedMilliseconds}msかかった");
        }

        // ================================================================
        // 🎭 型パンチ・プロトコル違反（Type Punching）
        // ================================================================

        /// <summary>
        /// @adversarial @category type @severity high
        /// Unicode数字（アラビア数字等）がASCII数字として扱われないこと
        /// char.IsAsciiDigit による修正が正しいことを確認
        /// </summary>
        [Fact]
        public void Compare_ArabicIndicDigits_NotTreatedAsNumbers()
        {
            // ٠١٢ (ARABIC-INDIC DIGIT ZERO/ONE/TWO) はASCII数字ではない
            // → テキストとして比較されるべき
            var result1 = NumericSort.Compare("file\u0660", "file0");
            // Unicode数字が数字として扱われていないなら、文字コード順で比較される
            // \u0660 > '0' (0x30) なので正の値
            Assert.True(result1 > 0);
        }

        /// <summary>
        /// @adversarial @category type @severity medium
        /// 全角数字がASCII数字として扱われないこと
        /// </summary>
        [Fact]
        public void Compare_FullWidthDigits_NotTreatedAsNumbers()
        {
            // ０１２ (FULLWIDTH DIGIT) はASCII数字ではない
            var result = NumericSort.Compare("file\uFF10", "file0");
            Assert.NotEqual(0, result);
        }

        /// <summary>
        /// @adversarial @category type @severity medium
        /// 数字と非数字が交互に現れる文字列の比較
        /// </summary>
        [Fact]
        public void Compare_AlternatingDigitsAndText_CorrectOrder()
        {
            // "a1b2c3" vs "a1b2c4" → 最後の数字チャンクで決まる
            Assert.True(NumericSort.Compare("a1b2c3", "a1b2c4") < 0);
            Assert.True(NumericSort.Compare("a1b2c10", "a1b2c3") > 0);
        }

        // ================================================================
        // ⚡ 並行性・レースコンディション（Concurrency Chaos）
        // ================================================================

        /// <summary>
        /// @adversarial @category concurrency @severity medium
        /// NumericSort.Compare はスレッドセーフであること（static メソッド、状態なし）
        /// </summary>
        [Fact]
        public void Compare_ConcurrentCalls_ThreadSafe()
        {
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<System.Exception>();
            System.Threading.Tasks.Parallel.For(0, 1000, i =>
            {
                try
                {
                    var result = NumericSort.Compare($"file{i}", $"file{i + 1}");
                    Assert.True(result < 0);
                }
                catch (System.Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
            Assert.Empty(exceptions);
        }

        // ================================================================
        // 🌪️ 環境異常・カオステスト（Environmental Chaos）
        // ================================================================

        /// <summary>
        /// @adversarial @category chaos @severity medium
        /// サロゲートペア（絵文字）を含む文字列でクラッシュしないこと
        /// </summary>
        [Fact]
        public void Compare_EmojiInString_DoesNotCrash()
        {
            _ = NumericSort.Compare("file👨‍👩‍👧‍👦1", "file👨‍👩‍👧‍👦2");
        }

        /// <summary>
        /// @adversarial @category chaos @severity low
        /// RTL制御文字を含む文字列でクラッシュしないこと
        /// </summary>
        [Fact]
        public void Compare_RtlOverride_DoesNotCrash()
        {
            _ = NumericSort.Compare("file\u202E1", "file\u202E2");
        }

        /// <summary>
        /// @adversarial @category chaos @severity medium
        /// 先頭ゼロ付き数値の比較（"file01" vs "file1"）
        /// 桁数が異なるため、先頭ゼロ付きの方が大きいと判定される
        /// </summary>
        [Fact]
        public void Compare_LeadingZeros_LongerChunkIsLarger()
        {
            // 桁数2 vs 桁数1 → 2桁の方が大きい
            Assert.True(NumericSort.Compare("file01", "file1") > 0);
        }
    }
}
