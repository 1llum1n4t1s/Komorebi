using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     リモートリポジトリにプッシュするgitコマンド。
    ///     git push --progress --verbose を実行する。
    /// </summary>
    public class Push : Command
    {
        /// <summary>
        ///     ブランチプッシュ用のPushコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="local">プッシュ元のローカルブランチ名。</param>
        /// <param name="remote">プッシュ先のリモート名。</param>
        /// <param name="remoteBranch">プッシュ先のリモートブランチ名。</param>
        /// <param name="withTags">タグも一緒にプッシュするかどうか。</param>
        /// <param name="checkSubmodules">サブモジュールのプッシュ状態をチェックするかどうか。</param>
        /// <param name="track">上流ブランチとして追跡設定するかどうか。</param>
        /// <param name="force">強制プッシュ（force-with-lease）するかどうか。</param>
        public Push(string repo, string local, string remote, string remoteBranch, bool withTags, bool checkSubmodules, bool track, bool force)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(1024);

            // git push --progress --verbose: 進捗と詳細情報を表示してプッシュする
            builder.Append("push --progress --verbose ");

            // --tags: 全てのタグをプッシュする
            if (withTags)
                builder.Append("--tags ");

            // --recurse-submodules=check: サブモジュールがプッシュ済みかチェックする
            if (checkSubmodules)
                builder.Append("--recurse-submodules=check ");

            // -u: 上流ブランチの追跡設定を行う
            if (track)
                builder.Append("-u ");

            // --force-with-lease: リモートが変更されていなければ強制プッシュする
            if (force)
                builder.Append("--force-with-lease ");

            // <remote> <local>:<remoteBranch> 形式でプッシュ先を指定する
            builder.Append(remote).Append(' ').Append(local).Append(':').Append(remoteBranch);
            Args = builder.ToString();
        }

        /// <summary>
        ///     参照のプッシュまたは削除用のPushコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="remote">プッシュ先のリモート名。</param>
        /// <param name="refname">プッシュまたは削除する参照名。</param>
        /// <param name="isDelete">参照を削除するかどうか。</param>
        public Push(string repo, string remote, string refname, bool isDelete)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(512);

            // git push: リモートに参照をプッシュする
            builder.Append("push ");

            // --delete: リモートの参照を削除する
            if (isDelete)
                builder.Append("--delete ");

            // リモートと参照名を指定する
            builder.Append(remote).Append(' ').Append(refname);

            Args = builder.ToString();
        }

        /// <summary>
        ///     SSH鍵を設定してからプッシュを実行する。
        /// </summary>
        /// <returns>コマンドが成功した場合はtrue。</returns>
        public async Task<bool> RunAsync()
        {
            // リモートに紐づくSSH秘密鍵パスをgit configから取得する
            SSHKey = await new Config(WorkingDirectory).GetAsync($"remote.{_remote}.sshkey").ConfigureAwait(false);

            // プッシュを非同期で実行する
            return await ExecAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     操作対象のリモート名。
        /// </summary>
        private readonly string _remote;
    }
}
