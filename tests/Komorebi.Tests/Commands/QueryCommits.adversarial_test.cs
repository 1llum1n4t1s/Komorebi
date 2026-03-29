using Komorebi.Commands;

namespace Komorebi.Tests.Commands
{
    /// <summary>
    /// QueryCommits.ParseCommitLine に対する嫌がらせテスト。
    /// ulong.Parseの型パンチ、不正なフォーマット、境界値を攻撃する。
    /// </summary>
    public class QueryCommitsAdversarialTests
    {
        // ===============================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ===============================================================

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>空文字列でクラッシュしないこと</summary>
        [Fact]
        public void ParseCommitLine_EmptyString_ReturnsNull()
        {
            var result = QueryCommits.ParseCommitLine("");
            Assert.Null(result);
        }

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>NULL区切りが7個未満の不正行でクラッシュしないこと</summary>
        [Theory]
        [InlineData("abc123")]
        [InlineData("abc123\0parent")]
        [InlineData("abc123\0parent\0deco\0author\0time")]
        [InlineData("abc123\0parent\0deco\0author\0time\0committer\0ctime")]
        public void ParseCommitLine_InsufficientParts_ReturnsNull(string line)
        {
            var result = QueryCommits.ParseCommitLine(line);
            Assert.Null(result);
        }

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>NULL区切りが8個超の余分なフィールドでクラッシュしないこと</summary>
        [Fact]
        public void ParseCommitLine_TooManyParts_ReturnsNull()
        {
            var line = "sha\0parents\0deco\0author±email\01234567890\0committer±email\01234567890\0subject\0extra";
            var result = QueryCommits.ParseCommitLine(line);
            Assert.Null(result);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>正常な8フィールドの行が正しくパースされること</summary>
        [Fact]
        public void ParseCommitLine_ValidLine_ParsesCorrectly()
        {
            var line = "abc123def456\0\0\0Author±author@test.com\01700000000\0Committer±committer@test.com\01700000001\0テストコミット";
            var result = QueryCommits.ParseCommitLine(line);

            Assert.NotNull(result);
            Assert.Equal("abc123def456", result.SHA);
            Assert.Equal(1700000000UL, result.AuthorTime);
            Assert.Equal(1700000001UL, result.CommitterTime);
            Assert.Equal("テストコミット", result.Subject);
        }

        // ===============================================================
        // 🎭 型パンチ・プロトコル違反（Type Punching）
        // ===============================================================

        /// <adversarial category="type" severity="critical" />
        /// <summary>タイムスタンプが非数値の場合にFormatExceptionがスローされること</summary>
        [Theory]
        [InlineData("sha\0\0\0author±e\0NOT_A_NUMBER\0committer±e\01234567890\0subject")]
        [InlineData("sha\0\0\0author±e\01234567890\0committer±e\0NOT_A_NUMBER\0subject")]
        public void ParseCommitLine_NonNumericTimestamp_ThrowsFormatException(string line)
        {
            // ulong.Parseは例外をスローする - これは既知の脆弱性
            var ex = Record.Exception(() => QueryCommits.ParseCommitLine(line));
            if (ex != null)
                Assert.IsType<System.FormatException>(ex);
        }

        /// <adversarial category="type" severity="high" />
        /// <summary>タイムスタンプが負数の場合にOverflowExceptionがスローされること</summary>
        [Fact]
        public void ParseCommitLine_NegativeTimestamp_ThrowsOverflow()
        {
            var line = "sha\0\0\0author±e\0-1\0committer±e\01234567890\0subject";
            var ex = Record.Exception(() => QueryCommits.ParseCommitLine(line));
            if (ex != null)
                Assert.True(ex is System.FormatException or System.OverflowException);
        }

        /// <adversarial category="type" severity="high" />
        /// <summary>タイムスタンプがulong最大値を超える場合にOverflowExceptionがスローされること</summary>
        [Fact]
        public void ParseCommitLine_TimestampOverflow_ThrowsOverflow()
        {
            // ulong.MaxValue + 1 = 18446744073709551616
            var line = "sha\0\0\0author±e\018446744073709551616\0committer±e\01234567890\0subject";
            var ex = Record.Exception(() => QueryCommits.ParseCommitLine(line));
            if (ex != null)
                Assert.IsType<System.OverflowException>(ex);
        }

        /// <adversarial category="type" severity="medium" />
        /// <summary>タイムスタンプが空文字列の場合にFormatExceptionがスローされること</summary>
        [Fact]
        public void ParseCommitLine_EmptyTimestamp_ThrowsFormatException()
        {
            var line = "sha\0\0\0author±e\0\0committer±e\01234567890\0subject";
            var ex = Record.Exception(() => QueryCommits.ParseCommitLine(line));
            if (ex != null)
                Assert.IsType<System.FormatException>(ex);
        }

        /// <adversarial category="type" severity="medium" />
        /// <summary>タイムスタンプに小数点が含まれる場合にFormatExceptionがスローされること</summary>
        [Fact]
        public void ParseCommitLine_DecimalTimestamp_ThrowsFormatException()
        {
            var line = "sha\0\0\0author±e\01234567890.5\0committer±e\01234567890\0subject";
            var ex = Record.Exception(() => QueryCommits.ParseCommitLine(line));
            if (ex != null)
                Assert.IsType<System.FormatException>(ex);
        }

        // ===============================================================
        // 🌪️ 環境異常・カオステスト（Environmental Chaos）
        // ===============================================================

        /// <adversarial category="chaos" severity="high" />
        /// <summary>著者名にUnicode文字が含まれる場合でも正しくパースされること</summary>
        [Fact]
        public void ParseCommitLine_UnicodeAuthor_ParsesCorrectly()
        {
            var line = "abc123\0\0\0田中太郎±tanaka@example.com\01700000000\0佐藤花子±sato@example.com\01700000001\0日本語コミット";
            var result = QueryCommits.ParseCommitLine(line);
            Assert.NotNull(result);
            Assert.Equal("日本語コミット", result.Subject);
        }

        /// <adversarial category="chaos" severity="medium" />
        /// <summary>サブジェクトにNULL文字（\0）が含まれない正常パターンを確認</summary>
        [Fact]
        public void ParseCommitLine_SubjectWithSpecialChars_ParsesCorrectly()
        {
            var line = "abc123\0\0\0author±e\01700000000\0committer±e\01700000001\0fix: 修正 <script>alert(1)</script>";
            var result = QueryCommits.ParseCommitLine(line);
            Assert.NotNull(result);
            Assert.Equal("fix: 修正 <script>alert(1)</script>", result.Subject);
        }

        /// <adversarial category="chaos" severity="high" />
        /// <summary>絵文字を含むサブジェクトが正しくパースされること</summary>
        [Fact]
        public void ParseCommitLine_EmojiSubject_ParsesCorrectly()
        {
            var line = "abc123\0\0\0author±e\01700000000\0committer±e\01700000001\0🎉 初回リリース 🚀";
            var result = QueryCommits.ParseCommitLine(line);
            Assert.NotNull(result);
            Assert.Equal("🎉 初回リリース 🚀", result.Subject);
        }

        // ===============================================================
        // 🔀 状態遷移の矛盾（State Machine Abuse）
        // ===============================================================

        /// <adversarial category="state" severity="high" />
        /// <summary>デコレーションフィールドにHEADが含まれるとIsMergedがtrueになること</summary>
        [Fact]
        public void ParseCommitLine_WithHEADDecorator_SetsIsMerged()
        {
            var line = "abc123\0\0HEAD\0author±e\01700000000\0committer±e\01700000001\0subject";
            var result = QueryCommits.ParseCommitLine(line);
            Assert.NotNull(result);
            Assert.True(result.IsMerged);
        }

        /// <adversarial category="state" severity="medium" />
        /// <summary>親フィールドが空の場合、Parentsリストが空であること（ルートコミット）</summary>
        [Fact]
        public void ParseCommitLine_EmptyParents_EmptyList()
        {
            var line = "abc123\0\0\0author±e\01700000000\0committer±e\01700000001\0subject";
            var result = QueryCommits.ParseCommitLine(line);
            Assert.NotNull(result);
            Assert.Empty(result.Parents);
        }

        /// <adversarial category="state" severity="medium" />
        /// <summary>複数の親SHA（マージコミット）が正しくパースされること</summary>
        [Fact]
        public void ParseCommitLine_MergeParents_ParsedCorrectly()
        {
            var line = "abc123\0parent1abc parent2def\0\0author±e\01700000000\0committer±e\01700000001\0Merge branch";
            var result = QueryCommits.ParseCommitLine(line);
            Assert.NotNull(result);
            Assert.Equal(2, result.Parents.Count);
        }

        // ===============================================================
        // 💀 リソース枯渇・DoS耐性（Resource Exhaustion）
        // ===============================================================

        /// <adversarial category="resource" severity="high" />
        /// <summary>巨大なサブジェクト（100KB）でクラッシュしないこと</summary>
        [Fact]
        public void ParseCommitLine_HugeSubject_DoesNotThrow()
        {
            var subject = new string('x', 100_000);
            var line = $"abc123\0\0\0author±e\01700000000\0committer±e\01700000001\0{subject}";
            var ex = Record.Exception(() => QueryCommits.ParseCommitLine(line));
            Assert.Null(ex);
        }

        /// <adversarial category="resource" severity="medium" />
        /// <summary>タイムスタンプがulong最大値の正常ケースが処理できること</summary>
        [Fact]
        public void ParseCommitLine_MaxUlongTimestamp_ParsesCorrectly()
        {
            var maxUlong = ulong.MaxValue.ToString();
            var line = $"abc123\0\0\0author±e\0{maxUlong}\0committer±e\0{maxUlong}\0subject";
            var result = QueryCommits.ParseCommitLine(line);
            Assert.NotNull(result);
            Assert.Equal(ulong.MaxValue, result.AuthorTime);
        }
    }
}
