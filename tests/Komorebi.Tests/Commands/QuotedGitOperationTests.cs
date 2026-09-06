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

    [Fact]
    public async Task Pull_ResolvesQuotedRemoteAndBranchFromLocalRepository()
    {
        var target = Path.Combine(_root, "target");
        Git(_root, "clone", "--no-hardlinks", _repo, target);
        const string branch = "topic\"quoted";
        const string remote = "up\"stream";
        var sha = Git(_repo, "rev-parse", "HEAD").Trim();
        // Windowsのファイル名制限を避け、有効な引用符付き参照をpacked-refsへ用意する。
        var packed = Path.Combine(_repo, ".git", "packed-refs");
        Assert.False(File.Exists(packed));
        File.WriteAllText(packed, $"{sha} refs/heads/{branch}\n");
        // fetchマッピングを作らず、引用符付きremoteの追跡refファイル生成を避ける。
        Git(target, "config", $"remote.{remote}.url", _repo);
        var command = new Komorebi.Commands.Pull(target, remote, branch, false) { RaiseError = false };
        Assert.True(await command.RunAsync());
        Assert.Equal(sha, Git(target, "rev-parse", "FETCH_HEAD").Trim());
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
