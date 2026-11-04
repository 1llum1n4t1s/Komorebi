using System.Diagnostics;

namespace Komorebi.Tests.Commands;

public class CommandCancellationTests
{
    public static bool IsUnix => !OperatingSystem.IsWindows();

    [Fact(Skip = "Unix process-group behavior; validated separately on macOS.", SkipUnless = nameof(IsUnix))]
    public async Task Supervision_PreservesArgumentsInputAndExitCode()
    {
        const string argument = "space 日本語 \"quoted\" \\";
        var start = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = "-c " + Komorebi.StringExtensions.Quoted("printf '%s' \"$1\"; /bin/cat; exit 7") + " -- " + Komorebi.StringExtensions.Quoted(argument),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Assert.True(Komorebi.Native.CommandCancellation.PrepareProcessGroup(start));
        using var process = Process.Start(start)!;
        await process.StandardInput.WriteAsync("stdin".AsMemory(), TestContext.Current.CancellationToken);
        process.StandardInput.Close();
        var output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(argument + "stdin", output);
        Assert.Empty(error);
        Assert.Equal(7, process.ExitCode);
    }

    [Fact(Skip = "Unix process-group behavior; validated separately on macOS.", SkipUnless = nameof(IsUnix))]
    public async Task Termination_StopsResistantDescendantAndReleasesPipes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"komorebi-cancel-group-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var childScript = Path.Combine(root, "child.sh");
        var leaderScript = Path.Combine(root, "leader.sh");
        var pidFile = Path.Combine(root, "child.pid");
        var cleaned = Path.Combine(root, "leader-cleaned");
        var release = Path.Combine(root, "release");
        await File.WriteAllTextAsync(childScript, "trap '' TERM\nprintf '%s' \"$$\" > \"$1\"\nwhile [ ! -f \"$2\" ]; do /bin/sleep 1; done\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(leaderScript, "trap 'printf cleaned > \"$3\"; exit 0' TERM\n/bin/sh \"$1\" \"$2\" \"$4\" &\nwait\n", TestContext.Current.CancellationToken);
        var start = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = string.Join(' ', new[] { leaderScript, childScript, pidFile, cleaned, release }.Select(Komorebi.StringExtensions.Quoted)),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Assert.True(Komorebi.Native.CommandCancellation.PrepareProcessGroup(start));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        Process? child = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var pid = 0;
            while (!File.Exists(pidFile) || !int.TryParse(await File.ReadAllTextAsync(pidFile, timeout.Token), out pid))
                await Task.Delay(20, timeout.Token);
            child = Process.GetProcessById(pid);
            Komorebi.Native.CommandCancellation.Terminate(process, true);
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(5), timeout.Token);
            // パイプのEOFと非子プロセスの回収は同時ではないため、同じ期限内で終了通知も待つ。
            await child.WaitForExitAsync(timeout.Token);
            Assert.True(child.HasExited);
            Assert.True(File.Exists(cleaned), "The leader did not receive its TERM cleanup opportunity.");
            await File.WriteAllTextAsync(Path.Combine(root, "result.json"), "{\"descendantExited\":true,\"pipesClosed\":true,\"leaderCleaned\":true}", timeout.Token);
        }
        finally
        {
            await File.WriteAllTextAsync(release, string.Empty, TestContext.Current.CancellationToken);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
            if (child is { HasExited: false })
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
            child?.Dispose();
        }
    }

    [Fact]
    public async Task Termination_StopsOwnedProcess()
    {
        var start = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sleep",
            Arguments = OperatingSystem.IsWindows() ? "-NoProfile -NonInteractive -Command Start-Sleep -Seconds 30" : "30",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var ownsGroup = Komorebi.Native.CommandCancellation.PrepareProcessGroup(start);
        using var process = Process.Start(start)!;
        try
        {
            await Task.Delay(200, TestContext.Current.CancellationToken);
            Komorebi.Native.CommandCancellation.Terminate(process, ownsGroup);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(process.HasExited);
            Komorebi.Native.CommandCancellation.Terminate(process, ownsGroup);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
        }
    }

    [Fact]
    public async Task Popup_CancellationScopeResetsAfterTerminationAndSupportsRetry()
    {
        var popup = new CancellablePopup();
        for (var i = 0; i < 2; i++)
        {
            var running = popup.Sure();
            Assert.True(popup.CanTerminate);
            popup.Terminate();
            Assert.False(await running.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.False(popup.CanTerminate);
            Assert.False(popup.IsTerminating);
        }
    }

    public sealed class CancellablePopup : Komorebi.ViewModels.Popup
    {
        public override async Task<bool> Sure()
        {
            using var operation = BeginCancellableOperation();
            try
            {
                await Task.Delay(Timeout.Infinite, operation.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
