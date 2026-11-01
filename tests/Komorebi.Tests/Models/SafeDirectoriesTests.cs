using Komorebi.Models;

namespace Komorebi.Tests.Models;

public class SafeDirectoriesTests
{
    [Theory]
    [InlineData("fatal: detected dubious ownership in repository", true)]
    [InlineData("fatal: unsafe repository", true)]
    [InlineData("fatal: not a git repository", false)]
    public void IsUntrustedRepository_RecognizesOnlyOwnershipErrors(string output, bool expected)
    {
        Assert.Equal(expected, SafeDirectories.IsUntrustedRepository(output));
    }

    [Theory]
    [InlineData("work space")]
    [InlineData("it's")]
    public void TryGetSafeDirectoryValue_UsesOnlyMatchingGitSuggestion(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "komorebi-trust", name);
        var expected = path.Replace(Path.DirectorySeparatorChar, '/');
        var output = "git config --global --add safe.directory '" + expected.Replace("'", "'\\''") + "'";
        Assert.True(SafeDirectories.TryGetSafeDirectoryValue(path, output, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("/unrelated/repository")]
    [InlineData("/unrelated/*")]
    public void TryGetSafeDirectoryValue_DoesNotExpandTrustFromDiagnostic(string suggestion)
    {
        var path = Path.Combine(Path.GetTempPath(), "komorebi-trust", "requested");
        Assert.True(SafeDirectories.TryGetSafeDirectoryValue(path, $"git config --global --add safe.directory '{suggestion}'", out var value));
        Assert.Equal(path.Replace(Path.DirectorySeparatorChar, '/'), value);
    }

    [Fact]
    public void TryGetSafeDirectoryValue_RejectsCommandEmbeddedInRepositoryName()
    {
        var path = Path.Combine(Path.GetTempPath(), "--add safe.directory '*");
        var output = $"fatal: detected dubious ownership in repository at '{path}'";
        Assert.True(SafeDirectories.TryGetSafeDirectoryValue(path, output, out var value));
        Assert.Equal(path.Replace(Path.DirectorySeparatorChar, '/'), value);
        Assert.NotEqual("*", value);
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("file")]
    [InlineData("bare")]
    public void TryGetSafeDirectoryValue_UsesNearestRepositoryBoundaryRegardlessOfDiagnostic(string marker)
    {
        var outer = Path.Combine(Path.GetTempPath(), "komorebi-trust-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(outer, ".git"));
        var root = Path.Combine(outer, "repository");
        var child = Path.Combine(root, "nested", "directory");
        Directory.CreateDirectory(child);
        if (marker == "directory")
            Directory.CreateDirectory(Path.Combine(root, ".git"));
        else if (marker == "file")
            File.WriteAllText(Path.Combine(root, ".git"), "gitdir: ../metadata");
        else
        {
            File.WriteAllText(Path.Combine(root, "HEAD"), "ref: refs/heads/main");
            Directory.CreateDirectory(Path.Combine(root, "objects"));
            Directory.CreateDirectory(Path.Combine(root, "refs"));
        }

        foreach (var suggestion in new[] { root, outer, "*", outer.Replace('\\', '/') + "/*", "/unrelated/repository" })
        {
            var output = $"git config --global --add safe.directory '{suggestion.Replace('\\', '/')}'";
            Assert.True(SafeDirectories.TryGetSafeDirectoryValue(child, output, out var value));
            Assert.Equal(root.Replace(Path.DirectorySeparatorChar, '/'), value);
        }
    }

    [Fact]
    public void TryGetSafeDirectoryValue_NormalizesFallbackAndRejectsEmptyPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "komorebi-trust");
        Assert.True(SafeDirectories.TryGetSafeDirectoryValue(path + Path.DirectorySeparatorChar, "", out var value));
        Assert.Equal(path.Replace(Path.DirectorySeparatorChar, '/'), value);
        Assert.False(SafeDirectories.TryGetSafeDirectoryValue("", "", out _));
        Assert.False(SafeDirectories.TryGetSafeDirectoryValue(path + "/*", "", out _));
        var root = Path.GetPathRoot(path)!;
        Assert.True(SafeDirectories.TryGetSafeDirectoryValue(root, "", out value));
        Assert.Equal(root.Replace(Path.DirectorySeparatorChar, '/'), value);
    }
}
