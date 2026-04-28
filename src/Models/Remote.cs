using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;

namespace Komorebi.Models;

/// <summary>
/// Gitリモートリポジトリの情報を保持するクラス。
/// URLの検証、訪問URL・PR作成URLの生成を行う。
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

    /// <summary>AWS CodeCommit git-remote-codecommit形式のURL検証正規表現（既定リージョン/プロファイル指定/明示リージョンに対応）</summary>
    [GeneratedRegex(@"^codecommit(?:::(?<region>[\w\-]+))?://(?:(?<profile>[\w\-\.]+)@)?(?<repo>(?![\w\.\-]*\.git$)[\w\.\-]+)$")]
    private static partial Regex REG_CODECOMMIT();

    /// <summary>CodeCommit HTTPS URLからリージョンとリポジトリ名を抽出する正規表現</summary>
    [GeneratedRegex(@"^https?://(?:[^/@]+@)?(?:git-codecommit|codecommit)(?:-fips)?\.(?<region>[\w\-]+)\.amazonaws\.com(?:\.cn)?/v1/repos/(?<repo>[\w\.\-]+?)(?:\.git)?/?$")]
    private static partial Regex REG_CODECOMMIT_HTTPS();

    /// <summary>CodeCommit SSH URLからリージョンとリポジトリ名を抽出する正規表現</summary>
    [GeneratedRegex(@"^ssh://(?:[\w\-\.]+@)?git-codecommit\.(?<region>[\w\-]+)\.amazonaws\.com(?:\.cn)?/v1/repos/(?<repo>[\w\.\-]+?)(?:\.git)?/?$")]
    private static partial Regex REG_CODECOMMIT_SSH();

    [GeneratedRegex(@"^git@([\w\.\-]+):([\w\.\-/~%]+/[\w\-\.%]+)\.git$")]
    private static partial Regex REG_TO_VISIT_URL_CAPTURE();

    /// <summary>サポートするURL形式の正規表現一覧</summary>
    private static readonly Regex[] URL_FORMATS = [
        REG_HTTPS(),
        REG_GIT(),
        REG_SSH1(),
        REG_SSH2(),
        REG_CODECOMMIT(),
    ];

    /// <summary>リモート名</summary>
    public string Name { get; set; }
    /// <summary>リモートURL</summary>
    public string URL { get; set; }

    /// <summary>
    /// 指定URLがSSH形式かどうかを判定する
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

    /// <summary>codecommit:// / codecommit::region:// プロトコルのURLかどうかを判定する</summary>
    public static bool IsCodeCommitProtocol(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return REG_CODECOMMIT().IsMatch(url);
    }

    /// <summary>CodeCommitのいずれかの公式Git URL形式かどうかを判定する</summary>
    public static bool IsCodeCommitURL(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return IsCodeCommitProtocol(url) ||
            TryParseCodeCommitHTTPS(url, out _, out _) ||
            TryParseCodeCommitSSH(url, out _, out _);
    }

    /// <summary>CodeCommit HTTPS URLからリージョンとリポジトリ名を抽出する</summary>
    public static bool TryParseCodeCommitHTTPS(string url, out string region, out string repoName)
    {
        region = null;
        repoName = null;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var m = REG_CODECOMMIT_HTTPS().Match(url);
        if (!m.Success)
            return false;

        region = m.Groups["region"].Value;
        repoName = m.Groups["repo"].Value;
        return true;
    }

    /// <summary>CodeCommit SSH URLからリージョンとリポジトリ名を抽出する</summary>
    public static bool TryParseCodeCommitSSH(string url, out string region, out string repoName)
    {
        region = null;
        repoName = null;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var m = REG_CODECOMMIT_SSH().Match(url);
        if (!m.Success)
            return false;

        region = m.Groups["region"].Value;
        repoName = m.Groups["repo"].Value;
        return true;
    }

    /// <summary>codecommit:// / codecommit::region:// プロトコルURLからリージョン・プロファイル・リポジトリ名を抽出する</summary>
    public static bool TryParseCodeCommitGRC(string url, out string region, out string profile, out string repoName)
    {
        region = null;
        profile = null;
        repoName = null;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var m = REG_CODECOMMIT().Match(url);
        if (!m.Success)
            return false;

        region = m.Groups["region"].Value;
        profile = m.Groups["profile"].Value;
        repoName = m.Groups["repo"].Value;
        return true;
    }

    /// <summary>CodeCommit URLからリポジトリ名を抽出する</summary>
    public static bool TryGetCodeCommitRepositoryName(string url, out string repoName)
    {
        repoName = null;

        if (TryParseCodeCommitHTTPS(url, out _, out repoName))
            return true;

        if (TryParseCodeCommitSSH(url, out _, out repoName))
            return true;

        if (TryParseCodeCommitGRC(url, out _, out _, out repoName))
            return true;

        return false;
    }

    /// <summary>AWS CodeCommitコンソールのホストかどうかを判定する</summary>
    public static bool IsCodeCommitConsoleHost(string host)
    {
        return host.Equals("console.aws.amazon.com", StringComparison.Ordinal) ||
            host.EndsWith(".console.aws.amazon.com", StringComparison.Ordinal) ||
            host.Equals("console.www.amazonaws.cn", StringComparison.Ordinal) ||
            host.Equals("console.amazonaws-us-gov.com", StringComparison.Ordinal);
    }

    /// <summary>CodeCommitコンソールURLからリポジトリ名・リポジトリルートURL・クエリを抽出する</summary>
    public static bool TryParseCodeCommitConsoleURL(Uri uri, out string repoName, out string repoRootURL, out string query)
    {
        repoName = null;
        repoRootURL = null;
        query = null;

        if (uri is null || !IsCodeCommitConsoleHost(uri.Host))
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 5 ||
            !segments[0].Equals("codesuite", StringComparison.Ordinal) ||
            !segments[1].Equals("codecommit", StringComparison.Ordinal) ||
            !segments[2].Equals("repositories", StringComparison.Ordinal) ||
            !segments[4].Equals("browse", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(segments[3]))
            return false;

        repoName = segments[3];
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        repoRootURL = $"{uri.Scheme}://{uri.Host}{port}/codesuite/codecommit/repositories/{repoName}";
        query = uri.Query;
        return true;
    }

    /// <summary>CodeCommitコンソールのURLを生成する</summary>
    private static string MakeCodeCommitConsoleURL(string region, string repoName, string route)
    {
        var repoRoute = $"/codesuite/codecommit/repositories/{repoName}/{route}";
        if (string.IsNullOrWhiteSpace(region))
            return $"https://console.aws.amazon.com{repoRoute}";

        if (region.StartsWith("cn-", StringComparison.Ordinal))
            return $"https://console.www.amazonaws.cn{repoRoute}?region={HttpUtility.UrlEncode(region)}";

        if (region.StartsWith("us-gov-", StringComparison.Ordinal))
            return $"https://console.amazonaws-us-gov.com{repoRoute}?region={HttpUtility.UrlEncode(region)}";

        return $"https://{region}.console.aws.amazon.com{repoRoute}";
    }

    /// <summary>
    /// 指定URLが有効なGitリモートURL形式かどうかを判定する
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
    /// リモートURLからブラウザで訪問可能なHTTP(S) URLを生成する
    /// </summary>
    /// <param name="url">生成された訪問URL</param>
    /// <returns>URL生成に成功した場合true</returns>
    public bool TryGetVisitURL(out string url)
    {
        url = null;

        // CodeCommit HTTPS URL → AWSコンソールURLに変換（汎用HTTPSハンドラより前に判定）
        if (TryParseCodeCommitHTTPS(URL, out var ccRegion, out var ccRepo))
        {
            url = MakeCodeCommitConsoleURL(ccRegion, ccRepo, "browse");
            return true;
        }

        // CodeCommit SSH URL → AWSコンソールURLに変換
        if (TryParseCodeCommitSSH(URL, out var ccSshRegion, out var ccSshRepo))
        {
            url = MakeCodeCommitConsoleURL(ccSshRegion, ccSshRepo, "browse");
            return true;
        }

        // codecommit:// / codecommit::region:// プロトコル → AWSコンソールURLに変換
        if (TryParseCodeCommitGRC(URL, out var grcRegion, out _, out var grcRepo))
        {
            url = MakeCodeCommitConsoleURL(grcRegion, grcRepo, "browse");
            return true;
        }

        if (URL.StartsWith("http", StringComparison.Ordinal))
        {
            try
            {
                var uri = new Uri(URL.EndsWith(".git", StringComparison.Ordinal) ? URL[..^4] : URL);
                if (uri.Port != 80 && uri.Port != 443)
                    url = $"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.AbsolutePath}";
                else
                    url = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";

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
    /// 各ホスティングサービスに応じたPR作成URLを生成する。
    /// GitHub、GitLab、Gitee、Bitbucket、Gitea、Azure DevOpsに対応。
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
        var encodedBranch = HttpUtility.UrlEncode(mergeBranch);

        // AWS CodeCommit（AWSコンソールURL経由）
        if (TryParseCodeCommitConsoleURL(uri, out _, out var repoRootURL, out var query))
        {
            url = $"{repoRootURL}/pull-requests/new/refs/heads/{encodedBranch}{query}";
            return true;
        }

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
