using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using Komorebi.AI;

namespace Komorebi.Tests.AI;

public class ToolCancellationTests
{
    [Theory]
    [InlineData(Provider.OpenAI)]
    [InlineData(Provider.Anthropic)]
    public async Task CancelDuringTool_StopsGitAndDoesNotContinueGeneration(Provider provider)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ai-cancellation-artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var pidDirectory = Path.Combine(root, "pids");
        Directory.CreateDirectory(pidDirectory);
        var releaseFile = Path.Combine(root, "release");
        var script = Path.Combine(root, OperatingSystem.IsWindows() ? "wait.ps1" : "wait.sh");
        File.WriteAllText(script, OperatingSystem.IsWindows()
            ? "[IO.File]::WriteAllText([IO.Path]::Combine('" + pidDirectory.Replace("'", "''") + "', [string]$PID), ''); while (!(Test-Path -LiteralPath '" + releaseFile.Replace("'", "''") + "')) { Start-Sleep -Milliseconds 20 }"
            : ": > '" + pidDirectory.Replace("'", "'\\''") + "'/$$; while [ ! -f '" + releaseFile.Replace("'", "'\\''") + "' ]; do sleep 0.02; done");
        Komorebi.Native.OS.GitExecutable = Komorebi.Native.OS.FindGitExecutable();
        async Task Git(params string[] arguments)
        {
            var start = new ProcessStartInfo(Komorebi.Native.OS.GitExecutable)
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            foreach (var argument in arguments)
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var error = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.True(process.ExitCode == 0, await error);
            await output;
        }

        await Git("init");
        await Git("config", "diff.wait.textconv", OperatingSystem.IsWindows()
            ? "pwsh -NoProfile -NonInteractive -File '" + script.Replace('\\', '/') + "'"
            : "/bin/sh '" + script + "'");
        File.WriteAllText(Path.Combine(root, ".gitattributes"), "*.txt diff=wait\n");
        File.WriteAllText(Path.Combine(root, "first.txt"), "first\n");
        File.WriteAllText(Path.Combine(root, "second.txt"), "second\n");
        await Git("add", ".");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serve = RespondAsync(listener, provider, root, timeout.Token);
        var service = new Service
        {
            Provider = provider,
            Server = $"http://127.0.0.1:{port}" + (provider == Provider.Anthropic ? "" : "/v1"),
            ApiKey = "synthetic-local-fixture-key",
            Model = "fixture",
        };
        var updates = new List<string>();
        var generating = new Agent(service).GenerateCommitMessageAsync(root, "first.txt\nsecond.txt", text =>
        {
            lock (updates)
                updates.Add(text);
        }, cancellation.Token);
        var expectedChildren = provider == Provider.Anthropic ? 2 : 1;
        List<Process> children = [];
        try
        {
            while (children.Count < expectedChildren)
            {
                foreach (var pidFile in Directory.EnumerateFiles(pidDirectory))
                {
                    var pid = int.Parse(Path.GetFileName(pidFile));
                    if (!children.Any(child => child.Id == pid))
                        children.Add(Process.GetProcessById(pid));
                }
                if (children.Count >= expectedChildren)
                    break;
                await Task.Delay(20, timeout.Token);
            }
            Assert.Equal(expectedChildren, children.Count);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await generating.WaitAsync(TimeSpan.FromSeconds(5), timeout.Token));
            Assert.All(children, child => Assert.True(child.HasExited));
            Assert.Equal(expectedChildren, Directory.GetFiles(pidDirectory).Length);
            Assert.False(listener.Pending(), "A follow-up provider request was sent after cancellation.");
            if (provider == Provider.OpenAI)
                Assert.DoesNotContain(updates, text => text.Contains("second.txt", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new
            {
                Provider = provider.ToString(),
                Cancelled = true,
                ExitedChildPids = children.Select(child => child.Id).ToArray(),
                FollowupRequest = false,
            }));
        }
        finally
        {
            cancellation.Cancel();
            File.WriteAllText(releaseFile, string.Empty);
            foreach (var child in children)
            {
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync(TestContext.Current.CancellationToken);
                }
                child.Dispose();
            }
            listener.Stop();
            try
            {
                await generating.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }
            await serve;
        }
    }

    private static async Task RespondAsync(TcpListener listener, Provider provider, string repo, CancellationToken cancellation)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellation);
        await using var stream = client.GetStream();
        var header = new StringBuilder();
        var one = new byte[1];
        while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            await stream.ReadExactlyAsync(one, cancellation);
            header.Append((char)one[0]);
            Assert.True(header.Length < 65536);
        }
        var length = System.Text.RegularExpressions.Regex.Match(header.ToString(), @"(?im)^Content-Length:\s*(\d+)");
        Assert.True(length.Success);
        await stream.ReadExactlyAsync(new byte[int.Parse(length.Groups[1].Value)], cancellation);
        var input1 = JsonSerializer.Serialize(new { repo, file = "first.txt" });
        var input2 = JsonSerializer.Serialize(new { repo, file = "second.txt" });
        var body = provider == Provider.Anthropic
            ? $$$$"""{"id":"fixture","type":"message","role":"assistant","model":"fixture","stop_reason":"tool_use","content":[{"type":"tool_use","id":"one","name":"GetDetailChangesInFile","input":{{{{input1}}}}},{"type":"tool_use","id":"two","name":"GetDetailChangesInFile","input":{{{{input2}}}}}],"usage":{"input_tokens":1,"output_tokens":1}}"""
            : $$$$"""{"id":"fixture","object":"chat.completion","created":1,"model":"fixture","choices":[{"index":0,"message":{"role":"assistant","tool_calls":[{"id":"one","type":"function","function":{"name":"GetDetailChangesInFile","arguments":{{{{JsonSerializer.Serialize(input1)}}}}}},{"id":"two","type":"function","function":{"name":"GetDetailChangesInFile","arguments":{{{{JsonSerializer.Serialize(input2)}}}}}}]},"finish_reason":"tool_calls"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""";
        var bytes = Encoding.UTF8.GetBytes(body);
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"), cancellation);
        await stream.WriteAsync(bytes, cancellation);
    }
}
