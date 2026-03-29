using System.IO;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 指定パスがベアリポジトリかどうかを判定するgitコマンド。
/// git rev-parse --is-bare-repository を実行する。
/// </summary>
public class IsBareRepository : Command
{
    /// <summary>
    /// IsBareRepositoryコマンドを初期化する。
    /// </summary>
    /// <param name="path">判定対象のディレクトリパス。</param>
    public IsBareRepository(string path)
    {
        WorkingDirectory = path;

        // git rev-parse --is-bare-repository: ベアリポジトリかどうかを判定する
        Args = "rev-parse --is-bare-repository";
    }

    /// <summary>
    /// ベアリポジトリ判定の結果を同期的に取得する。
    /// gitコマンド実行前にディレクトリ構造を事前チェックする。
    /// </summary>
    /// <returns>ベアリポジトリであればtrue。</returns>
    public bool GetResult()
    {
        // refs/、objects/ディレクトリとHEADファイルの存在を事前チェックする
        if (!Directory.Exists(Path.Combine(WorkingDirectory, "refs")) ||
            !Directory.Exists(Path.Combine(WorkingDirectory, "objects")) ||
            !File.Exists(Path.Combine(WorkingDirectory, "HEAD")))
            return false;

        // git rev-parseを実行し、出力が"true"かどうかを確認する
        var rs = ReadToEnd();
        return rs.IsSuccess && rs.StdOut.Trim() == "true";
    }

    /// <summary>
    /// ベアリポジトリ判定の結果を非同期で取得する。
    /// gitコマンド実行前にディレクトリ構造を事前チェックする。
    /// </summary>
    /// <returns>ベアリポジトリであればtrue。</returns>
    public async Task<bool> GetResultAsync()
    {
        // refs/、objects/ディレクトリとHEADファイルの存在を事前チェックする
        if (!Directory.Exists(Path.Combine(WorkingDirectory, "refs")) ||
            !Directory.Exists(Path.Combine(WorkingDirectory, "objects")) ||
            !File.Exists(Path.Combine(WorkingDirectory, "HEAD")))
            return false;

        // git rev-parseを実行し、出力が"true"かどうかを確認する
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        return rs.IsSuccess && rs.StdOut.Trim() == "true";
    }
}
