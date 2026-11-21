// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// Branchクラスに対するadversarialテスト。
    /// Ahead / Behind が null のときの TrackStatusDescription を攻撃する。
    /// </summary>
    public class BranchAdversarialTests
    {
        // ================================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ================================================================

        /// <summary>
        /// @adversarial @category boundary @severity high
        /// Ahead=null で TrackStatusDescription がクラッシュしないこと
        /// </summary>
        [Fact]
        public void TrackStatusDescription_NullAhead_ReturnsEmpty()
        {
            var branch = new Branch { Ahead = null, Behind = [] };
            Assert.Equal(string.Empty, branch.TrackStatusDescription);
        }

        /// <summary>
        /// @adversarial @category boundary @severity high
        /// Behind=null で TrackStatusDescription がクラッシュしないこと
        /// </summary>
        [Fact]
        public void TrackStatusDescription_NullBehind_ReturnsEmpty()
        {
            var branch = new Branch { Ahead = [], Behind = null };
            Assert.Equal(string.Empty, branch.TrackStatusDescription);
        }
    }
}
