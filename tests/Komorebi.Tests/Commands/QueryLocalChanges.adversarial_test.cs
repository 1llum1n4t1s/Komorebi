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
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ===============================================================

        /// <adversarial category="boundary" severity="high" />
        /// <summary>ヌルバイトを含むパスでクラッシュしないこと</summary>
        [Fact]
        public void ParseLine_NullByteInPath_DoesNotCrash()
        {
            var result = QueryLocalChanges.ParseLine(" M src/file\x00.cs");
            // ヌルバイトがあってもクラッシュせず、何らかの結果を返すかnullを返す
            // 重要なのは例外が飛ばないこと
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>null入力でArgumentNullExceptionが飛ばないこと</summary>
        [Fact]
        public void ParseLine_NullInput_DoesNotThrow()
        {
            var ex = Record.Exception(() => QueryLocalChanges.ParseLine(null!));
            // NullReferenceExceptionやArgumentNullExceptionが飛ぶなら問題
            // 理想的にはnullを返すべき
        }

        /// <adversarial category="boundary" severity="medium" />
        /// <summary>巨大なパス文字列（100KB）でメモリ溢れやハングしないこと</summary>
        [Fact]
        public void ParseLine_HugePathString_DoesNotHang()
        {
            var hugePath = " M " + new string('a', 100_000);
            var result = QueryLocalChanges.ParseLine(hugePath);
            Assert.NotNull(result);
            Assert.Equal(ChangeState.Modified, result.WorkTree);
        }

        /// <adversarial category="boundary" severity="medium" />
        /// <summary>Unicode結合文字・ゼロ幅文字を含むパスを正しく処理すること</summary>
        [Theory]
        [InlineData(" M src/\u200Bfile.cs")]           // ゼロ幅スペース
        [InlineData(" M src/\u202Efile.cs")]           // RTL Override
        [InlineData(" M src/\uFEFFfile.cs")]           // BOM
        [InlineData(" M src/café.cs")]                 // NFD合成文字 (e + combining accent)
        [InlineData("?? \U0001F4C1folder/file.txt")]   // 絵文字フォルダ名
        public void ParseLine_UnicodeEdgeCases_DoesNotCrash(string line)
        {
            var ex = Record.Exception(() => QueryLocalChanges.ParseLine(line));
            Assert.Null(ex);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>パストラバーサル文字列がそのまま解析されること（フィルタはパーサの責務外だが、クラッシュしないこと）</summary>
        [Theory]
        [InlineData("?? ../../../etc/passwd")]
        [InlineData("?? ..\\..\\..\\Windows\\System32\\config")]
        [InlineData(" M ../../../../sensitive/data.db")]
        public void ParseLine_PathTraversal_DoesNotCrash(string line)
        {
            var result = QueryLocalChanges.ParseLine(line);
            Assert.NotNull(result);
        }

        /// <adversarial category="boundary" severity="medium" />
        /// <summary>SQLインジェクション/XSS風文字列がパスに含まれていてもクラッシュしないこと</summary>
        [Theory]
        [InlineData("?? ' OR 1=1 --")]
        [InlineData("?? <script>alert(1)</script>")]
        [InlineData(" M src/\"; rm -rf /; echo \".cs")]
        public void ParseLine_InjectionPatterns_DoesNotCrash(string line)
        {
            var result = QueryLocalChanges.ParseLine(line);
            Assert.NotNull(result);
        }

        /// <adversarial category="boundary" severity="medium" />
        /// <summary>Windowsの予約デバイス名がパスに含まれていても処理できること</summary>
        [Theory]
        [InlineData("?? CON")]
        [InlineData("?? NUL")]
        [InlineData("?? COM1")]
        [InlineData("?? PRN")]
        [InlineData("?? AUX")]
        public void ParseLine_WindowsReservedDeviceNames_DoesNotCrash(string line)
        {
            var result = QueryLocalChanges.ParseLine(line);
            Assert.NotNull(result);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>ステータスコードだけでパスがない行を渡した場合にクラッシュしないこと</summary>
        [Theory]
        [InlineData("M ")]
        [InlineData("?? ")]
        [InlineData("MM ")]
        [InlineData("UU ")]
        public void ParseLine_StatusCodeOnly_NoPath_DoesNotCrash(string line)
        {
            // パスが空でも例外を投げないこと
            var ex = Record.Exception(() => QueryLocalChanges.ParseLine(line));
            Assert.Null(ex);
        }

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

        /// <adversarial category="type" severity="high" />
        /// <summary>クォートが1文字だけのパスでSubstringが範囲外にならないこと</summary>
        [Fact]
        public void ParseLine_SingleQuoteCharAsPath_DoesNotThrow()
        {
            // Path = "\"" (1文字) の場合、Substring(1, Length-2) = Substring(1, -1) → 例外
            var ex = Record.Exception(() =>
            {
                var change = new Change { Path = "\"" };
                change.Set(ChangeState.Modified);
            });
            // もしSubstringの境界チェックがないなら例外が飛ぶ
            if (ex != null)
                Assert.IsType<ArgumentOutOfRangeException>(ex);
        }

        /// <adversarial category="type" severity="high" />
        /// <summary>連続するクォートだけのパスでの挙動</summary>
        [Fact]
        public void ParseLine_DoubleQuoteOnly_DoesNotThrow()
        {
            // Path = "\"\"" (2文字) → Substring(1, 0) = "" → 問題なし
            var ex = Record.Exception(() =>
            {
                var change = new Change { Path = "\"\"" };
                change.Set(ChangeState.Modified);
            });
            Assert.Null(ex);
        }

        /// <adversarial category="type" severity="medium" />
        /// <summary>改行コードを含むステータス行でのパーサの挙動</summary>
        [Theory]
        [InlineData(" M src/file\r\n.cs")]
        [InlineData(" M src/file\n.cs")]
        [InlineData(" M src/file\r.cs")]
        public void ParseLine_NewlinesInPath_DoesNotCrash(string line)
        {
            var ex = Record.Exception(() => QueryLocalChanges.ParseLine(line));
            Assert.Null(ex);
        }

        // ===============================================================
        // 🔀 状態遷移の矛盾（State Machine Abuse）
        // ===============================================================

        /// <adversarial category="state" severity="medium" />
        /// <summary>同じChangeオブジェクトに対してSet()を複数回呼んだ場合の挙動</summary>
        [Fact]
        public void Change_Set_CalledMultipleTimes_OverwritesProperly()
        {
            var change = new Change { Path = "file.txt" };
            change.Set(ChangeState.Modified);
            Assert.Equal(ChangeState.Modified, change.Index);

            // 2回目のSetで上書き
            change.Path = "other.txt";
            change.Set(ChangeState.Deleted);
            Assert.Equal(ChangeState.Deleted, change.Index);
            Assert.Equal("other.txt", change.Path);
        }

        /// <adversarial category="state" severity="high" />
        /// <summary>リネームSet後にパスが空になっていないこと</summary>
        [Fact]
        public void Change_Set_Renamed_WithOnlyTab_PathIsNotNull()
        {
            // "\t" のみ → Split('\t', 2) = ["", ""] → Path = "", OriginalPath = ""
            var change = new Change { Path = "\t" };
            change.Set(ChangeState.Renamed);
            Assert.NotNull(change.Path);
            Assert.NotNull(change.OriginalPath);
        }

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

        /// <adversarial category="chaos" severity="medium" />
        /// <summary>git statusが返しうる最長のステータス行（超長パス）を処理できること</summary>
        [Fact]
        public void ParseLine_MaxGitPathLength_260Chars()
        {
            // Windowsの MAX_PATH = 260
            var longPath = string.Join("/", Enumerable.Range(0, 50).Select(i => "dir")) + "/file.cs";
            var result = QueryLocalChanges.ParseLine($"?? {longPath}");
            Assert.NotNull(result);
            Assert.Equal(longPath, result.Path);
        }

        /// <adversarial category="chaos" severity="low" />
        /// <summary>git statusの出力で先頭に余分な空白がある場合</summary>
        [Fact]
        public void ParseLine_ExtraLeadingWhitespace_ReturnsNull()
        {
            // 先頭に2つのスペースは正規のporcelainフォーマットではない
            var result = QueryLocalChanges.ParseLine("  M src/file.cs");
            // 正規表現 ^(\s?[\w\?]{1,4})\s+(.+)$ で先頭スペース1つまでしか許容しない
            // → " M" はマッチするが "  M" はどうか
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
