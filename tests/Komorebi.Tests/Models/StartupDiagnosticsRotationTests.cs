using Komorebi.Models;

namespace Komorebi.Tests.Models;

/// <summary>
/// 起動クラッシュログ（追記のみ・Logger の日付ローテーション対象外）の 1 世代ローテーションを検証する。
/// StartupDiagnostics の静的状態（Bind 先）には触れず、AppendWithRotation を一時ディレクトリで直接呼ぶ。
/// </summary>
public sealed class StartupDiagnosticsRotationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"komorebi-crashlog-{Guid.NewGuid():N}");

    public StartupDiagnosticsRotationTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch
        {
            // 後始末の失敗はテスト結果に影響させない
        }
    }

    [Fact]
    public void AppendWithRotation_BelowLimit_AppendsWithoutRotating()
    {
        var path = Path.Combine(_dir, "Komorebi_startup_crash.log");

        StartupDiagnostics.AppendWithRotation(path, "first\n", 100);
        StartupDiagnostics.AppendWithRotation(path, "second\n", 100);

        Assert.Equal("first\nsecond\n", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".old"));
    }

    [Fact]
    public void AppendWithRotation_AtOrAboveLimit_MovesToOldAndKeepsOneGeneration()
    {
        var path = Path.Combine(_dir, "Komorebi_startup_crash.log");
        File.WriteAllText(path, new string('a', 10));

        // 10 バイト（閾値ちょうど）なので退避してから新しいファイルへ書く
        StartupDiagnostics.AppendWithRotation(path, "new\n", 10);
        Assert.Equal(new string('a', 10), File.ReadAllText(path + ".old"));
        Assert.Equal("new\n", File.ReadAllText(path));

        // 再び閾値を超えたら .old は 1 世代だけ（直前の内容で上書き）
        File.AppendAllText(path, new string('b', 10));
        StartupDiagnostics.AppendWithRotation(path, "newer\n", 10);
        Assert.Equal("new\n" + new string('b', 10), File.ReadAllText(path + ".old"));
        Assert.Equal("newer\n", File.ReadAllText(path));
        Assert.Equal(2, Directory.GetFiles(_dir).Length);
    }

    [Fact]
    public void AppendWithRotation_RotationFails_StillAppends()
    {
        var path = Path.Combine(_dir, "Komorebi_startup_crash.log");
        File.WriteAllText(path, new string('a', 10));

        // .old をディレクトリにして退避（File.Move）を必ず失敗させる。記録は失わず現行ファイルへ追記されること。
        Directory.CreateDirectory(path + ".old");
        StartupDiagnostics.AppendWithRotation(path, "kept\n", 10);

        Assert.Equal(new string('a', 10) + "kept\n", File.ReadAllText(path));
    }
}
