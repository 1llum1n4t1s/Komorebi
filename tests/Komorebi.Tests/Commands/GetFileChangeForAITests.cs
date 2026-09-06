using System.Diagnostics;

using Komorebi.Commands;

namespace Komorebi.Tests.Commands;

public class GetFileChangeForAITests
{
    [Fact]
    public async Task AmendDiff_IncludesPreviousCommitAndStagedChanges()
    {
        var repo = Path.Combine(Path.GetTempPath(), "Komorebi-AIDiff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        try
        {
            async Task<string> Git(string args)
            {
                using var process = Process.Start(new ProcessStartInfo("git", args)
                {
                    WorkingDirectory = repo,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                })!;
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(TestContext.Current.CancellationToken);
                Assert.True(process.ExitCode == 0, await error);
                return await output;
            }

            await Git("init");
            await Git("-c user.name=Test -c user.email=test@example.invalid commit --allow-empty -m base");
            var parent = (await Git("rev-parse HEAD")).Trim();
            var file = Path.Combine(repo, "message.txt");
            await File.WriteAllTextAsync(file, "previous commit\n", TestContext.Current.CancellationToken);
            await Git("add message.txt");
            await Git("-c user.name=Test -c user.email=test@example.invalid commit -m previous");
            await File.AppendAllTextAsync(file, "staged change\n", TestContext.Current.CancellationToken);
            await Git("add message.txt");

            var normal = await Git(new GetFileChangeForAI(repo, "message.txt", string.Empty).Args);
            var amend = await Git(new GetFileChangeForAI(repo, "message.txt", string.Empty, parent).Args);
            Assert.DoesNotContain("+previous commit", normal);
            Assert.Contains("+previous commit", amend);
            Assert.Contains("+staged change", amend);
        }
        finally
        {
            // このテストが作成した一意な一時リポジトリだけを削除する。
            foreach (var file in Directory.EnumerateFiles(repo, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(repo, true);
        }
    }
}
