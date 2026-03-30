using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 指定ファイルがGit LFSで管理されているかどうかを判定するgitコマンド。
/// git check-attr -z filter を実行してfilter属性がlfsかどうかを確認する。
/// </summary>
public class IsLFSFiltered : Command
{
    /// <summary>
    /// ワーキングツリーのファイルに対するLFS判定コマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="path">判定対象のファイルパス。</param>
    public IsLFSFiltered(string repo, string path)
    {
        WorkingDirectory = repo;
        Context = repo;

        // git check-attr -z filter: NULLセパレータ形式でfilter属性を取得する
        Args = $"check-attr -z filter {path.Quoted()}";

        // LFS判定はエラーを抑制する
        RaiseError = false;
    }

    /// <summary>
    /// 特定コミットのファイルに対するLFS判定コマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="sha">参照するコミットSHA。</param>
    /// <param name="path">判定対象のファイルパス。</param>
    public IsLFSFiltered(string repo, string sha, string path)
    {
        WorkingDirectory = repo;
        Context = repo;

        // git check-attr --source <sha> -z filter: 指定コミット時点のfilter属性を取得する
        Args = $"check-attr --source {sha} -z filter {path.Quoted()}";

        // LFS判定はエラーを抑制する
        RaiseError = false;
    }

    /// <summary>
    /// LFS判定の結果を同期的に取得する。
    /// </summary>
    /// <returns>LFSで管理されていればtrue。</returns>
    public bool GetResult()
    {
        return Parse(ReadToEnd());
    }

    /// <summary>
    /// LFS判定の結果を非同期で取得する。
    /// </summary>
    /// <returns>LFSで管理されていればtrue。</returns>
    public async Task<bool> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        return Parse(rs);
    }

    /// <summary>
    /// コマンドの実行結果を解析してLFS管理かどうかを判定する。
    /// </summary>
    /// <param name="rs">コマンドの実行結果。</param>
    /// <returns>filter属性がlfsであればtrue。</returns>
    private static bool Parse(Result rs)
    {
        // 出力に "filter\0lfs" が含まれていればLFS管理されている
        return rs.IsSuccess && rs.StdOut.Contains("filter\0lfs");
    }
}
