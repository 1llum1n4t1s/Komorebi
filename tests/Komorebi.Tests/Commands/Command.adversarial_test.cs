// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using Komorebi.Commands;
using Komorebi.Models;

namespace Komorebi.Tests.Commands
{
    /// <summary>
    /// Command.ParseNameStatusLine に対する嫌がらせテスト。
    /// 正規表現マッチの境界値、不正入力、ReDoS を攻撃する。
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

        [Fact]
        public void RedactSensitiveText_HttpRemoteUrl_HidesToken()
        {
            var text = "clone \"https://user:secret-token@example.com/org/repo.git\"";
            var redacted = Command.RedactSensitiveText(text);

            Assert.DoesNotContain("secret-token", redacted);
            Assert.Contains("https://user:***@example.com/org/repo.git", redacted);
        }

        [Fact]
        public void CreateGitStartInfo_UsesCredentialHelperResetAndAskHostKeyChecking()
        {
            var oldHelper = Komorebi.Native.OS.CredentialHelper;
            try
            {
                Komorebi.Native.OS.CredentialHelper = "manager";
                var command = new TestableCommand { Args = "status" };
                var start = command.BuildStartInfo();

                Assert.Contains("-c credential.helper= -c credential.helper=manager", start.Arguments);
                Assert.DoesNotContain("StrictHostKeyChecking=accept-new", start.Environment["GIT_SSH_COMMAND"]);
                Assert.Contains("StrictHostKeyChecking=ask", start.Environment["GIT_SSH_COMMAND"]);
            }
            finally
            {
                Komorebi.Native.OS.CredentialHelper = oldHelper;
            }
        }

        // ===============================================================
        // 🗡️ AWS CodeCommit / GCM 抑止（DisableCredentialHelper）
        // ===============================================================

        /// <adversarial category="codecommit" severity="critical" />
        /// <summary>
        /// DisableCredentialHelper=true なら -c credential.helper= だけが残り、
        /// `Native.OS.CredentialHelper` の値（manager / libsecret 等）は付与されないこと。
        /// CodeCommit GRC / HTTPS / SSH URL では GCM を完全に外す必要がある。
        /// </summary>
        [Fact]
        public void CreateGitStartInfo_DisableCredentialHelper_DropsManagerHelper()
        {
            var oldHelper = Komorebi.Native.OS.CredentialHelper;
            try
            {
                Komorebi.Native.OS.CredentialHelper = "manager";
                var command = new TestableCommand
                {
                    Args = "fetch --progress --verbose origin",
                    DisableCredentialHelperPublic = true,
                };
                var start = command.BuildStartInfo();

                // クリア用の `-c credential.helper=` は残るが、後段の `manager` 再設定は消えること
                Assert.Contains("-c credential.helper= ", start.Arguments);
                Assert.DoesNotContain("credential.helper=manager", start.Arguments);
                Assert.DoesNotContain("credential.helper=libsecret", start.Arguments);
            }
            finally
            {
                Komorebi.Native.OS.CredentialHelper = oldHelper;
            }
        }

        /// <adversarial category="codecommit" severity="critical" />
        /// <summary>
        /// CodeCommit URL（GRC / HTTPS / SSH）を Clone コンストラクタに渡すと
        /// DisableCredentialHelper が自動で立ち、GCM 抑止構成の引数が生成されること。
        /// </summary>
        [Theory]
        [InlineData("codecommit::us-east-1://my-repo")]
        [InlineData("codecommit://my-repo")]
        [InlineData("codecommit::ap-northeast-1://profile@my-repo")]
        [InlineData("https://git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo")]
        [InlineData("ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/my-repo")]
        public void Clone_CodeCommitUrl_DisablesCredentialHelper(string url)
        {
            var oldHelper = Komorebi.Native.OS.CredentialHelper;
            try
            {
                Komorebi.Native.OS.CredentialHelper = "manager";
                var clone = new TestableClone("ctx", System.IO.Path.GetTempPath(), url, "local", string.Empty, string.Empty);
                var start = clone.BuildStartInfo();

                Assert.DoesNotContain("credential.helper=manager", start.Arguments);
            }
            finally
            {
                Komorebi.Native.OS.CredentialHelper = oldHelper;
            }
        }

        /// <adversarial category="codecommit" severity="high" />
        /// <summary>
        /// 通常の GitHub / GitLab 等の URL では Clone は credential helper を維持すること（誤検知防止）。
        /// </summary>
        [Theory]
        [InlineData("https://github.com/user/repo.git")]
        [InlineData("git@github.com:user/repo.git")]
        [InlineData("ssh://git@gitlab.com/user/repo.git")]
        [InlineData("https://example.com/codecommit-mirror/repo.git")]
        public void Clone_NonCodeCommitUrl_KeepsCredentialHelper(string url)
        {
            var oldHelper = Komorebi.Native.OS.CredentialHelper;
            try
            {
                Komorebi.Native.OS.CredentialHelper = "manager";
                var clone = new TestableClone("ctx", System.IO.Path.GetTempPath(), url, "local", string.Empty, string.Empty);
                var start = clone.BuildStartInfo();

                Assert.Contains("-c credential.helper= -c credential.helper=manager", start.Arguments);
            }
            finally
            {
                Komorebi.Native.OS.CredentialHelper = oldHelper;
            }
        }

        private sealed class TestableCommand : Command
        {
            public bool DisableCredentialHelperPublic
            {
                get => DisableCredentialHelper;
                set => DisableCredentialHelper = value;
            }

            public System.Diagnostics.ProcessStartInfo BuildStartInfo() => CreateGitStartInfo(true);
        }

        private sealed class TestableClone : Clone
        {
            public TestableClone(string ctx, string path, string url, string localName, string sshKey, string extraArgs)
                : base(ctx, path, url, localName, sshKey, extraArgs) { }

            public System.Diagnostics.ProcessStartInfo BuildStartInfo() => CreateGitStartInfo(true);
        }
    }
}
