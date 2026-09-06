using System.Diagnostics;

namespace Komorebi.Tests.Commands;

public class ArgumentBoundaryTests
{
    [Theory]
    [InlineData("")]
    [InlineData("plain")]
    [InlineData("space and 日本語")]
    [InlineData("topic\"quoted")]
    [InlineData("C:\\work folder\\")]
    [InlineData("C:\\work folder\\\\")]
    [InlineData("back\\\"quote")]
    [InlineData("back\\\\\"quote")]
    [InlineData("\\")]
    [InlineData("a'b\tc")]
    public void Quoted_RoundTripsThroughActualProcessArguments(string value)
    {
        AssertArguments(value.Quoted() + " next", value, "next");
    }

    [Theory]
    [InlineData("topic\"quoted")]
    [InlineData("")]
    public void Pull_PreservesRemoteAndOptionalBranch(string branch)
    {
        var command = new Komorebi.Commands.Pull(Path.GetTempPath(), "up\"stream", branch, false);
        List<string> expected = ["pull", "--verbose", "--progress", "--rebase=false", "up\"stream"];
        if (branch.Length > 0)
            expected.Add(branch);
        AssertArguments(command.Args, [.. expected]);
    }

    [Theory]
    [InlineData(true, "origin/topic\"quoted")]
    [InlineData(false, "")]
    public async Task Worktree_PreservesNameTrackingAndTrailingSlash(bool createNew, string tracking)
    {
        var command = new Komorebi.Commands.Worktree(Path.GetTempPath())
        {
            CancellationToken = new CancellationToken(true),
            RaiseError = false,
        };
        const string name = "topic\"quoted";
        const string path = "C:\\work folder\\";
        Assert.False(await command.AddAsync(path, name, createNew, tracking));
        List<string> expected = ["worktree", "add"];
        if (tracking.Length > 0)
            expected.Add("--track");
        expected.AddRange([createNew ? "-b" : "-B", name, path]);
        if (tracking.Length > 0)
            expected.Add(tracking);
        else if (!createNew)
            expected.Add(name);
        AssertArguments(command.Args, [.. expected]);
    }

    [Fact]
    public async Task LFS_AllRemoteOperationsPreserveArgumentBoundaries()
    {
        var command = new Komorebi.Commands.LFS(Path.GetTempPath())
        {
            CancellationToken = new CancellationToken(true),
            RaiseError = false,
        };
        const string remote = "up\"stream";
        await command.FetchAsync(remote);
        AssertArguments(command.Args, "lfs", "fetch", remote);
        await command.PullAsync(remote);
        AssertArguments(command.Args, "lfs", "pull", remote);
        await command.PushAsync(remote);
        AssertArguments(command.Args, "lfs", "push", remote);
        await command.GetLocksAsync(remote);
        AssertArguments(command.Args, "lfs", "locks", "--json", "--remote=" + remote);
        await command.LockAsync(remote, "file name");
        AssertArguments(command.Args, "lfs", "lock", "--remote=" + remote, "file name");
        await command.UnlockAsync(remote, "file name", true);
        AssertArguments(command.Args, "lfs", "unlock", "--remote=" + remote, "-f", "file name");
        await command.UnlockMultipleAsync(remote, ["file one", "file two"], false);
        AssertArguments(command.Args, "lfs", "unlock", "--remote=" + remote, "file one", "file two");
    }

    private static void AssertArguments(string arguments, params string[] expected)
    {
        // --sq-quoteは後続を実行せず表示する。LFS等の外部サービスへは接続しない。
        var git = Komorebi.Native.OS.FindGitExecutable();
        var raw = new ProcessStartInfo(git) { Arguments = "rev-parse --sq-quote " + arguments };
        var baseline = new ProcessStartInfo(git);
        baseline.ArgumentList.Add("rev-parse");
        baseline.ArgumentList.Add("--sq-quote");
        foreach (var value in expected)
            baseline.ArgumentList.Add(value);
        Assert.Equal(Read(baseline), Read(raw));
    }

    internal static string Read(ProcessStartInfo start)
    {
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
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
        Assert.True(process.ExitCode == 0, stderr.GetAwaiter().GetResult());
        return stdout.GetAwaiter().GetResult();
    }
}
