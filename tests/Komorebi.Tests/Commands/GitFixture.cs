using System.Diagnostics;

namespace Komorebi.Tests.Commands;

/// <summary>
/// 実 git を使うテスト用の一時リポジトリ（TempGitRepository 相当の共通ヘルパー）。
/// 各テストが個別に init・設定・後始末を書いていたものをここへ集約する。
/// <list type="bullet">
/// <item>グローバル設定・利用者の設定の影響を避けるため、コミットに必要な user.name/email と
/// 署名・改行変換・フックの無効化をリポジトリローカルに設定する。</item>
/// <item>git は 15 秒で打ち切る（ハングでテスト全体を止めない）。</item>
/// <item>Dispose で GUID 付きの作業ディレクトリだけを削除する（他テスト・利用者のファイルには触れない）。</item>
/// </list>
/// </summary>
internal sealed class GitFixture : IDisposable
{
    /// <summary>リポジトリのルート（作業ツリー）。</summary>
    public string Root { get; }

    /// <summary>リポジトリの .git ディレクトリ。</summary>
    public string GitDir => Path.Combine(Root, ".git");

    public GitFixture()
    {
        // 他テストと同じく実行環境の git を使う（ToolCancellationTests と同じ方式）
        Komorebi.Native.OS.GitExecutable = Komorebi.Native.OS.FindGitExecutable();
        Root = Path.Combine(Path.GetTempPath(), $"komorebi-gitfixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
        Git("init", "-q", "-b", "main");
        Git("config", "user.name", "Fixture");
        Git("config", "user.email", "fixture@example.invalid");
        Git("config", "commit.gpgsign", "false");
        // 利用者の autocrlf 設定で作業ツリーの内容が変わると、ファイル内容の比較が環境依存になる
        Git("config", "core.autocrlf", "false");
        // 利用者のグローバルフック（core.hooksPath）を実行させない。存在しないディレクトリを指す。
        Git("config", "core.hooksPath", Path.Combine(Root, "no-hooks"));
    }

    /// <summary>リポジトリ内のパスを返す。</summary>
    public string PathOf(string relative) => Path.Combine(Root, relative);

    /// <summary>git を同期実行し、失敗したらテストを失敗させる。前後の空白を除いた標準出力を返す。</summary>
    public string Git(params string[] arguments) => Run(true, arguments);

    /// <summary>
    /// git を同期実行し、終了コードの成否が <paramref name="expectSuccess"/> と一致することを確認する。
    /// 失敗を期待する操作（衝突する merge 等）の準備に使う。前後の空白を除いた標準出力を返す。
    /// </summary>
    public string Run(bool expectSuccess, params string[] arguments)
    {
        var start = new ProcessStartInfo(Komorebi.Native.OS.GitExecutable)
        {
            WorkingDirectory = Root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;
        // 両出力を並行に読み、パイプ詰まりによるデッドロックを避ける
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit(15000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        Assert.True(exited, $"git {string.Join(' ', arguments)} がタイムアウトしました");
        Assert.True((process.ExitCode == 0) == expectSuccess,
            $"git {string.Join(' ', arguments)} (exit {process.ExitCode}): {error.GetAwaiter().GetResult()}");
        return output.GetAwaiter().GetResult().Trim();
    }

    /// <summary>ファイルを書いてコミットする。</summary>
    public void Commit(string file, string content, string message)
    {
        File.WriteAllText(PathOf(file), content);
        Git("add", file);
        Git("commit", "-q", "-m", message);
    }

    public void Dispose() => DeleteDirectory(Root);

    /// <summary>
    /// テストが作った一時ディレクトリを削除する。.git 内の読み取り専用オブジェクトも消せるよう
    /// 属性を戻してから削除する。後始末の失敗はテスト結果に影響させない。
    /// </summary>
    internal static void DeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(path, true);
        }
        catch
        {
            // 後始末の失敗（ウイルス対策ソフトによる一時ロック等）は無視する
        }
    }
}
