using System.Diagnostics;

using Komorebi.Commands;
using Komorebi.Models;

namespace Komorebi.Tests.Commands;

public class RawDiffPatchTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NonUtf8Patch_RoundTripsThroughGit(bool reverse, bool crlf)
    {
        var root = Path.Combine(Path.GetTempPath(), $"komorebi-raw-diff-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await Git(root, "init", "--quiet");
            await Git(root, "config", "core.autocrlf", "false");
            var newline = crlf ? new byte[] { 13, 10 } : [10];
            // CP932 の「あ」「い」。末尾の文脈行は改行を持たない。
            byte[] oldContent = [.. "prefix"u8, .. newline, 0x82, 0xa0, .. newline, .. "tail"u8];
            byte[] newContent = [.. "prefix"u8, .. newline, 0x82, 0xa2, .. newline, .. "tail"u8];
            var file = Path.Combine(root, "sample.txt");
            await File.WriteAllBytesAsync(file, oldContent, TestContext.Current.CancellationToken);
            await Git(root, "add", "sample.txt");
            await File.WriteAllBytesAsync(file, newContent, TestContext.Current.CancellationToken);
            var raw = await Git(root, "diff", "--no-color", "--no-ext-diff", "--full-index", "--", "sample.txt");
            var result = Diff.ParseDiffOutput(raw);
            var diff = Assert.IsType<TextDiff>(result.TextDiff);
            Assert.Equal(new byte[] { 0x82, 0xa0 }.Concat(crlf ? new byte[] { 13 } : []),
                Assert.Single(diff.Lines, x => x.Type == TextDiffLineType.Deleted).RawContent);

            var selection = diff.MakeSelection(1, diff.Lines.Count, true, false);
            var patch = Path.Combine(root, "selected.patch");
            diff.GeneratePatchFromSelection("sample.txt", result.OldHash, selection, reverse, patch);
            if (reverse)
            {
                await Git(root, "add", "sample.txt");
                await Git(root, "apply", "--cached", "--reverse", patch);
            }
            else
            {
                await Git(root, "apply", "--cached", patch);
            }

            Assert.Equal(reverse ? oldContent : newContent, await Git(root, "show", ":sample.txt"));
            Assert.Equal(newContent, await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken));
        }
        finally
        {
            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(root, true);
        }
    }

    private static async Task<byte[]> Git(string root, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);

        using var process = Process.Start(start)!;
        using var output = new MemoryStream();
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.StandardOutput.BaseStream.CopyToAsync(output, TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(process.ExitCode == 0, await stderr);
        return output.ToArray();
    }
}
