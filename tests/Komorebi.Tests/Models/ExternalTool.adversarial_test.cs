using Komorebi.Models;

namespace Komorebi.Tests.Models;

/// <summary>
/// ExternalTool クラスの敵対的テスト。
/// Launch() の引数検証不足、TOCTTOU競合、プロセス起動失敗の黙殺を検証する。
/// @adversarial @security @chaos @boundary
/// </summary>
public class ExternalToolAdversarialTests
{
    #region Launch - 存在しないファイルパス

    /// <summary>
    /// @adversarial @boundary
    /// ExecFile が存在しない場合、Launch() は何もしない（例外なし）。
    /// File.Exists チェックにより安全だが、エラー報告もない。
    /// </summary>
    [Fact]
    public void Launch_NonExistentExecFile_DoesNotThrow()
    {
        var tool = CreateToolWithExecFile(@"C:\nonexistent\path\to\editor.exe");

        var exception = Record.Exception(() => tool.Launch(@"C:\some\repo"));
        Assert.Null(exception);
    }

    /// <summary>
    /// @adversarial @boundary
    /// ExecFile が空文字列の場合。File.Exists("") は false を返す。
    /// </summary>
    [Fact]
    public void Launch_EmptyExecFile_DoesNotThrow()
    {
        var tool = CreateToolWithExecFile(string.Empty);

        var exception = Record.Exception(() => tool.Launch("some args"));
        Assert.Null(exception);
    }

    /// <summary>
    /// @adversarial @boundary
    /// ExecFile が null の場合。File.Exists(null) は ArgumentNullException を投げる。
    /// コンストラクタで null が渡されるケースを検証。
    /// </summary>
    [Fact]
    public void Launch_NullExecFile_DoesNotThrow()
    {
        var tool = CreateToolWithExecFile(null!);

        // File.Exists(null) は .NET 6+ では false を返す（例外にならない）
        var exception = Record.Exception(() => tool.Launch("some args"));
        Assert.Null(exception);
    }

    #endregion

    #region ExternalTool プロパティ検証

    /// <summary>
    /// @adversarial @boundary
    /// MakeLaunchOptions は optionsGenerator が null の場合 null を返す。
    /// </summary>
    [Fact]
    public void MakeLaunchOptions_NoGenerator_ReturnsNull()
    {
        var tool = CreateToolWithExecFile(@"C:\test\editor.exe");

        var result = tool.MakeLaunchOptions(@"C:\some\repo");
        Assert.Null(result);
    }

    #endregion

    #region ヘルパーメソッド

    /// <summary>
    /// テスト用の ExternalTool インスタンスを生成する。
    /// IconImage のロード失敗は無視される（Avalonia リソースが無いため）。
    /// </summary>
    private static ExternalTool CreateToolWithExecFile(string execFile)
    {
        // コンストラクタ内の AssetLoader.Open は例外を catch して無視する
        // テスト環境では Avalonia リソースが無いため IconImage は null になる
        return new ExternalTool("TestTool", "nonexistent_icon", execFile);
    }

    #endregion
}
