using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 指定リビジョンに含まれるすべてのファイル名を取得するクラス。
/// git ls-tree -r --name-only を使用する。
/// </summary>
public class QueryRevisionFileNames : Command
{
    /// <summary>
    /// コンストラクタ。指定リビジョンのファイル名一覧を取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="revision">対象リビジョン</param>
    public QueryRevisionFileNames(string repo, string revision)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = $"ls-tree -r --name-only {revision}";
    }

    /// <summary>
    /// コマンドを非同期で実行し、ファイル名のリストを返す。
    /// </summary>
    /// <returns>ファイルパスのリスト</returns>
    public async Task<List<string>> GetResultAsync()
    {
        var outs = new List<string>();

        try
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);
            proc.Start();

            while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { Length: > 0 } line)
                outs.Add(line);

            await proc.WaitForExitAsync().ConfigureAwait(false);
        }
        catch
        {
            // Ignore exceptions.
        }

        return outs;
    }
}
