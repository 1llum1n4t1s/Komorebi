using System.Diagnostics;

namespace Komorebi.Tests.Commands;

public class ArgumentBoundaryTests
{
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
    public void Push_Branch_UsesFullRefNamesOnBothSides()
    {
        // upstream 8189114b: 同名タグがあっても曖昧にならないよう、ブランチは完全な ref 名で push する。
        var command = new Komorebi.Commands.Push(Path.GetTempPath(), "topic", "origin", "feature/x", false, false, true, false);
        AssertArguments(command.Args, "push", "--progress", "--verbose", "-u", "origin", "refs/heads/topic:refs/heads/feature/x");
    }

    [Fact]
    public void Push_Revision_KeepsShaAndQualifiesOnlyRemoteSide()
    {
        // Komorebi は revision push も同じコンストラクターを使うため、SHA に refs/heads/ を付けない。
        const string sha = "0123456789abcdef0123456789abcdef01234567";
        var command = new Komorebi.Commands.Push(Path.GetTempPath(), sha, "origin", "main", false, false, false, true, localIsRevision: true);
        AssertArguments(command.Args, "push", "--progress", "--verbose", "--force-with-lease", "origin", $"{sha}:refs/heads/main");
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
