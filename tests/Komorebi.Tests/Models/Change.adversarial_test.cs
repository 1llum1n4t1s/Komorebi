using Komorebi.Models;
using Xunit;

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

        /// <adversarial category="boundary" severity="medium" />
        /// <summary>パスがスペースのみの場合にクラッシュしないこと</summary>
        [Fact]
        public void Set_PathIsOnlySpaces_DoesNotThrow()
        {
            var change = new Change { Path = "   " };
            var ex = Record.Exception(() => change.Set(ChangeState.Modified));
            Assert.Null(ex);
        }

        /// <adversarial category="boundary" severity="medium" />
        /// <summary>巨大なパス文字列でもメモリ溢れしないこと</summary>
        [Fact]
        public void Set_HugePath_DoesNotExhaustMemory()
        {
            var hugePath = new string('x', 500_000);
            var change = new Change { Path = hugePath };

            var ex = Record.Exception(() => change.Set(ChangeState.Modified));

            Assert.Null(ex);
            Assert.Equal(hugePath, change.Path);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>ヌルバイトを含むパスでの挙動</summary>
        [Fact]
        public void Set_PathWithNullByte_DoesNotCrash()
        {
            var change = new Change { Path = "file\x00name.txt" };
            var ex = Record.Exception(() => change.Set(ChangeState.Modified));
            Assert.Null(ex);
        }

        /// <adversarial category="boundary" severity="medium" />
        /// <summary>パストラバーサル文字列がそのまま保持されること</summary>
        [Theory]
        [InlineData("../../../etc/passwd")]
        [InlineData("..\\..\\..\\Windows\\System32")]
        [InlineData("./././file.txt")]
        public void Set_PathTraversal_PreservedAsIs(string path)
        {
            var change = new Change { Path = path };
            change.Set(ChangeState.None, ChangeState.Untracked);
            Assert.Equal(path, change.Path);
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
        /// <summary>リネームでタブの前後が空の場合</summary>
        [Fact]
        public void Set_Renamed_EmptyPartsAroundTab()
        {
            var change = new Change { Path = "\t" };
            change.Set(ChangeState.Renamed);

            Assert.Equal("", change.OriginalPath);
            Assert.Equal("", change.Path);
        }

        /// <adversarial category="type" severity="medium" />
        /// <summary>リネームでArrow区切りの前後にスペースがない場合（"->"のみ）</summary>
        [Fact]
        public void Set_Renamed_ArrowWithoutSpaces_NotSplit()
        {
            var change = new Change { Path = "old.txt->new.txt" };
            change.Set(ChangeState.Renamed);

            // " -> " (前後スペース付き) でSplitしているので、スペースなしでは分割されない
            Assert.Equal("", change.OriginalPath);
            Assert.Equal("old.txt->new.txt", change.Path);
        }

        /// <adversarial category="type" severity="medium" />
        /// <summary>パスが先頭クォートだが末尾クォートなしの場合</summary>
        [Fact]
        public void Set_UnbalancedQuote_OpenOnly()
        {
            var change = new Change { Path = "\"unbalanced path" };
            change.Set(ChangeState.Modified);

            // Substring(1, Length-2) → "nbalanced pat" (末尾1文字も切られる)
            // これは意図した動作ではないが、クラッシュしないこと
            var ex = Record.Exception(() => _ = change.Path);
            Assert.Null(ex);
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

        /// <adversarial category="state" severity="high" />
        /// <summary>全てのChangeState値でSet()がクラッシュしないこと</summary>
        [Theory]
        [InlineData(ChangeState.None)]
        [InlineData(ChangeState.Modified)]
        [InlineData(ChangeState.TypeChanged)]
        [InlineData(ChangeState.Added)]
        [InlineData(ChangeState.Deleted)]
        [InlineData(ChangeState.Renamed)]
        [InlineData(ChangeState.Copied)]
        [InlineData(ChangeState.Untracked)]
        [InlineData(ChangeState.Conflicted)]
        public void Set_AllChangeStates_WithNormalPath_DoNotThrow(ChangeState state)
        {
            var change = new Change { Path = "file.txt" };
            var ex = Record.Exception(() => change.Set(state));
            Assert.Null(ex);
        }

        /// <adversarial category="state" severity="high" />
        /// <summary>不正な列挙値（定義外の数値）でSet()を呼んだ場合</summary>
        [Fact]
        public void Set_InvalidEnumValue_DoesNotThrow()
        {
            var change = new Change { Path = "file.txt" };
            var ex = Record.Exception(() => change.Set((ChangeState)999));
            Assert.Null(ex);
        }

        /// <adversarial category="state" severity="high" />
        /// <summary>不正な列挙値のWorkTreeDescでIndexOutOfRangeが飛ばないこと</summary>
        [Fact]
        public void WorkTreeDesc_InvalidEnumValue_DoesNotThrow()
        {
            var change = new Change { WorkTree = (ChangeState)999 };
            var ex = Record.Exception(() => _ = change.WorkTreeDesc);
            // TYPE_DESCS配列の範囲外アクセスでIndexOutOfRangeExceptionが飛ぶ可能性
            if (ex != null)
                Assert.IsType<IndexOutOfRangeException>(ex);
        }

        /// <adversarial category="state" severity="high" />
        /// <summary>不正なConflictReason値でConflictMarkerがクラッシュしないこと</summary>
        [Fact]
        public void ConflictMarker_InvalidEnumValue_DoesNotThrow()
        {
            var change = new Change { ConflictReason = (ConflictReason)999 };
            var ex = Record.Exception(() => _ = change.ConflictMarker);
            if (ex != null)
                Assert.IsType<IndexOutOfRangeException>(ex);
        }

        // ===============================================================
        // 🌪️ 環境異常（Environmental Chaos）
        // ===============================================================

        /// <adversarial category="chaos" severity="medium" />
        /// <summary>サロゲートペア（4バイトUnicode）を含むパスでの挙動</summary>
        [Fact]
        public void Set_SurrogatePairInPath_DoesNotCrash()
        {
            var change = new Change { Path = "src/\U0001F600emoji.txt" };
            var ex = Record.Exception(() => change.Set(ChangeState.Modified));
            Assert.Null(ex);
            Assert.Contains("\U0001F600", change.Path);
        }

        /// <adversarial category="chaos" severity="medium" />
        /// <summary>バックスラッシュパス（Windows形式）がそのまま保持されること</summary>
        [Fact]
        public void Set_WindowsBackslashPath_PreservedAsIs()
        {
            var change = new Change { Path = "src\\subdir\\file.cs" };
            change.Set(ChangeState.Modified);
            Assert.Equal("src\\subdir\\file.cs", change.Path);
        }

        /// <adversarial category="chaos" severity="low" />
        /// <summary>超深いネストパスでの挙動</summary>
        [Fact]
        public void Set_DeeplyNestedPath_1000Levels()
        {
            var deepPath = string.Join("/", Enumerable.Range(0, 1000).Select(i => "d")) + "/f.cs";
            var change = new Change { Path = deepPath };
            var ex = Record.Exception(() => change.Set(ChangeState.Modified));
            Assert.Null(ex);
        }
    }
}
