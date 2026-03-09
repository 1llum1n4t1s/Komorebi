using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
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
        ///     SSH鍵を設定してからプルを実行する。
        /// </summary>
        /// <returns>コマンドが成功した場合はtrue。</returns>
        public async Task<bool> RunAsync()
        {
            // リモートに紐づくSSH秘密鍵パスをgit configから取得する
            SSHKey = await new Config(WorkingDirectory).GetAsync($"remote.{_remote}.sshkey").ConfigureAwait(false);

            // プルを非同期で実行する
            return await ExecAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     操作対象のリモート名。
        /// </summary>
        private readonly string _remote;
    }
}
