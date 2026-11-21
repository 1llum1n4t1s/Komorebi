using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// Commit.ParseDecorators / ParseParents / GetFriendlyName に対する嫌がらせテスト。
    /// 短い入力の早期リターン、未知プレフィックス、IsMerged の状態遷移を攻撃する。
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
    }
}
