using System.Diagnostics;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 実際にリベースを行わずリベース結果をテストするgitコマンド。
/// git replay --onto を実行し、コンフリクトの有無を事前判定する。
/// リベースポップアップ表示時の事前チェック機能で使用する（git 2.44.0以上が必要）。
/// </summary>
public class Replay : Command
{
    /// <summary>
    /// Replayコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="onto">リベース先のリビジョン。</param>
    /// <param name="range">再適用するコミット範囲（例: base..head）。</param>
    public Replay(string repo, string onto, string range)
    {
        WorkingDirectory = repo;
        Context = repo;
        RaiseError = false;
        Args = $"replay --onto {onto} {range}";
    }

    /// <summary>
    /// リベーステストを実行し終了コードを取得する。
    /// 0=コンフリクトなし、1=コンフリクトあり、それ以外=不明なエラー。
    /// プロセス起動に失敗した場合は-1を返す。
    /// </summary>
    public async Task<int> GetExitCodeAsync()
    {
        using var proc = new Process();
        proc.StartInfo = CreateGitStartInfo(false);

        var exitCode = -1;
        try
        {
            proc.Start();
            await proc.WaitForExitAsync().ConfigureAwait(false);
            exitCode = proc.ExitCode;
        }
        catch
        {
            // 例外は無視して-1を返すのみ
        }

        return exitCode;
    }
}
