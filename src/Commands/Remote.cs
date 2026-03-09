using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     リモートリポジトリの管理操作を提供するgitコマンド。
    ///     git remote のサブコマンドでリモートの追加・削除・名前変更・URL設定を行う。
    /// </summary>
    public class Remote : Command
    {
        /// <summary>
        ///     Remoteコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        public Remote(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        /// <summary>
        ///     新しいリモートを追加する。
        ///     git remote add を実行する。
        /// </summary>
        /// <param name="name">リモート名。</param>
        /// <param name="url">リモートURL。</param>
        /// <returns>コマンドが成功した場合はtrue。</returns>
        public async Task<bool> AddAsync(string name, string url)
        {
            // git remote add: 新しいリモートを追加する
            Args = $"remote add {name} {url}";
            return await ExecAsync();
        }

        /// <summary>
        ///     リモートを削除する。
        ///     git remote remove を実行する。
        /// </summary>
        /// <param name="name">削除するリモート名。</param>
        /// <returns>コマンドが成功した場合はtrue。</returns>
        public async Task<bool> DeleteAsync(string name)
        {
            // git remote remove: 指定リモートを削除する
            Args = $"remote remove {name}";
            return await ExecAsync();
        }

        /// <summary>
        ///     リモートの名前を変更する。
        ///     git remote rename を実行する。
        /// </summary>
        /// <param name="name">現在のリモート名。</param>
        /// <param name="to">新しいリモート名。</param>
        /// <returns>コマンドが成功した場合はtrue。</returns>
        public async Task<bool> RenameAsync(string name, string to)
        {
            // git remote rename: リモート名を変更する
            Args = $"remote rename {name} {to}";
            return await ExecAsync();
        }

        /// <summary>
        ///     リモートの古い追跡ブランチを削除する。
        ///     git remote prune を実行する。
        /// </summary>
        /// <param name="name">プルーニング対象のリモート名。</param>
        /// <returns>コマンドが成功した場合はtrue。</returns>
        public async Task<bool> PruneAsync(string name)
        {
            // git remote prune: リモートに存在しなくなった追跡ブランチを削除する
            Args = $"remote prune {name}";
            return await ExecAsync();
        }

        /// <summary>
        ///     リモートのURLを取得する。
        ///     git remote get-url を実行する。
        /// </summary>
        /// <param name="name">リモート名。</param>
        /// <param name="isPush">プッシュ用URLを取得するかどうか。</param>
        /// <returns>リモートのURL文字列。取得失敗時は空文字列。</returns>
        public async Task<string> GetURLAsync(string name, bool isPush)
        {
            // git remote get-url [--push]: リモートのフェッチ/プッシュURLを取得する
            Args = "remote get-url" + (isPush ? " --push " : " ") + name;

            var rs = await ReadToEndAsync();
            return rs.IsSuccess ? rs.StdOut.Trim() : string.Empty;
        }

        /// <summary>
        ///     リモートのURLを設定する。
        ///     git remote set-url を実行する。
        /// </summary>
        /// <param name="name">リモート名。</param>
        /// <param name="url">設定するURL。</param>
        /// <param name="isPush">プッシュ用URLを設定するかどうか。</param>
        /// <returns>コマンドが成功した場合はtrue。</returns>
        public async Task<bool> SetURLAsync(string name, string url, bool isPush)
        {
            // git remote set-url [--push]: リモートのフェッチ/プッシュURLを設定する
            Args = "remote set-url" + (isPush ? " --push " : " ") + $"{name} {url}";
            return await ExecAsync();
        }

        /// <summary>
        ///     リモートに指定ブランチが存在するかを確認する。
        ///     git ls-remote を実行して結果を確認する。
        /// </summary>
        /// <param name="remote">確認するリモート名。</param>
        /// <param name="branch">確認するブランチ名。</param>
        /// <returns>リモートにブランチが存在すればtrue。</returns>
        public async Task<bool> HasBranchAsync(string remote, string branch)
        {
            // リモートに紐づくSSH秘密鍵パスをgit configから取得する
            SSHKey = await new Config(WorkingDirectory).GetAsync($"remote.{remote}.sshkey");

            // git ls-remote: リモートの参照一覧を取得して指定ブランチの存在を確認する
            Args = $"ls-remote {remote} {branch}";

            var rs = await ReadToEndAsync();
            return rs.IsSuccess && rs.StdOut.Trim().Length > 0;
        }
    }
}
