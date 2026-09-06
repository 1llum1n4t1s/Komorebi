using System.Diagnostics;

namespace Komorebi.Tests.Commands;

public class ReadToEndCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringOutput_WaitsForKillInsteadOfUnregisteringIt(bool compareRevisions)
    {
        var root = Path.Combine(Path.GetTempPath(), $"komorebi-read-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        using var cancellation = new CancellationTokenSource();
        using var releaseCancellation = new ManualResetEventSlim();
        var enteredCancellation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Process? ownedProcess = null;
        Task? cancelling = null;
        Task<Komorebi.Commands.Command.Result>? reading = null;
        var testToken = TestContext.Current.CancellationToken;
        try
        {
            var pidFile = Path.Combine(root, "pid");
            var continueFile = Path.Combine(root, "continue");
            var start = CreateWaitingProcess(root, pidFile, continueFile);
            reading = compareRevisions
                ? ReadComparisonAsync(start, cancellation.Token)
                : Komorebi.Commands.Command.ReadToEndAsync(start, cancellation.Token);
            using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(testToken);
            startupTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            var pid = 0;
            while (!File.Exists(pidFile) || !int.TryParse(await File.ReadAllTextAsync(pidFile, startupTimeout.Token), out pid))
                await Task.Delay(10, startupTimeout.Token);
            ownedProcess = Process.GetProcessById(pid);

            // 後から登録したコールバックを先に止め、Killより先に出力読み取りが完了する順序を作る。
            using var blocker = cancellation.Token.Register(() =>
            {
                enteredCancellation.TrySetResult();
                releaseCancellation.Wait(TimeSpan.FromSeconds(10));
            });
            cancelling = Task.Run(cancellation.Cancel, testToken);
            await enteredCancellation.Task.WaitAsync(TimeSpan.FromSeconds(5), testToken);
            await File.WriteAllTextAsync(continueFile, string.Empty, testToken);
            var completedBeforeKill = await Task.WhenAny(reading, Task.Delay(1000, testToken)) == reading;
            releaseCancellation.Set();
            await cancelling.WaitAsync(TimeSpan.FromSeconds(5), testToken);
            var result = await reading.WaitAsync(TimeSpan.FromSeconds(5), testToken);

            Assert.False(completedBeforeKill, $"Kill前に読み取りが完了しました。プロセス終了: {ownedProcess.HasExited}");
            Assert.False(result.IsSuccess);
            Assert.True(ownedProcess.HasExited);
        }
        finally
        {
            releaseCancellation.Set();
            if (ownedProcess is { HasExited: false })
            {
                ownedProcess.Kill(entireProcessTree: true);
                await ownedProcess.WaitForExitAsync(testToken).WaitAsync(TimeSpan.FromSeconds(5), testToken);
            }
            if (cancelling != null)
                await cancelling.WaitAsync(TimeSpan.FromSeconds(5), testToken);
            if (reading != null)
                await reading.WaitAsync(TimeSpan.FromSeconds(5), testToken);
            ownedProcess?.Dispose();
            Directory.Delete(root, true);
        }
    }

    private static async Task<Komorebi.Commands.Command.Result> ReadComparisonAsync(ProcessStartInfo start, CancellationToken token)
    {
        var changes = await Komorebi.Commands.CompareRevisions.ReadAsync(start, token);
        return new() { IsSuccess = changes.Count > 0 };
    }

    [Fact]
    public async Task AlreadyCancelled_DoesNotStartProcess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await Komorebi.Commands.Command.ReadToEndAsync(
            new ProcessStartInfo { FileName = "komorebi-nonexistent-test-executable" }, cancellation.Token);
        Assert.False(result.IsSuccess);
        Assert.Empty(result.StdErr);
    }

    private static ProcessStartInfo CreateWaitingProcess(string root, string pidFile, string continueFile)
    {
        var start = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "pwsh" : "/bin/sh",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (OperatingSystem.IsWindows())
        {
            var script = Path.Combine(root, "wait.ps1");
            File.WriteAllText(script, """
                param($pidFile, $continueFile)
                [Console]::Out.WriteLine("M`talready-read.txt")
                [IO.File]::WriteAllText($pidFile, [string]$PID)
                while (!(Test-Path -LiteralPath $continueFile)) { Start-Sleep -Milliseconds 10 }
                [Console]::Out.WriteLine("M`tafter-cancellation.txt")
                [Console]::Error.WriteLine('stderr after cancellation')
                Start-Sleep -Seconds 30
                """);
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", script, pidFile, continueFile })
                start.ArgumentList.Add(argument);
        }
        else
        {
            foreach (var argument in new[] { "-c", "printf 'M\\talready-read.txt\\n'; echo $$ > \"$1\"; while [ ! -f \"$2\" ]; do sleep 0.01; done; printf 'M\\tafter-cancellation.txt\\n'; echo stderr >&2; sleep 30", "komorebi-test", pidFile, continueFile })
                start.ArgumentList.Add(argument);
        }
        return start;
    }
}
