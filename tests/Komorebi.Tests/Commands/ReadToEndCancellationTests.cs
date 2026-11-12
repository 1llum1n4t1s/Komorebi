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
        var pidFile = Path.Combine(root, "pid");
        var continueFile = Path.Combine(root, "continue");
        try
        {
            var start = CreateWaitingProcess(root, pidFile, continueFile);
            reading = compareRevisions
                ? ReadComparisonAsync(start, cancellation.Token)
                : Komorebi.Commands.Command.ReadToEndAsync(start, cancellation.Token);
            // pwsh の起動は並行テストの負荷で数秒かかることがあるため、起動待ちには余裕を持たせる。
            using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(testToken);
            startupTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            var pid = await WaitForPidAsync(pidFile, startupTimeout.Token);
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

            // 以前は PID 取得前に失敗（起動待ちのタイムアウト、PID ファイル読取時の共有違反）すると、
            // 子の pwsh が continue ファイルを待つ無限ループのまま残っていた。
            // どの段階で失敗しても子が終わるよう、(1) 読取側の中止で製品の Kill 経路を動かし、
            // (2) continue ファイルで待機ループを抜けさせ、(3) PID が分かれば直接終了させる。
            if (!cancellation.IsCancellationRequested)
                await cancellation.CancelAsync();
            TryCreateFile(continueFile);
            if (ownedProcess == null && TryReadPid(pidFile, out var latePid))
                ownedProcess = TryGetProcess(latePid);
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
            GitFixture.DeleteDirectory(root);
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

    /// <summary>
    /// 子が書いた PID ファイルを待つ。子は一時ファイルへ書いてから rename するが、
    /// 念のため読取中の共有違反（IOException）や空読みは再試行する（以前はここで例外になり子が残っていた）。
    /// </summary>
    private static async Task<int> WaitForPidAsync(string pidFile, CancellationToken token)
    {
        while (true)
        {
            if (TryReadPid(pidFile, out var pid))
                return pid;
            await Task.Delay(10, token);
        }
    }

    private static bool TryReadPid(string pidFile, out int pid)
    {
        pid = 0;
        try
        {
            return File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile).Trim(), out pid);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static Process? TryGetProcess(int pid)
    {
        try
        {
            return Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            // 既に終了している
            return null;
        }
    }

    private static void TryCreateFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                File.WriteAllText(path, string.Empty);
        }
        catch (IOException)
        {
            // 後始末の補助なので失敗は無視する（直接 Kill へ進む）
        }
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
        // 子スクリプトの方針:
        // - PID は一時ファイルに書いてから rename し、読取側が書きかけ・共有違反に遭わないようにする。
        // - continue ファイルの待機は 60 秒で打ち切る。テスト側が異常終了しても子が無限に残らない。
        // - 最後の待機（Kill されるまでの区間）も 30 秒で自然終了する。
        if (OperatingSystem.IsWindows())
        {
            var script = Path.Combine(root, "wait.ps1");
            File.WriteAllText(script, """
                param($pidFile, $continueFile)
                [Console]::Out.WriteLine("M`talready-read.txt")
                [IO.File]::WriteAllText("$pidFile.tmp", [string]$PID)
                [IO.File]::Move("$pidFile.tmp", $pidFile)
                $deadline = [DateTime]::UtcNow.AddSeconds(60)
                while (!(Test-Path -LiteralPath $continueFile)) {
                    if ([DateTime]::UtcNow -gt $deadline) { exit 2 }
                    Start-Sleep -Milliseconds 10
                }
                [Console]::Out.WriteLine("M`tafter-cancellation.txt")
                [Console]::Error.WriteLine('stderr after cancellation')
                Start-Sleep -Seconds 30
                """);
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", script, pidFile, continueFile })
                start.ArgumentList.Add(argument);
        }
        else
        {
            const string script = "printf 'M\\talready-read.txt\\n'; echo $$ > \"$1.tmp\"; mv \"$1.tmp\" \"$1\"; " +
                "n=0; while [ ! -f \"$2\" ]; do n=$((n+1)); [ $n -gt 6000 ] && exit 2; sleep 0.01; done; " +
                "printf 'M\\tafter-cancellation.txt\\n'; echo stderr >&2; sleep 30";
            foreach (var argument in new[] { "-c", script, "komorebi-test", pidFile, continueFile })
                start.ArgumentList.Add(argument);
        }
        return start;
    }
}
