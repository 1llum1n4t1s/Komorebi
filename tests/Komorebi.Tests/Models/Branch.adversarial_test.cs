using System.Collections.Generic;
using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// Branchクラスに対するadversarialテスト。
    /// null リスト、極端なカウント、FriendlyName の null Remote を攻撃する。
    /// </summary>
    public class BranchAdversarialTests
    {
        // ================================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ================================================================

        /// <summary>
        /// @adversarial @category boundary @severity high
        /// Ahead=null で IsTrackStatusVisible がクラッシュしないこと
        /// </summary>
        [Fact]
        public void IsTrackStatusVisible_NullAhead_DoesNotCrash()
        {
            var branch = new Branch { Ahead = null, Behind = [] };
            Assert.False(branch.IsTrackStatusVisible);
        }

        /// <summary>
        /// @adversarial @category boundary @severity high
        /// Behind=null で IsTrackStatusVisible がクラッシュしないこと
        /// </summary>
        [Fact]
        public void IsTrackStatusVisible_NullBehind_DoesNotCrash()
        {
            var branch = new Branch { Ahead = [], Behind = null };
            Assert.False(branch.IsTrackStatusVisible);
        }

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

        /// <summary>
        /// @adversarial @category boundary @severity high
        /// 両方null で TrackStatusDescription がクラッシュしないこと
        /// </summary>
        [Fact]
        public void TrackStatusDescription_BothNull_ReturnsEmpty()
        {
            var branch = new Branch { Ahead = null, Behind = null };
            Assert.Equal(string.Empty, branch.TrackStatusDescription);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// 大量のAhead/Behindコミットで正常な文字列を返すこと
        /// </summary>
        [Fact]
        public void TrackStatusDescription_LargeCommitCounts_FormatsCorrectly()
        {
            var ahead = new List<string>();
            var behind = new List<string>();
            for (var i = 0; i < 10_000; i++)
            {
                ahead.Add($"sha{i}");
                behind.Add($"sha{i}");
            }

            var branch = new Branch { Ahead = ahead, Behind = behind };
            Assert.Equal("10000↑ 10000↓", branch.TrackStatusDescription);
        }

        // ================================================================
        // 🎭 型パンチ・プロトコル違反（Type Punching）
        // ================================================================

        /// <summary>
        /// @adversarial @category type @severity medium
        /// FriendlyName で Remote=null、IsLocal=false の場合にクラッシュしないこと
        /// </summary>
        [Fact]
        public void FriendlyName_NullRemoteNotLocal_DoesNotCrash()
        {
            var branch = new Branch { Name = "main", IsLocal = false, Remote = null };
            var name = branch.FriendlyName;
            Assert.Equal("/main", name);
        }

        /// <summary>
        /// @adversarial @category type @severity medium
        /// FriendlyName で Name=null の場合にクラッシュしないこと
        /// </summary>
        [Fact]
        public void FriendlyName_NullName_DoesNotCrash()
        {
            var branch = new Branch { Name = null, IsLocal = true };
            Assert.Null(branch.FriendlyName);
        }

        /// <summary>
        /// @adversarial @category type @severity low
        /// CommitterDate に ulong.MaxValue を設定してもクラッシュしないこと
        /// </summary>
        [Fact]
        public void CommitterDate_MaxValue_DoesNotCrash()
        {
            var branch = new Branch { CommitterDate = ulong.MaxValue };
            Assert.Equal(ulong.MaxValue, branch.CommitterDate);
        }

        // ================================================================
        // 🔀 状態遷移の矛盾（State Machine Abuse）
        // ================================================================

        /// <summary>
        /// @adversarial @category state @severity medium
        /// HasWorktree の条件（IsCurrent=false かつ WorktreePath 設定済み）が正しいこと
        /// </summary>
        [Theory]
        [InlineData(true, "/path", false)]   // IsCurrent=true → HasWorktree=false
        [InlineData(false, "/path", true)]   // IsCurrent=false, path設定 → true
        [InlineData(false, "", false)]       // IsCurrent=false, path空 → false
        [InlineData(false, null, false)]     // IsCurrent=false, path=null → false
        [InlineData(true, null, false)]      // IsCurrent=true, path=null → false
        public void HasWorktree_AllCombinations(bool isCurrent, string path, bool expected)
        {
            var branch = new Branch { IsCurrent = isCurrent, WorktreePath = path };
            Assert.Equal(expected, branch.HasWorktree);
        }

        /// <summary>
        /// @adversarial @category state @severity medium
        /// Ahead/Behindリストのクリア後にTrackStatusが更新されること
        /// </summary>
        [Fact]
        public void TrackStatusDescription_AfterListClear_ReflectsChange()
        {
            var branch = new Branch
            {
                Ahead = new List<string> { "a", "b" },
                Behind = new List<string> { "c" }
            };
            Assert.Equal("2↑ 1↓", branch.TrackStatusDescription);

            branch.Ahead.Clear();
            Assert.Equal("1↓", branch.TrackStatusDescription);

            branch.Behind.Clear();
            Assert.Equal(string.Empty, branch.TrackStatusDescription);
        }
    }
}
