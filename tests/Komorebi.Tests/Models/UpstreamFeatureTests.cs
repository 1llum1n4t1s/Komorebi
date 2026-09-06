using Komorebi.Models;

namespace Komorebi.Tests.Models;

public class UpstreamFeatureTests
{
    [Fact]
    public void GitFlowConfiguration_PrefersNextAndClearsStaleValues()
    {
        var flow = new GitFlow();
        Dictionary<string, string> config = new(StringComparer.Ordinal)
        {
            ["gitflow.initialized"] = "true",
            ["gitflow.branch.Main.type"] = "base",
            ["gitflow.branch.Dev.type"] = "base",
            ["gitflow.branch.Dev.parent"] = "Main",
            ["gitflow.branch.feature.prefix"] = "feature/",
            ["gitflow.branch.release.prefix"] = "release/",
            ["gitflow.branch.hotfix.prefix"] = "hotfix/",
        };
        flow.Parse(config, true);
        Assert.True(flow.IsValid);
        Assert.Equal("Main", flow.Master);
        Assert.Equal("Dev", flow.Develop);

        flow.Parse([], true);
        Assert.False(flow.IsValid);
        Assert.Equal(string.Empty, flow.Master);

        config["gitflow.branch.master"] = "legacy";
        flow.Parse(config, false);
        Assert.Equal("legacy", flow.Master);
        Assert.Equal(string.Empty, flow.FeaturePrefix);
    }

    [Fact]
    public void BinaryFile_ReadsAcrossBufferBoundaryAndDeletesOwnedFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            var bytes = Enumerable.Range(0, 40000).Select(i => (byte)(i % 251)).ToArray();
            File.WriteAllBytes(path, bytes);
            using (var file = new BinaryFile(path, true))
            {
                Assert.Equal(bytes[16000..24000], file.Read(16000, 8000).ToArray());
                Assert.Equal(bytes[39995..], file.Read(39995, long.MaxValue).ToArray());
                Assert.Equal(0, file.Read(-1, 10).Count);
                Assert.Equal(0, file.Read(0, -1).Count);
                Assert.Equal(0, file.Read(long.MaxValue, 1).Count);
            }
            Assert.False(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("diff --cc file\n++<<<<<<< HEAD\n++=======\n++>>>>>>> topic", ConflictFileState.UnmergedText)]
    [InlineData("Binary files a/file and b/file differ", ConflictFileState.UnmergedBinary)]
    [InlineData("diff --cc file\n++resolved with trailing space ", ConflictFileState.Resolved)]
    public void ConflictState_DistinguishesBinaryAndIgnoresWhitespace(string output, ConflictFileState expected)
    {
        Assert.Equal(expected, Komorebi.Commands.QueryConflictFileState.Parse(output));
    }
}
