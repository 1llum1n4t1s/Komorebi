using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class RemoteTests
    {
        #region IsSSH

        [Theory]
        [InlineData("git@github.com:user/repo.git", true)]
        [InlineData("git@gitlab.com:user/repo.git", true)]
        [InlineData("ssh://git@github.com/user/repo.git", true)]
        [InlineData("ssh://git@github.com:22/user/repo.git", true)]
        [InlineData("https://github.com/user/repo.git", false)]
        [InlineData("http://github.com/user/repo", false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        public void IsSSH_DetectsSSHUrls(string url, bool expected)
        {
            Assert.Equal(expected, Remote.IsSSH(url));
        }

        [Fact]
        public void IsSSH_NullUrl_ReturnsFalse()
        {
            Assert.False(Remote.IsSSH(null!));
        }

        #endregion

        #region IsValidURL

        [Theory]
        [InlineData("https://github.com/user/repo", true)]
        [InlineData("https://github.com/user/repo.git", true)]
        [InlineData("http://github.com/user/repo", true)]
        [InlineData("git@github.com:user/repo.git", true)]
        [InlineData("ssh://git@github.com/user/repo.git", true)]
        [InlineData("git://github.com/user/repo", true)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        public void IsValidURL_ValidatesCorrectly(string url, bool expected)
        {
            Assert.Equal(expected, Remote.IsValidURL(url));
        }

        [Fact]
        public void IsValidURL_NullUrl_ReturnsFalse()
        {
            Assert.False(Remote.IsValidURL(null!));
        }

        [Fact]
        public void IsValidURL_FileProtocol_IsValid()
        {
            Assert.True(Remote.IsValidURL("file:///home/user/repo"));
        }

        [Fact]
        public void IsValidURL_RelativePath_IsValid()
        {
            Assert.True(Remote.IsValidURL("./relative/path"));
            Assert.True(Remote.IsValidURL("../parent/path"));
        }

        #endregion

        #region TryGetVisitURL - HTTPS URLs

        [Fact]
        public void TryGetVisitURL_HttpsWithGitSuffix_RemovesSuffix()
        {
            var remote = new Remote { URL = "https://github.com/user/repo.git" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://github.com/user/repo", url);
        }

        [Fact]
        public void TryGetVisitURL_HttpsWithoutGitSuffix_ReturnsAsIs()
        {
            var remote = new Remote { URL = "https://github.com/user/repo" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://github.com/user/repo", url);
        }

        [Fact]
        public void TryGetVisitURL_HttpUrl_ReturnsUrl()
        {
            var remote = new Remote { URL = "http://github.com/user/repo" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("http://github.com/user/repo", url);
        }

        [Fact]
        public void TryGetVisitURL_HttpsWithCustomPort_IncludesPort()
        {
            var remote = new Remote { URL = "https://git.example.com:8443/user/repo.git" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://git.example.com:8443/user/repo", url);
        }

        #endregion

        #region TryGetVisitURL - SSH URLs

        [Fact]
        public void TryGetVisitURL_GitAtGithub_ConvertsToHttps()
        {
            // HTTPSValidator must know the host to use https://
            HTTPSValidator.Add("github.com");
            var remote = new Remote { URL = "git@github.com:user/repo.git" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://github.com/user/repo", url);
        }

        [Fact]
        public void TryGetVisitURL_GitAtGitlab_ConvertsToHttps()
        {
            HTTPSValidator.Add("gitlab.com");
            var remote = new Remote { URL = "git@gitlab.com:user/repo.git" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://gitlab.com/user/repo", url);
        }

        [Fact]
        public void TryGetVisitURL_NonSshNonHttp_ReturnsFalse()
        {
            var remote = new Remote { URL = "file:///local/repo" };
            Assert.False(remote.TryGetVisitURL(out var url));
            Assert.Null(url);
        }

        #endregion

        #region TryGetCreatePullRequestURL

        [Fact]
        public void TryGetCreatePullRequestURL_GitHub_ReturnsCompareUrl()
        {
            var remote = new Remote { URL = "https://github.com/user/repo" };
            Assert.True(remote.TryGetCreatePullRequestURL(out var url, "feature-branch"));
            Assert.Contains("/compare/feature-branch", url);
            Assert.Contains("expand=1", url);
        }

        [Fact]
        public void TryGetCreatePullRequestURL_GitLab_ReturnsMergeRequestUrl()
        {
            var remote = new Remote { URL = "https://gitlab.com/user/repo" };
            Assert.True(remote.TryGetCreatePullRequestURL(out var url, "feature-branch"));
            Assert.Contains("/-/merge_requests/new", url);
            Assert.Contains("source_branch", url);
        }

        [Fact]
        public void TryGetCreatePullRequestURL_Gitee_ReturnsPullsUrl()
        {
            var remote = new Remote { URL = "https://gitee.com/user/repo" };
            Assert.True(remote.TryGetCreatePullRequestURL(out var url, "feature-branch"));
            Assert.Contains("/pulls/new", url);
        }

        [Fact]
        public void TryGetCreatePullRequestURL_BitBucket_ReturnsPullRequestUrl()
        {
            var remote = new Remote { URL = "https://bitbucket.org/user/repo" };
            Assert.True(remote.TryGetCreatePullRequestURL(out var url, "feature-branch"));
            Assert.Contains("/pull-requests/new", url);
        }

        [Fact]
        public void TryGetCreatePullRequestURL_UnknownHost_ReturnsFalse()
        {
            var remote = new Remote { URL = "https://unknown-host.com/user/repo" };
            Assert.False(remote.TryGetCreatePullRequestURL(out var url, "feature-branch"));
            Assert.Null(url);
        }

        [Fact]
        public void TryGetCreatePullRequestURL_FileUrl_ReturnsFalse()
        {
            var remote = new Remote { URL = "file:///local/repo" };
            Assert.False(remote.TryGetCreatePullRequestURL(out var url, "feature-branch"));
        }

        [Fact]
        public void TryGetCreatePullRequestURL_BranchWithSpecialChars_IsEncoded()
        {
            var remote = new Remote { URL = "https://github.com/user/repo" };
            Assert.True(remote.TryGetCreatePullRequestURL(out var url, "feature/my branch"));
            // URL-encoded: space -> +, / -> %2f
            Assert.DoesNotContain(" ", url);
        }

        #endregion

        #region IsSSH - CodeCommit

        [Theory]
        [InlineData("codecommit::us-east-1://my-repo", false)]
        [InlineData("codecommit::us-east-1://profile@my-repo", false)]
        [InlineData("ssh://git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo", true)]
        public void IsSSH_CodeCommit_DetectsCorrectly(string url, bool expected)
        {
            Assert.Equal(expected, Remote.IsSSH(url));
        }

        #endregion

        #region IsValidURL - CodeCommit

        [Theory]
        [InlineData("codecommit://my-repo", true)]
        [InlineData("codecommit://profile@my-repo", true)]
        [InlineData("codecommit::us-east-1://my-repo", true)]
        [InlineData("codecommit::us-east-1://profile@my-repo", true)]
        [InlineData("codecommit::ap-northeast-1://my.repo-name", true)]
        [InlineData("https://git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo", true)]
        [InlineData("https://codecommit.us-east-1.amazonaws.com/v1/repos/my-repo", true)]
        [InlineData("https://git-codecommit-fips.us-east-1.amazonaws.com/v1/repos/my-repo", true)]
        [InlineData("https://git-user@git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo", true)]
        [InlineData("https://git-codecommit.cn-north-1.amazonaws.com.cn/v1/repos/my-repo", true)]
        [InlineData("ssh://git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo", true)]
        [InlineData("ssh://APKAEIBAERJR2EXAMPLE@git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo", true)]
        [InlineData("ssh://git-codecommit.cn-north-1.amazonaws.com.cn/v1/repos/my-repo", true)]
        [InlineData("codecommit://my-repo.git", false)]
        [InlineData("codecommit::://my-repo", false)]
        [InlineData("codecommit::", false)]
        [InlineData("codecommit::us-east-1://profile@repo@extra", false)]
        [InlineData("codecommit::us-east-1://profile@", false)]
        public void IsValidURL_CodeCommit_ValidatesCorrectly(string url, bool expected)
        {
            Assert.Equal(expected, Remote.IsValidURL(url));
        }

        #endregion

        #region IsCodeCommitProtocol

        [Theory]
        [InlineData("codecommit://my-repo", true)]
        [InlineData("codecommit://profile@my-repo", true)]
        [InlineData("codecommit::us-east-1://my-repo", true)]
        [InlineData("codecommit::us-east-1://profile@my-repo", true)]
        [InlineData("https://git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo", false)]
        [InlineData("https://github.com/user/repo", false)]
        public void IsCodeCommitProtocol_DetectsCorrectly(string url, bool expected)
        {
            Assert.Equal(expected, Remote.IsCodeCommitProtocol(url));
        }

        #endregion

        #region TryParseCodeCommitHTTPS

        [Fact]
        public void TryParseCodeCommitHTTPS_ValidUrls_ExtractRegionAndRepo()
        {
            var urls = new[]
            {
                "https://git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo",
                "https://codecommit.us-east-1.amazonaws.com/v1/repos/my-repo",
                "https://git-codecommit-fips.us-east-1.amazonaws.com/v1/repos/my-repo",
                "https://git-user@git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo",
            };

            foreach (var url in urls)
            {
                Assert.True(Remote.TryParseCodeCommitHTTPS(url, out var region, out var repo));
                Assert.Equal("us-east-1", region);
                Assert.Equal("my-repo", repo);
            }
        }

        [Fact]
        public void TryParseCodeCommitHTTPS_ChinaUrl_ExtractsRegionAndRepo()
        {
            Assert.True(Remote.TryParseCodeCommitHTTPS(
                "https://git-codecommit.cn-north-1.amazonaws.com.cn/v1/repos/my-repo",
                out var region, out var repo));
            Assert.Equal("cn-north-1", region);
            Assert.Equal("my-repo", repo);
        }

        [Fact]
        public void TryParseCodeCommitHTTPS_WithGitSuffix_ExtractsCorrectly()
        {
            Assert.True(Remote.TryParseCodeCommitHTTPS(
                "https://git-codecommit.eu-west-1.amazonaws.com/v1/repos/my-repo.git",
                out var region, out var repo));
            Assert.Equal("eu-west-1", region);
            Assert.Equal("my-repo", repo);
        }

        [Fact]
        public void TryParseCodeCommitHTTPS_NonCodeCommitUrl_ReturnsFalse()
        {
            Assert.False(Remote.TryParseCodeCommitHTTPS(
                "https://github.com/user/repo", out _, out _));
        }

        #endregion

        #region TryParseCodeCommitGRC

        [Fact]
        public void TryParseCodeCommitGRC_DefaultRegion_ExtractsProfileAndRepo()
        {
            Assert.True(Remote.TryParseCodeCommitGRC(
                "codecommit://CodeCommitProfile@MyDemoRepo",
                out var region, out var profile, out var repo));
            Assert.Equal("", region);
            Assert.Equal("CodeCommitProfile", profile);
            Assert.Equal("MyDemoRepo", repo);
        }

        [Fact]
        public void TryParseCodeCommitGRC_DefaultRegionWithoutProfile_ExtractsRepo()
        {
            Assert.True(Remote.TryParseCodeCommitGRC(
                "codecommit://MyDemoRepo",
                out var region, out var profile, out var repo));
            Assert.Equal("", region);
            Assert.Equal("", profile);
            Assert.Equal("MyDemoRepo", repo);
        }

        [Fact]
        public void TryParseCodeCommitGRC_WithProfile_ExtractsAll()
        {
            Assert.True(Remote.TryParseCodeCommitGRC(
                "codecommit::us-east-1://myprofile@my-repo",
                out var region, out var profile, out var repo));
            Assert.Equal("us-east-1", region);
            Assert.Equal("myprofile", profile);
            Assert.Equal("my-repo", repo);
        }

        [Fact]
        public void TryParseCodeCommitGRC_WithoutProfile_ExtractsRegionAndRepo()
        {
            Assert.True(Remote.TryParseCodeCommitGRC(
                "codecommit::ap-northeast-1://my-repo",
                out var region, out var profile, out var repo));
            Assert.Equal("ap-northeast-1", region);
            Assert.Equal("", profile);
            Assert.Equal("my-repo", repo);
        }

        #endregion

        #region TryParseCodeCommitSSH

        [Fact]
        public void TryParseCodeCommitSSH_ValidUrls_ExtractRegionAndRepo()
        {
            var urls = new[]
            {
                "ssh://git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo",
                "ssh://APKAEIBAERJR2EXAMPLE@git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo",
            };

            foreach (var url in urls)
            {
                Assert.True(Remote.TryParseCodeCommitSSH(url, out var region, out var repo));
                Assert.Equal("us-east-1", region);
                Assert.Equal("my-repo", repo);
            }
        }

        [Fact]
        public void TryParseCodeCommitSSH_ChinaUrl_ExtractsRegionAndRepo()
        {
            Assert.True(Remote.TryParseCodeCommitSSH(
                "ssh://git-codecommit.cn-north-1.amazonaws.com.cn/v1/repos/my-repo",
                out var region, out var repo));
            Assert.Equal("cn-north-1", region);
            Assert.Equal("my-repo", repo);
        }

        [Fact]
        public void TryParseCodeCommitSSH_NonCodeCommitUrl_ReturnsFalse()
        {
            Assert.False(Remote.TryParseCodeCommitSSH(
                "ssh://git@github.com/user/repo.git", out _, out _));
        }

        #endregion

        #region TryGetVisitURL - CodeCommit

        [Fact]
        public void TryGetVisitURL_CodeCommitHTTPS_ReturnsConsoleUrl()
        {
            var remote = new Remote { URL = "https://git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://us-east-1.console.aws.amazon.com/codesuite/codecommit/repositories/my-repo/browse", url);
        }

        [Fact]
        public void TryGetVisitURL_CodeCommitHTTPSFromAwsCli_ReturnsConsoleUrl()
        {
            var remote = new Remote { URL = "https://codecommit.us-east-1.amazonaws.com/v1/repos/my-repo" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://us-east-1.console.aws.amazon.com/codesuite/codecommit/repositories/my-repo/browse", url);
        }

        [Fact]
        public void TryGetVisitURL_CodeCommitFipsHTTPS_ReturnsConsoleUrl()
        {
            var remote = new Remote { URL = "https://git-codecommit-fips.us-east-1.amazonaws.com/v1/repos/my-repo" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://us-east-1.console.aws.amazon.com/codesuite/codecommit/repositories/my-repo/browse", url);
        }

        [Fact]
        public void TryGetVisitURL_CodeCommitChinaHTTPS_ReturnsChinaConsoleUrl()
        {
            var remote = new Remote { URL = "https://git-codecommit.cn-north-1.amazonaws.com.cn/v1/repos/my-repo" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://console.www.amazonaws.cn/codesuite/codecommit/repositories/my-repo/browse?region=cn-north-1", url);
        }

        [Fact]
        public void TryGetVisitURL_CodeCommitGovCloudHTTPS_ReturnsGovCloudConsoleUrl()
        {
            var remote = new Remote { URL = "https://git-codecommit.us-gov-west-1.amazonaws.com/v1/repos/my-repo" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://console.amazonaws-us-gov.com/codesuite/codecommit/repositories/my-repo/browse?region=us-gov-west-1", url);
        }

        [Fact]
        public void TryGetVisitURL_CodeCommitGRC_ReturnsConsoleUrl()
        {
            var remote = new Remote { URL = "codecommit::us-east-1://my-repo" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://us-east-1.console.aws.amazon.com/codesuite/codecommit/repositories/my-repo/browse", url);
        }

        [Fact]
        public void TryGetVisitURL_CodeCommitGRC_DefaultRegion_ReturnsGlobalConsoleUrl()
        {
            var remote = new Remote { URL = "codecommit://MyDemoRepo" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://console.aws.amazon.com/codesuite/codecommit/repositories/MyDemoRepo/browse", url);
        }

        [Fact]
        public void TryGetVisitURL_CodeCommitGRC_WithProfile_ReturnsConsoleUrl()
        {
            var remote = new Remote { URL = "codecommit::ap-northeast-1://profile@my-repo" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://ap-northeast-1.console.aws.amazon.com/codesuite/codecommit/repositories/my-repo/browse", url);
        }

        [Fact]
        public void TryGetVisitURL_CodeCommitSSH_ReturnsConsoleUrl()
        {
            var remote = new Remote { URL = "ssh://git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo" };
            Assert.True(remote.TryGetVisitURL(out var url));
            Assert.Equal("https://us-east-1.console.aws.amazon.com/codesuite/codecommit/repositories/my-repo/browse", url);
        }

        #endregion

        #region TryGetCreatePullRequestURL - CodeCommit

        [Fact]
        public void TryGetCreatePullRequestURL_CodeCommitHTTPS_ReturnsPRUrl()
        {
            var remote = new Remote { URL = "https://git-codecommit.us-east-1.amazonaws.com/v1/repos/my-repo" };
            Assert.True(remote.TryGetCreatePullRequestURL(out var url, "feature-branch"));
            Assert.Contains("pull-requests/new", url);
            Assert.Contains("feature-branch", url);
        }

        [Fact]
        public void TryGetCreatePullRequestURL_CodeCommitChinaHTTPS_PreservesRegionQuery()
        {
            var remote = new Remote { URL = "https://git-codecommit.cn-north-1.amazonaws.com.cn/v1/repos/my-repo" };
            Assert.True(remote.TryGetCreatePullRequestURL(out var url, "feature/my branch"));
            Assert.Equal("https://console.www.amazonaws.cn/codesuite/codecommit/repositories/my-repo/pull-requests/new/refs/heads/feature%2fmy+branch?region=cn-north-1", url);
        }

        [Fact]
        public void TryParseCodeCommitConsoleURL_ValidChinaUrl_ExtractsRootBeforeQuery()
        {
            var uri = new Uri("https://console.www.amazonaws.cn/codesuite/codecommit/repositories/my-repo/browse?region=cn-north-1");
            Assert.True(Remote.TryParseCodeCommitConsoleURL(uri, out var repo, out var root, out var query));
            Assert.Equal("my-repo", repo);
            Assert.Equal("https://console.www.amazonaws.cn/codesuite/codecommit/repositories/my-repo", root);
            Assert.Equal("?region=cn-north-1", query);
        }

        [Theory]
        [InlineData("https://console.aws.amazon.com/cloudwatch/home")]
        [InlineData("https://console.aws.amazon.com/codesuite/codecommit/not-repositories/my-repo/browse")]
        [InlineData("https://console.aws.amazon.com/codesuite/codecommit/repositories/my-repo/settings")]
        public void TryParseCodeCommitConsoleURL_NonCodeCommitRepositoryRoutes_ReturnsFalse(string url)
        {
            Assert.False(Remote.TryParseCodeCommitConsoleURL(new Uri(url), out _, out _, out _));
        }

        [Fact]
        public void CommitLink_Get_CodeCommitChina_PutsRegionQueryAfterSha()
        {
            var remote = new Remote { URL = "https://git-codecommit.cn-north-1.amazonaws.com.cn/v1/repos/my-repo" };
            var link = CommitLink.Get([remote]).Single();

            Assert.Equal("CodeCommit (my-repo)", link.Name);
            Assert.Equal("https://console.www.amazonaws.cn/codesuite/codecommit/repositories/my-repo/commit/abcdef?region=cn-north-1", link.GetURL("abcdef"));
        }

        [Fact]
        public void TryGetCodeCommitRepositoryName_OfficialFormats_ExtractsRepoName()
        {
            var urls = new[]
            {
                "codecommit://MyDemoRepo",
                "codecommit://CodeCommitProfile@MyDemoRepo",
                "codecommit::ap-northeast-1://CodeCommitProfile@MyDemoRepo",
                "https://codecommit.us-east-1.amazonaws.com/v1/repos/MyDemoRepo",
                "https://git-codecommit.cn-north-1.amazonaws.com.cn/v1/repos/MyDemoRepo",
                "ssh://APKAEIBAERJR2EXAMPLE@git-codecommit.us-east-1.amazonaws.com/v1/repos/MyDemoRepo",
            };

            foreach (var url in urls)
            {
                Assert.True(Remote.TryGetCodeCommitRepositoryName(url, out var repo));
                Assert.Equal("MyDemoRepo", repo);
            }
        }

        [Fact]
        public void TryGetCreatePullRequestURL_CodeCommitGRC_ReturnsPRUrl()
        {
            var remote = new Remote { URL = "codecommit::us-east-1://my-repo" };
            Assert.True(remote.TryGetCreatePullRequestURL(out var url, "feature-branch"));
            Assert.Contains("pull-requests/new", url);
            Assert.Contains("feature-branch", url);
        }

        #endregion
    }
}
