using Komorebi.Models;

namespace Komorebi.Tests.Models;

/// <summary>
/// /rere 10 人分隊 P1#21: PII redaction の境界条件テスト。
/// マスキング漏れ・過剰マスキング・パフォーマンス劣化を防ぐ。
/// </summary>
public class LoggerRedactTests
{
    [Fact]
    public void Redact_NullOrEmpty_ReturnsSameInput()
    {
        Assert.Null(Logger.Redact(null));
        Assert.Equal(string.Empty, Logger.Redact(string.Empty));
    }

    [Fact]
    public void Redact_NormalText_Unchanged()
    {
        const string input = "branch 'feature/foo' updated to abc1234";
        Assert.Equal(input, Logger.Redact(input));
    }

    [Theory]
    [InlineData(@"C:\Users\yuro\Work\Komorebi", @"C:\Users\***\Work\Komorebi")]
    [InlineData(@"c:\users\YuroSan\desktop", @"C:\Users\***\desktop")]
    [InlineData(@"/home/yuro/repo/main.cs", "/home/***/repo/main.cs")]
    [InlineData(@"/Users/yuro/Documents", "/Users/***/Documents")]
    public void Redact_HomePath_MaskedToAsterisk(string input, string expected)
    {
        Assert.Equal(expected, Logger.Redact(input));
    }

    [Theory]
    [InlineData("https://yuro:supersecret@github.com/owner/repo.git", "https://***:***@github.com/owner/repo.git")]
    [InlineData("ssh://user:pwd@git-codecommit.us-east-1.amazonaws.com/repo", "ssh://***:***@git-codecommit.us-east-1.amazonaws.com/repo")]
    public void Redact_GitUrlWithCredentials_MaskedUserAndPassword(string input, string expected)
    {
        Assert.Equal(expected, Logger.Redact(input));
    }

    [Theory]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiJ9.foo.bar", "Bearer ***")]
    [InlineData("Authorization: Token abc123def456", "Authorization: Token ***")]
    public void Redact_BearerToken_Masked(string input, string expected)
    {
        Assert.Equal(expected, Logger.Redact(input));
    }

    [Theory]
    [InlineData("sk-1234567890abcdefghij", "sk-***")]
    [InlineData("sk-proj-AbCdEf1234567890XyZw", "sk-***")]
    public void Redact_OpenAiApiKey_Masked(string input, string expected)
    {
        Assert.Equal(expected, Logger.Redact(input));
    }

    [Theory]
    [InlineData(@"{""api_key"": ""real-secret-here""}", @"{""api_key"": ""***""}")]
    [InlineData(@"{""password"":""mypass""}", @"{""password"":""***""}")]
    public void Redact_JsonApiKey_Masked(string input, string expected)
    {
        Assert.Equal(expected, Logger.Redact(input));
    }

    [Fact]
    public void Redact_EmailAddress_LocalPartMaskedDomainKept()
    {
        Assert.Equal("Author: ***@example.com", Logger.Redact("Author: yuro@example.com"));
    }

    [Fact]
    public void Redact_MultipleSecretsInOneString_AllMasked()
    {
        const string input = @"Cloning https://yuro:t0k3n@github.com/me/repo to C:\Users\yuro\src";
        var result = Logger.Redact(input);
        Assert.DoesNotContain("yuro:t0k3n", result);
        Assert.DoesNotContain(@"C:\Users\yuro", result);
        Assert.Contains("***:***", result);
        Assert.Contains(@"C:\Users\***", result);
    }

    [Fact]
    public void Redact_DoesNotMangleSha1OrFilePath()
    {
        const string input = "commit a31223f5e9b6c4d8 in src/AI/Service.cs";
        Assert.Equal(input, Logger.Redact(input));
    }
}
