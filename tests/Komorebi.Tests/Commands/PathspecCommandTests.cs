using Komorebi.Commands;

namespace Komorebi.Tests.Commands;

public class PathspecCommandTests
{
    [Fact]
    public void PathspecCommands_UseNullTerminatedInput()
    {
        Assert.Contains("--pathspec-file-nul", new Add("repo", "paths").Args);
        Assert.Contains("--pathspec-file-nul", new Reset("repo", "paths").Args);
        Assert.Contains("--pathspec-file-nul", new Restore("repo", "paths").Args);
    }

    [Fact]
    public async Task TempFileScope_WritesUtf8NullTerminatedPaths()
    {
        using var temp = new TempFileScope();
        await temp.WriteNullTerminatedPathsAsync(["line\nbreak.txt", "quote\"file.txt"]);

        var bytes = await File.ReadAllBytesAsync(temp.Path, TestContext.Current.CancellationToken);
        Assert.Equal("line\nbreak.txt\0quote\"file.txt\0", System.Text.Encoding.UTF8.GetString(bytes));
    }
}
