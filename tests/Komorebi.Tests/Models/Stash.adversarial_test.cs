using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// Stashクラスに対するadversarialテスト。
    /// Subject プロパティのnull/極端入力、Timeの境界値、Parentsのnullを攻撃する。
    /// </summary>
    public class StashAdversarialTests
    {
        // ================================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ================================================================

        /// <summary>
        /// @adversarial @category boundary @severity high
        /// Message=null の場合、Subject がクラッシュせずに空文字列を返すこと
        /// </summary>
        [Fact]
        public void Subject_NullMessage_ReturnsEmptyWithoutCrash()
        {
            var stash = new Stash { Message = null };
            var subject = stash.Subject;
            Assert.Equal("", subject);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// 改行のみで構成されたMessageのSubjectが空文字列を返すこと
        /// </summary>
        [Theory]
        [InlineData("\n")]
        [InlineData("\n\n\n")]
        [InlineData("\r\n")]
        [InlineData("\r\n\r\n")]
        public void Subject_OnlyNewlines_ReturnsEmpty(string message)
        {
            var stash = new Stash { Message = message };
            Assert.Equal("", stash.Subject);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// 空白文字のみのMessageがトリムされて空文字列を返すこと
        /// </summary>
        [Theory]
        [InlineData("   ")]
        [InlineData("\t\t")]
        [InlineData(" \t \t ")]
        public void Subject_WhitespaceOnly_ReturnsEmpty(string message)
        {
            var stash = new Stash { Message = message };
            Assert.Equal("", stash.Subject);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// 100KBの巨大なMessageでもSubjectがクラッシュしないこと
        /// </summary>
        [Fact]
        public void Subject_HugeMessage_DoesNotCrash()
        {
            var huge = new string('A', 100_000) + "\nSecond line";
            var stash = new Stash { Message = huge };
            Assert.Equal(100_000, stash.Subject.Length);
        }

        /// <summary>
        /// @adversarial @category boundary @severity low
        /// ゼロ幅文字を含むMessageのSubjectが正常に取得できること
        /// </summary>
        [Fact]
        public void Subject_ZeroWidthChars_PreservedInSubject()
        {
            var stash = new Stash { Message = "WIP\u200B: feature" };
            Assert.Contains("\u200B", stash.Subject);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// ヌルバイトを含むMessageでSubjectがクラッシュしないこと
        /// </summary>
        [Fact]
        public void Subject_NullByteInMessage_DoesNotCrash()
        {
            var stash = new Stash { Message = "WIP\0: feature\nDetails" };
            var subject = stash.Subject;
            Assert.Contains("\0", subject);
        }

        // ================================================================
        // 🎭 型パンチ・プロトコル違反（Type Punching）
        // ================================================================

        /// <summary>
        /// @adversarial @category type @severity medium
        /// Time に ulong.MaxValue を設定してもクラッシュしないこと
        /// </summary>
        [Fact]
        public void Time_MaxValue_DoesNotCrash()
        {
            var stash = new Stash { Time = ulong.MaxValue };
            Assert.Equal(ulong.MaxValue, stash.Time);
        }

        // ================================================================
        // 🔀 状態遷移の矛盾（State Machine Abuse）
        // ================================================================

        /// <summary>
        /// @adversarial @category state @severity medium
        /// Parents を null に設定してもオブジェクト自体はクラッシュしないこと
        /// </summary>
        [Fact]
        public void Parents_SetToNull_DoesNotCrashOnAccess()
        {
            var stash = new Stash { Parents = null };
            Assert.Null(stash.Parents);
        }

        /// <summary>
        /// @adversarial @category state @severity low
        /// Message を何度も上書きしてもSubjectが最新値を返すこと
        /// </summary>
        [Fact]
        public void Subject_AfterMultipleMessageChanges_ReflectsLatest()
        {
            var stash = new Stash { Message = "first" };
            Assert.Equal("first", stash.Subject);

            stash.Message = "second\ndetails";
            Assert.Equal("second", stash.Subject);

            stash.Message = null;
            Assert.Equal("", stash.Subject);

            stash.Message = "third";
            Assert.Equal("third", stash.Subject);
        }
    }
}
