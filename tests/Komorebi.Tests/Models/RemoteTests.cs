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
    }
}
