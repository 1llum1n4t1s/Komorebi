using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// NumericSortクラスに対するadversarialテスト。
    /// Unicode数字、桁数の大きい数値チャンク、先頭ゼロを攻撃する。
    /// </summary>
    public class NumericSortAdversarialTests
    {
        // ================================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ================================================================


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

        // ================================================================
        // 🌪️ 環境異常・カオステスト（Environmental Chaos）
        // ================================================================

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
