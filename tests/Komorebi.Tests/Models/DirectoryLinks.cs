namespace Komorebi.Tests.Models;

/// <summary>
/// テスト用にディレクトリのリンク（reparse point）を作るヘルパー。
/// SSHKeyPermissionFixerTests と SafeDirectoriesTests の両方で、リンク・ジャンクション経由の誘導を検証するために使う。
/// </summary>
internal static class DirectoryLinks
{
    /// <summary>
    /// ディレクトリのシンボリックリンクを作る。Windows で権限が無く作れない場合は、権限不要のジャンクション
    /// （同じ reparse point）で代用する。どちらも作れない環境ではテストを Skip する。
    /// </summary>
    public static void CreateOrSkip(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            if (!OperatingSystem.IsWindows())
                Assert.Skip($"シンボリックリンクを作成できない環境: {e.Message}");
        }

        CreateJunctionOrSkip(link, target);
    }

    /// <summary>
    /// Windows のジャンクションを作る（.NET に作成 API が無いため、Windows 標準の PowerShell で作る）。
    /// Windows 以外・作成できない環境ではテストを Skip する。
    /// </summary>
    public static void CreateJunctionOrSkip(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
            Assert.Skip("ジャンクションは Windows のみ");

        var start = new System.Diagnostics.ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // パスはコマンド文字列に埋め込まず環境変数で渡す（空白・引用符を含むパスでも解釈を変えない）
        start.Environment["KOMOREBI_TEST_LINK"] = link;
        start.Environment["KOMOREBI_TEST_TARGET"] = target;
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command",
            "New-Item -ItemType Junction -Path $env:KOMOREBI_TEST_LINK -Target $env:KOMOREBI_TEST_TARGET | Out-Null" })
            start.ArgumentList.Add(argument);
        try
        {
            using var process = System.Diagnostics.Process.Start(start)!;
            process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(15000);
            if (!Directory.Exists(link) || new DirectoryInfo(link).LinkTarget is null)
                Assert.Skip($"リンク・ジャンクションを作成できない環境: {error}");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            Assert.Skip($"ジャンクションを作成できない環境: {e.Message}");
        }
    }
}
