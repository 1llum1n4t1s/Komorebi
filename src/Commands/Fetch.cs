using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     リモートリポジトリから最新の情報を取得するgitコマンド。
    ///     git fetch --progress --verbose を実行する。
    /// </summary>
    public class Fetch : Command
    {
        /// <summary>
        ///     タグ設定と強制オプション付きでFetchコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="remote">フェッチ元のリモート名。</param>
        /// <param name="noTags">タグを取得しないかどうか。</param>
        /// <param name="force">強制的にフェッチするかどうか。</param>
        public Fetch(string repo, string remote, bool noTags, bool force)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(512);

            // git fetch --progress --verbose: 進捗と詳細情報を表示してフェッチする
            builder.Append("fetch --progress --verbose ");

            // --no-tags / --tags: タグの取得を制御する
            builder.Append(noTags ? "--no-tags " : "--tags ");

            // --force: リモートの参照でローカルの参照を強制上書きする
            if (force)
                builder.Append("--force ");

            // フェッチ元のリモート名を指定する
            builder.Append(remote);

            Args = builder.ToString();
        }

        /// <summary>
        ///     シンプルなFetchコマンドを初期化する（エラーを例外として上げない）。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="remote">フェッチ元のリモート名。</param>
        public Fetch(string repo, string remote)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;

            // バックグラウンドフェッチ用のためエラーを抑制する
            RaiseError = false;

            // git fetch --progress --verbose <remote>: 指定リモートからフェッチする
            Args = $"fetch --progress --verbose {remote}";
        }

        /// <summary>
        ///     特定のリモートブランチをローカルブランチにフェッチするコンストラクタ。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="local">フェッチ先のローカルブランチ。</param>
        /// <param name="remote">フェッチ元のリモートブランチ。</param>
        public Fetch(string repo, Models.Branch local, Models.Branch remote)
        {
            _remote = remote.Remote;

            WorkingDirectory = repo;
            Context = repo;

            // git fetch <remote> <remoteBranch>:<localBranch>: リモートブランチをローカルブランチに直接フェッチする
            Args = $"fetch --progress --verbose {remote.Remote} {remote.Name}:{local.Name}";
        }

        /// <summary>
        ///     SSH鍵を設定してからフェッチを実行する（基底クラスの共通メソッドを使用）。
        /// </summary>
        /// <returns>コマンドが成功した場合はtrue。</returns>
        public Task<bool> RunAsync() => ExecWithSSHKeyAsync(_remote);

        /// <summary>
        ///     操作対象のリモート名。
        /// </summary>
        private readonly string _remote;
    }
}
