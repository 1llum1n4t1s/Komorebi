using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;

namespace Komorebi.Models
{
    /// <summary>
    ///     Gitリモートリポジトリの情報を保持するクラス。
    ///     URLの検証、訪問URL・PR作成URLの生成を行う。
    /// </summary>
    public partial class Remote
    {
        /// <summary>HTTPS形式のURL検証正規表現</summary>
        [GeneratedRegex(@"^https?://[^/]+/.+[^/\.]$")]
        private static partial Regex REG_HTTPS();

        /// <summary>git://プロトコル形式のURL検証正規表現</summary>
        [GeneratedRegex(@"^git://[^/]+/.+[^/\.]$")]
        private static partial Regex REG_GIT();

        /// <summary>SSH形式（user@host:path）のURL検証正規表現</summary>
        [GeneratedRegex(@"^[\w\-]+@[\w\.\-]+(\:[0-9]+)?:([a-zA-z0-9~%][\w\-\./~%]*)?[a-zA-Z0-9](\.git)?$")]
        private static partial Regex REG_SSH1();

        /// <summary>SSH形式（ssh://）のURL検証正規表現</summary>
        [GeneratedRegex(@"^ssh://([\w\-]+@)?[\w\.\-]+(\:[0-9]+)?/([a-zA-z0-9~%][\w\-\./~%]*)?[a-zA-Z0-9](\.git)?$")]
        private static partial Regex REG_SSH2();

        /// <summary>git@形式からホストとパスを抽出する正規表現</summary>
        [GeneratedRegex(@"^git@([\w\.\-]+):([\w\.\-/~%]+/[\w\-\.%]+)\.git$")]
        private static partial Regex REG_TO_VISIT_URL_CAPTURE();

        /// <summary>サポートするURL形式の正規表現一覧</summary>
        private static readonly Regex[] URL_FORMATS = [
            REG_HTTPS(),
            REG_GIT(),
            REG_SSH1(),
            REG_SSH2(),
        ];

        /// <summary>リモート名</summary>
        public string Name { get; set; }
        /// <summary>リモートURL</summary>
        public string URL { get; set; }

        /// <summary>
        ///     指定URLがSSH形式かどうかを判定する
        /// </summary>
        /// <param name="url">検証対象のURL</param>
        /// <returns>SSH形式の場合true</returns>
        public static bool IsSSH(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (REG_SSH1().IsMatch(url))
                return true;

            return REG_SSH2().IsMatch(url);
        }

        /// <summary>
        ///     指定URLが有効なGitリモートURL形式かどうかを判定する
        /// </summary>
        /// <param name="url">検証対象のURL</param>
        /// <returns>有効なURL形式の場合true</returns>
        public static bool IsValidURL(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            foreach (var fmt in URL_FORMATS)
            {
                if (fmt.IsMatch(url))
                    return true;
            }

            return url.StartsWith("file://", StringComparison.Ordinal) ||
                url.StartsWith("./", StringComparison.Ordinal) ||
                url.StartsWith("../", StringComparison.Ordinal) ||
                Directory.Exists(url);
        }

        /// <summary>
        ///     リモートURLからブラウザで訪問可能なHTTP(S) URLを生成する
        /// </summary>
        /// <param name="url">生成された訪問URL</param>
        /// <returns>URL生成に成功した場合true</returns>
        public bool TryGetVisitURL(out string url)
        {
            url = null;

            if (URL.StartsWith("http", StringComparison.Ordinal))
            {
                try
                {
                    var uri = new Uri(URL.EndsWith(".git", StringComparison.Ordinal) ? URL.Substring(0, URL.Length - 4) : URL);
                    if (uri.Port != 80 && uri.Port != 443)
                        url = $"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.LocalPath}";
                    else
                        url = $"{uri.Scheme}://{uri.Host}{uri.LocalPath}";

                    return true;
                }
                catch (UriFormatException)
                {
                    return false;
                }
            }

            var match = REG_TO_VISIT_URL_CAPTURE().Match(URL);
            if (match.Success)
            {
                var host = match.Groups[1].Value;
                var supportHTTPS = HTTPSValidator.IsSupported(host);
                var scheme = supportHTTPS ? "https" : "http";
                url = $"{scheme}://{host}/{match.Groups[2].Value}";
                return true;
            }

            return false;
        }

        /// <summary>
        ///     各ホスティングサービスに応じたPR作成URLを生成する。
        ///     GitHub、GitLab、Gitee、Bitbucket、Gitea、Azure DevOpsに対応。
        /// </summary>
        /// <param name="url">生成されたPR作成URL</param>
        /// <param name="mergeBranch">マージ元のブランチ名</param>
        /// <returns>URL生成に成功した場合true</returns>
        public bool TryGetCreatePullRequestURL(out string url, string mergeBranch)
        {
            url = null;

            if (!TryGetVisitURL(out var baseURL))
                return false;

            var uri = new Uri(baseURL);
            var host = uri.Host;
            var route = uri.AbsolutePath.TrimStart('/');
            var encodedBranch = HttpUtility.UrlEncode(mergeBranch);

            if (host.Contains("github.com", StringComparison.Ordinal))
            {
                url = $"{baseURL}/compare/{encodedBranch}?expand=1";
                return true;
            }

            if (host.Contains("gitlab", StringComparison.Ordinal))
            {
                url = $"{baseURL}/-/merge_requests/new?merge_request%5Bsource_branch%5D={encodedBranch}";
                return true;
            }

            if (host.Equals("gitee.com", StringComparison.Ordinal))
            {
                url = $"{baseURL}/pulls/new?source={encodedBranch}";
                return true;
            }

            if (host.Equals("bitbucket.org", StringComparison.Ordinal))
            {
                url = $"{baseURL}/pull-requests/new?source={encodedBranch}";
                return true;
            }

            if (host.Equals("gitea.org", StringComparison.Ordinal))
            {
                url = $"{baseURL}/compare/{encodedBranch}";
                return true;
            }

            if (host.Contains("azure.com", StringComparison.Ordinal) ||
                host.Contains("visualstudio.com", StringComparison.Ordinal))
            {
                url = $"{baseURL}/pullrequestcreate?sourceRef={encodedBranch}";
                return true;
            }

            return false;
        }
    }
}
