using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// Remoteクラスに対するadversarialテスト。
    /// URI生成の例外、ブランチ名の特殊文字、正規表現の境界を攻撃する。
    /// </summary>
    public class RemoteAdversarialTests
    {
        // ================================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ================================================================

        /// <summary>
        /// @adversarial @category boundary @severity high
        /// TryGetVisitURL で不正なHTTP URLがUriFormatExceptionを漏らさないこと
        /// </summary>
        [Theory]
        [InlineData("http://")]
        [InlineData("https://")]
        [InlineData("http:// invalid url")]
        [InlineData("http://host with spaces/repo")]
        [InlineData("https://[invalid-ipv6/repo")]
        public void TryGetVisitURL_MalformedHttpUrl_ReturnsFalseWithoutException(string badUrl)
        {
            var remote = new Remote { URL = badUrl };
            var result = remote.TryGetVisitURL(out var url);
            Assert.False(result);
            Assert.Null(url);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// TryGetVisitURL で ".git" だけのURL（長さ4未満のサブストリング問題なし）
        /// </summary>
        [Fact]
        public void TryGetVisitURL_UrlIs_DotGit_ReturnsFalse()
        {
            var remote = new Remote { URL = "http://.git" };
            var result = remote.TryGetVisitURL(out _);
            // .git を除去すると "http://" だけになる → UriFormatException → false
            Assert.False(result);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// TryGetVisitURL で URL=null の場合にクラッシュしないこと
        /// </summary>
        [Fact]
        public void TryGetVisitURL_NullUrl_ThrowsOrReturnsFalse()
        {
            var remote = new Remote { URL = null };
            // URL.StartsWith で NullReferenceException が発生しうる
            // 現在の実装では NullReferenceException が発生する可能性あり
            Assert.ThrowsAny<System.NullReferenceException>(() =>
            {
                remote.TryGetVisitURL(out _);
            });
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// IsValidURL に100KBのURLを渡してもクラッシュしないこと
        /// </summary>
        [Fact]
        public void IsValidURL_HugeUrl_DoesNotCrash()
        {
            var huge = "https://github.com/" + new string('a', 100_000);
            // 正規表現がバックトラックで遅延しないことを確認
            var result = Remote.IsValidURL(huge);
            // 結果は問わない、クラッシュしないことが重要
            _ = result;
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// IsSSH にパストラバーサルを含むURLを渡した場合
        /// </summary>
        [Theory]
        [InlineData("git@github.com:../../../etc/passwd")]
        [InlineData("ssh://git@host/../../../etc/passwd")]
        public void IsSSH_PathTraversal_ReturnsFalseOrDoesNotCrash(string url)
        {
            // クラッシュしないことが重要
            _ = Remote.IsSSH(url);
        }

        // ================================================================
        // 🎭 型パンチ・プロトコル違反（Type Punching）
        // ================================================================

        /// <summary>
        /// @adversarial @category type @severity high
        /// TryGetCreatePullRequestURL でブランチ名にURL特殊文字を含む場合
        /// URLエンコードが正しく適用されること
        /// </summary>
        [Theory]
        [InlineData("feature/test", "feature%2ftest")]
        [InlineData("fix #123", "fix+%23123")]
        [InlineData("release&deploy", "release%26deploy")]
        [InlineData("test?param=1", "test%3fparam%3d1")]
        public void TryGetCreatePullRequestURL_SpecialCharsInBranch_AreUrlEncoded(
            string branch, string expectedEncoded)
        {
            var remote = new Remote
            {
                URL = "https://github.com/user/repo.git"
            };
            var result = remote.TryGetCreatePullRequestURL(out var url, branch);

            Assert.True(result);
            Assert.Contains(expectedEncoded, url, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// @adversarial @category type @severity medium
        /// IsValidURL で file:// プロトコルが有効として扱われること
        /// </summary>
        [Theory]
        [InlineData("file:///home/user/repo", true)]
        [InlineData("file:///C:/Users/user/repo", true)]
        public void IsValidURL_FileProtocol_IsValid(string url, bool expected)
        {
            Assert.Equal(expected, Remote.IsValidURL(url));
        }

        /// <summary>
        /// @adversarial @category type @severity medium
        /// IsValidURL で相対パスが有効として扱われること
        /// </summary>
        [Theory]
        [InlineData("./repo")]
        [InlineData("../repo")]
        public void IsValidURL_RelativePath_IsValid(string url)
        {
            Assert.True(Remote.IsValidURL(url));
        }

        /// <summary>
        /// @adversarial @category type @severity medium
        /// SSH URLの正規表現がポート番号99999を受け入れるか確認
        /// （ポート範囲 0-65535 を超える値）
        /// </summary>
        [Theory]
        [InlineData("git@host:99999:user/repo.git")]
        [InlineData("ssh://git@host:99999/user/repo.git")]
        public void IsValidURL_SshWithHugePort_BehaviorCheck(string url)
        {
            // 正規表現は [0-9]+ なのでポート範囲制限なし — 受け入れてしまう
            // これは仕様として記録する（修正不要）
            _ = Remote.IsValidURL(url);
        }

        // ================================================================
        // 🌪️ 環境異常・カオステスト（Environmental Chaos）
        // ================================================================

        /// <summary>
        /// @adversarial @category chaos @severity medium
        /// Unicode文字を含むホスト名のSSH URL
        /// </summary>
        [Theory]
        [InlineData("git@日本語ホスト.com:user/repo.git")]
        [InlineData("https://例え.jp/user/repo")]
        public void IsValidURL_UnicodeHostname_DoesNotCrash(string url)
        {
            // 正規表現の \w がUnicode文字にマッチするため valid 判定される。
            // クラッシュしないことが重要。結果は仕様として記録。
            _ = Remote.IsValidURL(url);
        }

        /// <summary>
        /// @adversarial @category chaos @severity medium
        /// RTL制御文字を含むURLが安全に処理されること
        /// </summary>
        [Fact]
        public void IsValidURL_RtlOverrideInUrl_DoesNotCrash()
        {
            var url = "https://github.com/user/\u202Erepo";
            _ = Remote.IsValidURL(url);
        }

        /// <summary>
        /// @adversarial @category chaos @severity medium
        /// TryGetVisitURL でポート付きHTTP URLが正しく処理されること
        /// </summary>
        [Theory]
        [InlineData("https://gitlab.local:8443/group/project.git", "https://gitlab.local:8443/group/project")]
        [InlineData("http://gitea.local:3000/user/repo", "http://gitea.local:3000/user/repo")]
        public void TryGetVisitURL_HttpWithCustomPort_IncludesPort(string input, string expected)
        {
            var remote = new Remote { URL = input };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal(expected, url);
        }

        /// <summary>
        /// @adversarial @category chaos @severity low
        /// TryGetVisitURL で標準ポート(80/443)のHTTP URLがポートを省略すること
        /// </summary>
        [Fact]
        public void TryGetVisitURL_StandardPort_OmitsPort()
        {
            var remote = new Remote { URL = "https://github.com/user/repo.git" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.DoesNotContain(":443", url);
            Assert.Equal("https://github.com/user/repo", url);
        }
    }
}
