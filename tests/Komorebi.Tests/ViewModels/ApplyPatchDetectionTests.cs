namespace Komorebi.Tests.ViewModels;

/// <summary>
/// パッチ適用ダイアログで git am（コミットとして適用）を選べるかどうかの判定を検証する（upstream ef017624）。
/// </summary>
public class ApplyPatchDetectionTests
{
    [Fact]
    public void FormatPatchHeader_IsDetectedAsMailFormat()
    {
        var file = WritePatch(
            "From 0123456789abcdef0123456789abcdef01234567 Mon Sep 17 00:00:00 2001\n" +
            "From: Alice <alice@example.com>\n" +
            "Date: Thu, 12 Nov 2026 10:00:00 +0900\n" +
            "Subject: [PATCH] fix\n" +
            "\n---\n");
        try
        {
            Assert.True(Komorebi.ViewModels.Apply.DetectMailFormatPatch(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1 +1 @@\n-a\n+b\n")]
    [InlineData("From abc\nFrom: Alice <alice@example.com>\nDate: x\n")]
    [InlineData("")]
    public void PlainOrTruncatedPatch_IsNotMailFormat(string content)
    {
        var file = WritePatch(content);
        try
        {
            Assert.False(Komorebi.ViewModels.Apply.DetectMailFormatPatch(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingPath_IsNotMailFormat(string? path)
    {
        Assert.False(Komorebi.ViewModels.Apply.DetectMailFormatPatch(path!));
        Assert.False(Komorebi.ViewModels.Apply.DetectMailFormatPatch(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.patch")));
    }

    private static string WritePatch(string content)
    {
        var file = Path.Combine(Path.GetTempPath(), $"komorebi-am-{Guid.NewGuid():N}.patch");
        File.WriteAllText(file, content);
        return file;
    }
}
