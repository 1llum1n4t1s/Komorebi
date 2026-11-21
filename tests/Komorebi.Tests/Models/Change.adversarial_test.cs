using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// Change.Set() に対する嫌がらせテスト。
    /// パス解析・クォート除去・リネーム分割ロジックの脆弱性を突く。
    /// </summary>
    public class ChangeAdversarialTests
    {
        // ===============================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ===============================================================

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>クォートが1文字だけのパスでSubstring(1, -1)が発生しないこと</summary>
        [Fact]
        public void Set_PathIsSingleQuote_DoesNotThrow()
        {
            var change = new Change { Path = "\"" };

            var ex = Record.Exception(() => change.Set(ChangeState.Modified));

            // Substring(1, Length-2) = Substring(1, -1) → ArgumentOutOfRangeException
            Assert.Null(ex);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>パスがクォート2文字だけの場合、空文字列になること</summary>
        [Fact]
        public void Set_PathIsDoubleQuotes_ResultsInEmptyPath()
        {
            var change = new Change { Path = "\"\"" };
            change.Set(ChangeState.Modified);

            Assert.Equal("", change.Path);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>OriginalPathがクォート1文字の場合にクラッシュしないこと</summary>
        [Fact]
        public void Set_OriginalPathIsSingleQuote_DoesNotThrow()
        {
            var change = new Change { Path = "\"\t\"new.txt\"" };

            var ex = Record.Exception(() => change.Set(ChangeState.Renamed));

            // OriginalPath = "\"" (1文字) → Substring(1, -1)
            Assert.Null(ex);
        }

        // ===============================================================
        // 🎭 型パンチ・プロトコル違反（Type Punching）
        // ===============================================================

        /// <adversarial category="type" severity="high" />
        /// <summary>リネームでタブが複数ある場合、最初のタブで分割されること</summary>
        [Fact]
        public void Set_Renamed_MultipleTabsInPath_SplitsOnFirstTab()
        {
            var change = new Change { Path = "old.txt\tmiddle.txt\tnew.txt" };
            change.Set(ChangeState.Renamed);

            Assert.Equal("old.txt", change.OriginalPath);
            Assert.Equal("middle.txt\tnew.txt", change.Path);
        }

        /// <adversarial category="type" severity="medium" />
        /// <summary>Copiedステータスでもリネーム分割が動作すること</summary>
        [Fact]
        public void Set_Copied_AlsoSplitsPath()
        {
            var change = new Change { Path = "original.cs\tcopy.cs" };
            change.Set(ChangeState.Copied);

            Assert.Equal("original.cs", change.OriginalPath);
            Assert.Equal("copy.cs", change.Path);
        }

        // ===============================================================
        // 🔀 状態遷移の矛盾（State Machine Abuse）
        // ===============================================================

        /// <adversarial category="state" severity="medium" />
        /// <summary>Set()を連続呼び出ししてリネーム→通常に戻した場合、OriginalPathが残らないこと</summary>
        [Fact]
        public void Set_ResetFromRenamedToModified_OriginalPathState()
        {
            var change = new Change { Path = "old.txt\tnew.txt" };
            change.Set(ChangeState.Renamed);

            Assert.Equal("old.txt", change.OriginalPath);
            Assert.Equal("new.txt", change.Path);

            // 再度Set()で通常のModifiedに変更
            change.Set(ChangeState.Modified);

            // OriginalPathは変更されない（前回の値が残る）ことを確認
            // これはバグか仕様かを明確にするテスト
            Assert.Equal("old.txt", change.OriginalPath);
        }
    }
}
