using System.Diagnostics;

namespace Komorebi.Tests.Commands;

public class QuotedGitOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "komorebi-quoted-" + Guid.NewGuid().ToString("N"));
    private readonly string _repo;

    public QuotedGitOperationTests()
    {
        _repo = Path.Combine(_root, "source");
        Directory.CreateDirectory(_repo);
        Komorebi.Native.OS.GitExecutable = Komorebi.Native.OS.FindGitExecutable();
        Git(_repo, "init", "-b", "main");
        Git(_repo, "config", "user.name", "Test");
        Git(_repo, "config", "user.email", "test@example.invalid");
        Git(_repo, "config", "commit.gpgsign", "false");
        Git(_repo, "config", "core.hooksPath", Path.Combine(_root, "no-hooks"));
        Git(_repo, "commit", "--allow-empty", "-m", "initial");
    }

    [Fact]
    public async Task Worktree_CreatesRequestedFolderWithTrailingSeparator()
    {
        var folder = Path.Combine(_root, "work tree");
        var command = new Komorebi.Commands.Worktree(_repo) { RaiseError = false };
        Assert.True(await command.AddAsync(folder + Path.DirectorySeparatorChar, "new-branch", true, ""));
        Assert.True(Directory.Exists(folder));
        Assert.Equal("new-branch", Git(folder, "branch", "--show-current").Trim());
    }

    private static string Git(string directory, params string[] args)
    {
        var start = new ProcessStartInfo(Komorebi.Native.OS.GitExecutable) { WorkingDirectory = directory };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        return ArgumentBoundaryTests.Read(start);
    }

    public void Dispose()
    {
        // このテストが作成したGUID付きディレクトリだけを削除する。
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }
}
