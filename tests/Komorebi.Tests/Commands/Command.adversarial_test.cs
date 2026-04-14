using Komorebi.Commands;
using Komorebi.Models;

namespace Komorebi.Tests.Commands
{
    /// <summary>
    /// Command.ParseNameStatusLine に対する嫌がらせテスト。
    /// 正規表現マッチの境界値、不正入力、パストラバーサルを攻撃する。
    /// </summary>
    public class CommandAdversarialTests
    {
        // ===============================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ===============================================================

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>空文字列でnullが返ること</summary>
        [Fact]
        public void ParseNameStatusLine_EmptyString_ReturnsNull()
        {
            var result = Command.ParseNameStatusLine("");
            Assert.Null(result);
        }

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>ステータス文字だけの行でnullが返ること（パスなし）</summary>
        [Theory]
        [InlineData("M")]
        [InlineData("A")]
        [InlineData("D")]
        [InlineData("R")]
        [InlineData("C")]
        public void ParseNameStatusLine_StatusOnly_ReturnsNull(string line)
        {
            var result = Command.ParseNameStatusLine(line);
            Assert.Null(result);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>ステータス文字とスペースだけの行でマッチしないこと</summary>
        [Theory]
        [InlineData("M ")]
        [InlineData("A ")]
        [InlineData("D ")]
        public void ParseNameStatusLine_StatusAndSpaceOnly_MatchesEmptyPath(string line)
        {
            // 正規表現 (.+) は最低1文字必要なので空パスはマッチしない...はず
            // しかし末尾スペースのみの場合はどうなるか
            var result = Command.ParseNameStatusLine(line);
            // ".+" requires at least 1 char, trailing space is consumed by \s+
            // So "M " → \s+ consumes the space, (.+) has nothing → no match
            Assert.Null(result);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>ヌルバイトを含むパスでクラッシュしないこと</summary>
        [Fact]
        public void ParseNameStatusLine_PathWithNullByte_DoesNotThrow()
        {
            var ex = Record.Exception(() => Command.ParseNameStatusLine("M\tsrc/file\x00.cs"));
            Assert.Null(ex);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>巨大なパス（100KB）でパフォーマンスが劣化しないこと</summary>
        [Fact]
        public void ParseNameStatusLine_HugePath_CompletesInTime()
        {
            var path = "M\t" + new string('a', 100_000);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = Command.ParseNameStatusLine(path);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 1000, $"100KBパスの解析に{sw.ElapsedMilliseconds}msかかった");
        }

        /// <adversarial category="boundary" severity="medium" />
        /// <summary>タブ区切りのM/A/Dが正しくパースされること</summary>
        [Theory]
        [InlineData("M\tsrc/file.cs", ChangeState.Modified)]
        [InlineData("A\tsrc/new.cs", ChangeState.Added)]
        [InlineData("D\tsrc/old.cs", ChangeState.Deleted)]
        public void ParseNameStatusLine_TabSeparated_ParsesCorrectly(string line, ChangeState expected)
        {
            var result = Command.ParseNameStatusLine(line);
            Assert.NotNull(result);
            Assert.Equal(expected, result.Value.state);
        }

        // ===============================================================
        // 🎭 型パンチ・プロトコル違反（Type Punching）
        // ===============================================================

        /// <adversarial category="type" severity="high" />
        /// <summary>認識されないステータス文字でnullが返ること</summary>
        [Theory]
        [InlineData("X\tsrc/file.cs")]
        [InlineData("T\tsrc/file.cs")]
        [InlineData("U\tsrc/file.cs")]
        [InlineData("?\tsrc/file.cs")]
        [InlineData("!\tsrc/file.cs")]
        public void ParseNameStatusLine_UnknownStatus_ReturnsNull(string line)
        {
            var result = Command.ParseNameStatusLine(line);
            Assert.Null(result);
        }

        /// <adversarial category="type" severity="high" />
        /// <summary>小文字のステータス文字でnullが返ること（大文字のみ有効）</summary>
        [Theory]
        [InlineData("m\tsrc/file.cs")]
        [InlineData("a\tsrc/file.cs")]
        [InlineData("d\tsrc/file.cs")]
        [InlineData("r\tsrc/file.cs")]
        [InlineData("c\tsrc/file.cs")]
        public void ParseNameStatusLine_LowercaseStatus_ReturnsNull(string line)
        {
            var result = Command.ParseNameStatusLine(line);
            Assert.Null(result);
        }

        /// <adversarial category="type" severity="high" />
        /// <summary>R/Cのリネーム/コピーで数値パーセンテージ付きが正しくパースされること</summary>
        [Theory]
        [InlineData("R100\told/path.cs\tnew/path.cs", ChangeState.Renamed)]
        [InlineData("C075\told/path.cs\tnew/path.cs", ChangeState.Copied)]
        [InlineData("R\told/path.cs\tnew/path.cs", ChangeState.Renamed)]
        public void ParseNameStatusLine_RenameWithPercent_ParsesCorrectly(string line, ChangeState expected)
        {
            var result = Command.ParseNameStatusLine(line);
            Assert.NotNull(result);
            Assert.Equal(expected, result.Value.state);
        }

        /// <adversarial category="type" severity="medium" />
        /// <summary>R/Cで5桁以上の数値はマッチしないこと（{0,4}制限）</summary>
        [Fact]
        public void ParseNameStatusLine_RenameWith5DigitPercent_ReturnsNull()
        {
            var result = Command.ParseNameStatusLine("R12345\told/path.cs\tnew/path.cs");
            Assert.Null(result);
        }

        // ===============================================================
        // 🌪️ 環境異常・カオステスト（Environmental Chaos）
        // ===============================================================

        /// <adversarial category="chaos" severity="high" />
        /// <summary>日本語パスが正しくパースされること</summary>
        [Fact]
        public void ParseNameStatusLine_JapanesePath_ParsesCorrectly()
        {
            var result = Command.ParseNameStatusLine("M\tソース/ファイル.cs");
            Assert.NotNull(result);
            Assert.Equal("ソース/ファイル.cs", result.Value.path);
        }

        /// <adversarial category="chaos" severity="high" />
        /// <summary>絵文字を含むパスが正しくパースされること</summary>
        [Fact]
        public void ParseNameStatusLine_EmojiPath_ParsesCorrectly()
        {
            var result = Command.ParseNameStatusLine("A\t🎉/release-notes.md");
            Assert.NotNull(result);
            Assert.Equal("🎉/release-notes.md", result.Value.path);
        }

        /// <adversarial category="chaos" severity="medium" />
        /// <summary>パストラバーサルを含む入力でもパースは成功すること（サニタイズは呼び出し側の責務）</summary>
        [Theory]
        [InlineData("M\t../../../etc/passwd")]
        [InlineData("A\t..\\..\\..\\windows\\system32\\config")]
        public void ParseNameStatusLine_PathTraversal_ParsesAsIs(string line)
        {
            var result = Command.ParseNameStatusLine(line);
            Assert.NotNull(result);
            // パース自体は成功する（サニタイズは呼び出し側の責務）
        }

        /// <adversarial category="chaos" severity="medium" />
        /// <summary>Windows予約名を含むパスでクラッシュしないこと</summary>
        [Theory]
        [InlineData("A\tCON")]
        [InlineData("A\tNUL")]
        [InlineData("A\tCOM1")]
        [InlineData("A\tPRN")]
        [InlineData("A\tAUX")]
        public void ParseNameStatusLine_WindowsReservedNames_DoesNotThrow(string line)
        {
            var ex = Record.Exception(() => Command.ParseNameStatusLine(line));
            Assert.Null(ex);
        }

        // ===============================================================
        // 💀 リソース枯渇・DoS耐性（Resource Exhaustion）
        // ===============================================================

        /// <adversarial category="resource" severity="critical" />
        /// <summary>正規表現に対するReDoS攻撃パターンでタイムアウトしないこと</summary>
        [Fact]
        public void ParseNameStatusLine_ReDoSPattern_CompletesInTime()
        {
            // .+ は貪欲マッチだが、GeneratedRegexなのでバックトラッキングは限定的
            var pathological = "M " + new string(' ', 10_000) + new string('?', 10_000);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = Command.ParseNameStatusLine(pathological);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 1000, $"ReDoSパターンの処理に{sw.ElapsedMilliseconds}msかかった");
        }

        // ===============================================================
        // ⚡ 並行性・レースコンディション（Concurrency Chaos）
        // ===============================================================

        /// <adversarial category="concurrency" severity="medium" />
        /// <summary>複数スレッドから同時にParseNameStatusLineを呼んでもスレッドセーフであること</summary>
        [Fact]
        public void ParseNameStatusLine_ConcurrentCalls_ThreadSafe()
        {
            var lines = new[] { "M\tfile1.cs", "A\tfile2.cs", "D\tfile3.cs", "R100\told.cs\tnew.cs" };
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<System.Exception>();

            System.Threading.Tasks.Parallel.For(0, 1000, i =>
            {
                try
                {
                    var line = lines[i % lines.Length];
                    var result = Command.ParseNameStatusLine(line);
                    Assert.NotNull(result);
                }
                catch (System.Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
        }

        // ===============================================================
        // 🗡️ SSHキー解決の後方互換（案A 以前の "__NONE__" センチネル）
        // ===============================================================

        /// <adversarial category="compat" severity="critical" />
        /// <summary>
        /// 旧バージョンで "__NONE__" を永続化した既存ユーザーが、
        /// 新バージョンでもグローバルSSHキーを継承せず、システムデフォルトで解決されること。
        /// これを怠るとマルチアカウント環境で認証主体が勝手に変わる regression になる。
        /// </summary>
        [Fact]
        public void ResolveSSHKeyValue_LegacyNoneSentinel_SkipsGlobalFallback()
        {
            var result = Command.ResolveSSHKeyValue("__NONE__", "/home/user/.ssh/id_ed25519");
            Assert.Equal(string.Empty, result);
        }

        /// <adversarial category="compat" severity="high" />
        /// <summary>グローバルSSHキーが未設定でも "__NONE__" は空文字列を返すこと</summary>
        [Fact]
        public void ResolveSSHKeyValue_LegacyNoneSentinel_WithNullGlobal_ReturnsEmpty()
        {
            var result = Command.ResolveSSHKeyValue("__NONE__", null);
            Assert.Equal(string.Empty, result);
        }

        /// <adversarial category="compat" severity="high" />
        /// <summary>リモート個別のSSHキーが設定されていればそれが最優先になること</summary>
        [Fact]
        public void ResolveSSHKeyValue_ExplicitKey_TakesPrecedenceOverGlobal()
        {
            var result = Command.ResolveSSHKeyValue("/home/user/.ssh/work_key", "/home/user/.ssh/id_ed25519");
            Assert.Equal("/home/user/.ssh/work_key", result);
        }

        /// <adversarial category="compat" severity="high" />
        /// <summary>
        /// リモート個別設定が空/未設定で、かつグローバルSSHキーが設定されている場合は
        /// グローバルSSHキーにフォールバックすること（案A のデフォルト挙動）。
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ResolveSSHKeyValue_EmptyConfig_FallsBackToGlobal(string? configValue)
        {
            var result = Command.ResolveSSHKeyValue(configValue!, "/home/user/.ssh/id_ed25519");
            Assert.Equal("/home/user/.ssh/id_ed25519", result);
        }

        /// <adversarial category="compat" severity="medium" />
        /// <summary>両方とも未設定ならシステムデフォルト（空文字列）で解決されること</summary>
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData(null, "")]
        [InlineData("", "")]
        public void ResolveSSHKeyValue_BothEmpty_ReturnsEmpty(string? configValue, string? globalSSHKey)
        {
            var result = Command.ResolveSSHKeyValue(configValue!, globalSSHKey!);
            Assert.Equal(string.Empty, result);
        }

        /// <adversarial category="compat" severity="medium" />
        /// <summary>
        /// "__none__"（小文字）や "__NONE__ " （末尾スペース付き）のような
        /// 似た値は旧センチネルとして扱わないこと — 完全一致のみ後方互換対象。
        /// </summary>
        [Theory]
        [InlineData("__none__")]
        [InlineData("__NONE__ ")]
        [InlineData(" __NONE__")]
        [InlineData("NONE")]
        public void ResolveSSHKeyValue_NearMissOfLegacySentinel_NotTreatedAsOptOut(string configValue)
        {
            // 完全一致しないので通常の "具体パス" として扱われる → そのまま返される
            var result = Command.ResolveSSHKeyValue(configValue, "/home/user/.ssh/id_ed25519");
            Assert.Equal(configValue, result);
        }
    }
}
