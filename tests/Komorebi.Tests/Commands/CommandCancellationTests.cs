using System.Diagnostics;

namespace Komorebi.Tests.Commands;

public class CommandCancellationTests
{
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
