using System.Diagnostics;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 実際にマージを行わずマージ結果をテストするgitコマンド。
/// git merge-tree --write-tree を実行し、コンフリクトの有無を事前判定する。
/// マージポップアップ表示時の事前チェック機能で使用する。
/// </summary>
public class MergeTree : Command
{
    /// <summary>
    /// MergeTreeコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="source">マージ元のブランチ名またはコミットSHA。</param>
    /// <param name="dest">マージ先のブランチ名。</param>
    public MergeTree(string repo, string source, string dest)
    {
        WorkingDirectory = repo;
        Context = repo;
        RaiseError = false;
        Args = $"merge-tree --write-tree {source} {dest}";
    }

    /// <summary>
    /// マージテストを実行し終了コードを取得する。
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
