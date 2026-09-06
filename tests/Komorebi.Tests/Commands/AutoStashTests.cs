using System.Diagnostics;

namespace Komorebi.Tests.Commands;

public class AutoStashTests : IDisposable
{
    private readonly string _repo = Path.Combine(Path.GetTempPath(), "komorebi-auto-stash-" + Guid.NewGuid().ToString("N"));

    public AutoStashTests()
    {
        Komorebi.Native.OS.GitExecutable = Komorebi.Native.OS.FindGitExecutable();
        Directory.CreateDirectory(_repo);
        Git("init", "-b", "main");
        Git("config", "user.name", "Komorebi Test");
        Git("config", "user.email", "test@example.invalid");
        Git("config", "commit.gpgsign", "false");
        Git("config", "core.autocrlf", "false");
        Git("config", "core.hooksPath", Path.Combine(_repo, "no-hooks"));
        File.WriteAllText(Path.Combine(_repo, "file.txt"), "base\n");
        Git("add", "file.txt");
        Git("commit", "-m", "base");
    }

    [Fact]
    public async Task FailedPull_RestoresBothIndexAndWorkingTree()
    {
        File.WriteAllText(Path.Combine(_repo, "file.txt"), "staged\n");
        Git("add", "file.txt");
        File.WriteAllText(Path.Combine(_repo, "file.txt"), "unstaged\n");
        var stash = new Komorebi.Commands.Stash(_repo) { RaiseError = false };
        Assert.True(await stash.PushAutoAsync("PULL_AUTO_STASH"));
        Assert.Empty(Git("status", "--porcelain"));
        // 存在しないローカルremoteで失敗させる。ネットワークには接続しない。
        Assert.False(await new Komorebi.Commands.Pull(_repo, Path.Combine(_repo, "missing"), "main", false)
        { RaiseError = false }.RunAsync());
        await stash.RestoreAutoAsync(Path.Combine(_repo, ".git"));
        Assert.Equal("unstaged\n", File.ReadAllText(Path.Combine(_repo, "file.txt")));
        Assert.Equal("staged", Git("show", ":file.txt"));
        Assert.Empty(Git("stash", "list"));
    }

    [Theory]
    [InlineData("MERGE_HEAD", false)]
    [InlineData("CHERRY_PICK_HEAD", false)]
    [InlineData("REVERT_HEAD", false)]
    [InlineData("rebase-merge", true)]
    [InlineData("rebase-apply", true)]
    [InlineData("sequencer", true)]
    public async Task InterruptedOperation_KeepsStashUntilOperationEnds(string marker, bool directory)
    {
        File.WriteAllText(Path.Combine(_repo, "file.txt"), "local\n");
        var stash = new Komorebi.Commands.Stash(_repo) { RaiseError = false };
        Assert.True(await stash.PushAutoAsync("PULL_AUTO_STASH"));
        var sha = Git("rev-parse", "refs/stash");
        var path = Path.Combine(_repo, ".git", marker);
        if (directory)
            Directory.CreateDirectory(path);
        else
            File.WriteAllText(path, Git("rev-parse", "HEAD"));
        await stash.RestoreAutoAsync(Path.Combine(_repo, ".git"));
        Assert.Equal(sha, Git("rev-parse", "refs/stash"));
        Assert.Equal("base\n", File.ReadAllText(Path.Combine(_repo, "file.txt")));
        if (directory)
            Directory.Delete(path);
        else
            File.Delete(path);
        await stash.RestoreAutoAsync(Path.Combine(_repo, ".git"));
        Assert.Equal("local\n", File.ReadAllText(Path.Combine(_repo, "file.txt")));
        Assert.Empty(Git("stash", "list"));
    }

    [Fact]
    public async Task MergeConflict_KeepsAutomaticStashAndConflictState()
    {
        Git("checkout", "-b", "other");
        File.WriteAllText(Path.Combine(_repo, "file.txt"), "other\n");
        Git("commit", "-am", "other");
        Git("checkout", "main");
        File.WriteAllText(Path.Combine(_repo, "file.txt"), "main\n");
        Git("commit", "-am", "main");
        File.WriteAllText(Path.Combine(_repo, "file.txt"), "local\n");
        var stash = new Komorebi.Commands.Stash(_repo) { RaiseError = false };
        Assert.True(await stash.PushAutoAsync("PULL_AUTO_STASH"));
        var sha = Git("rev-parse", "refs/stash");
        RunGit(false, "merge", "other");
        var conflict = File.ReadAllText(Path.Combine(_repo, "file.txt"));
        Assert.Contains("<<<<<<<", conflict);
        await stash.RestoreAutoAsync(Path.Combine(_repo, ".git"));
        Assert.Equal(sha, Git("rev-parse", "refs/stash"));
        Assert.Equal(conflict, File.ReadAllText(Path.Combine(_repo, "file.txt")));
        Assert.True(File.Exists(Path.Combine(_repo, ".git", "MERGE_HEAD")));
    }

    [Fact]
    public async Task RestoreConflict_KeepsSavedChangesInStash()
    {
        File.WriteAllText(Path.Combine(_repo, "file.txt"), "local\n");
        var stash = new Komorebi.Commands.Stash(_repo) { RaiseError = false };
        Assert.True(await stash.PushAutoAsync("PULL_AUTO_STASH"));
        var sha = Git("rev-parse", "refs/stash");
        File.WriteAllText(Path.Combine(_repo, "file.txt"), "updated head\n");
        Git("commit", "-am", "updated");
        await stash.RestoreAutoAsync(Path.Combine(_repo, ".git"));
        Assert.Equal(sha, Git("rev-parse", "refs/stash"));
        Assert.Equal("local", Git("show", "refs/stash:file.txt"));
        Assert.NotEmpty(Git("ls-files", "--unmerged"));
    }

    [Fact]
    public async Task AnotherStash_DoesNotRestoreOrDropSomeoneElsesChanges()
    {
        File.WriteAllText(Path.Combine(_repo, "file.txt"), "ours\n");
        var stash = new Komorebi.Commands.Stash(_repo) { RaiseError = false };
        Assert.True(await stash.PushAutoAsync("PULL_AUTO_STASH"));
        File.WriteAllText(Path.Combine(_repo, "file.txt"), "theirs\n");
        Git("stash", "push", "-m", "another operation");
        var before = Git("stash", "list");
        await stash.RestoreAutoAsync(Path.Combine(_repo, ".git"));
        Assert.Equal(before, Git("stash", "list"));
        Assert.Equal("base\n", File.ReadAllText(Path.Combine(_repo, "file.txt")));
    }

    [Fact]
    public async Task NoLocalChanges_DoesNotPopAnExistingStash()
    {
        File.WriteAllText(Path.Combine(_repo, "file.txt"), "older\n");
        Git("stash", "push", "-m", "existing");
        var before = Git("stash", "list");
        var stash = new Komorebi.Commands.Stash(_repo) { RaiseError = false };
        Assert.True(await stash.PushAutoAsync("PULL_AUTO_STASH"));
        await stash.RestoreAutoAsync(Path.Combine(_repo, ".git"));
        Assert.Equal(before, Git("stash", "list"));
        Assert.Equal("base\n", File.ReadAllText(Path.Combine(_repo, "file.txt")));
    }

    private string Git(params string[] args) => RunGit(true, args);

    private string RunGit(bool success, params string[] args)
    {
        var start = new ProcessStartInfo(Komorebi.Native.OS.GitExecutable)
        {
            WorkingDirectory = _repo,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit(15000);
        if (!exited)
        {
            process.Kill(true);
            process.WaitForExit();
        }
        Assert.True(exited);
        Assert.True((process.ExitCode == 0) == success, stderr.GetAwaiter().GetResult());
        return stdout.GetAwaiter().GetResult().Trim();
    }

    public void Dispose()
    {
        // このテストが作成したGUID付きリポジトリだけを削除する。
        foreach (var path in Directory.EnumerateFiles(_repo, "*", SearchOption.AllDirectories))
            File.SetAttributes(path, FileAttributes.Normal);
        Directory.Delete(_repo, true);
    }
}
