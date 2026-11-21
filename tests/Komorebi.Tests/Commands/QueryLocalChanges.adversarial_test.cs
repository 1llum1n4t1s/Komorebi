using Komorebi.Commands;
using Komorebi.Models;

namespace Komorebi.Tests.Commands
{
    /// <summary>
    /// QueryLocalChanges.ParseLine() に対する嫌がらせテスト。
    /// git status porcelain出力のパーサに対し、異常入力・境界値・不正フォーマットを注入する。
    /// </summary>
    public class QueryLocalChangesAdversarialTests
    {
        // ===============================================================
        // 🎭 型パンチ・プロトコル違反（Type Punching）
        // ===============================================================

        /// <adversarial category="type" severity="high" />
        /// <summary>不正なステータスコード（5文字以上）でクラッシュしないこと</summary>
        [Theory]
        [InlineData("MMMMM src/file.cs")]
        [InlineData("ABCDE src/file.cs")]
        [InlineData("????? src/file.cs")]
        public void ParseLine_OversizedStatusCode_ReturnsNull(string line)
        {
            // 正規表現 [\w\?]{1,4} で4文字までしかマッチしないはず
            var result = QueryLocalChanges.ParseLine(line);
            Assert.Null(result);
        }

        /// <adversarial category="type" severity="medium" />
        /// <summary>タブ文字がパス名に含まれるリネーム以外のケースでの挙動</summary>
        [Fact]
        public void ParseLine_TabInNonRenameStatus_PreservesTabInPath()
        {
            // Modifiedでタブを含むパスの場合、リネーム処理は走らないはず
            var result = QueryLocalChanges.ParseLine(" M src/file\twith\ttabs.cs");
            Assert.NotNull(result);
            Assert.Equal(ChangeState.Modified, result.WorkTree);
            // パスにタブが残っているはず
            Assert.Contains("\t", result.Path);
        }

        // ===============================================================
        // 🔀 状態遷移の矛盾（State Machine Abuse）
        // ===============================================================

        /// <adversarial category="state" severity="high" />
        /// <summary>リネームでArrow区切りの後にさらにArrowがある場合の挙動</summary>
        [Fact]
        public void Change_Set_Renamed_MultipleArrows_SplitsOnlyFirst()
        {
            // "a -> b -> c" → Split(" -> ", 2) = ["a", "b -> c"]
            var change = new Change { Path = "a -> b -> c" };
            change.Set(ChangeState.Renamed);
            Assert.Equal("a", change.OriginalPath);
            Assert.Equal("b -> c", change.Path);
        }

        // ===============================================================
        // 🌪️ 環境異常（Environmental Chaos）
        // ===============================================================

        /// <adversarial category="chaos" severity="medium" />
        /// <summary>日本語・中国語・韓国語のファイル名を正しくパースすること</summary>
        [Theory]
        [InlineData("?? テスト/ファイル.txt")]
        [InlineData("?? 测试/文件.txt")]
        [InlineData("?? 테스트/파일.txt")]
        [InlineData("?? données/résumé.txt")]
        public void ParseLine_NonLatinPaths_ParsesCorrectly(string line)
        {
            var result = QueryLocalChanges.ParseLine(line);
            Assert.NotNull(result);
            Assert.Equal(ChangeState.Untracked, result.WorkTree);
        }

        /// <adversarial category="chaos" severity="low" />
        /// <summary>git statusの出力で先頭に余分な空白がある場合</summary>
        [Fact]
        public void ParseLine_ExtraLeadingWhitespace_ReturnsNull()
        {
            // 先頭に2つのスペースは正規のporcelainフォーマットではない
            var result = QueryLocalChanges.ParseLine("  M src/file.cs");
            // 正規表現 ^(\s?[\w\?]{1,4})\s+(.+)$ は先頭スペースを1つまでしか許容しない。
            // "  M" は先頭の \s? が1つ目の空白を消費しても、2つ目の空白が [\w\?] に当たらず、
            // \s? を空にしても先頭の空白が [\w\?] に当たらないため、マッチせず null になる。
            Assert.Null(result);
        }

        // ===============================================================
        // 💀 ReDoS耐性（Resource Exhaustion）
        // ===============================================================

        /// <adversarial category="resource" severity="high" />
        /// <summary>正規表現に対する壊滅的バックトラッキング入力でハングしないこと</summary>
        [Fact]
        public void ParseLine_ReDoSAttempt_CompletesQuickly()
        {
            // 大量のスペースとクエスチョンマークの繰り返し
            var malicious = "??" + new string(' ', 10_000) + new string('?', 10_000);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = QueryLocalChanges.ParseLine(malicious);
            sw.Stop();
            // 1秒以内に完了すること（ReDoSなら数分以上かかる）
            Assert.True(sw.ElapsedMilliseconds < 1000, $"ParseLine took {sw.ElapsedMilliseconds}ms - possible ReDoS!");
        }
    }
}
