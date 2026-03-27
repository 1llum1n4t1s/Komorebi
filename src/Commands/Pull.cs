using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     リモートリポジトリからプル（フェッチ＋マージ/リベース）するgitコマンド。
///     git pull --verbose --progress を実行する。
/// </summary>
public class Pull : Command
{
    /// <summary>
    ///     Pullコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="remote">プル元のリモート名。</param>
    /// <param name="branch">プル対象のブランチ名。</param>
    /// <param name="useRebase">マージの代わりにリベースを使用するかどうか。</param>
    public Pull(string repo, string remote, string branch, bool useRebase)
    {
        _remote = remote;

        WorkingDirectory = repo;
        Context = repo;

        var builder = new StringBuilder(512);

        // git pull --verbose --progress: 詳細情報と進捗を表示してプルする
        builder.Append("pull --verbose --progress ");

        // --rebase=true: マージの代わりにリベースを使用する
        if (useRebase)
            builder.Append("--rebase=true ");

        // プル元のリモートとブランチを指定する
        builder.Append(remote).Append(' ').Append(branch);

        Args = builder.ToString();
    }

    /// <summary>
    ///     SSH鍵を設定してからプルを実行する（基底クラスの共通メソッドを使用）。
    /// </summary>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public Task<bool> RunAsync() => ExecWithSSHKeyAsync(_remote);

    /// <summary>
    ///     操作対象のリモート名。
    /// </summary>
    private readonly string _remote;
}
