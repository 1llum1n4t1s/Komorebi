using System.Threading;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// リモートリポジトリの管理操作を提供するgitコマンド。
/// git remote のサブコマンドでリモートの追加・削除・名前変更・URL設定を行う。
/// </summary>
public class Remote : Command
{
    /// <summary>
    /// Remoteコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    public Remote(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
    }

    /// <summary>
    /// 新しいリモートを追加する。
    /// git remote add を実行する。
    /// </summary>
    /// <param name="name">リモート名。</param>
    /// <param name="url">リモートURL。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> AddAsync(string name, string url)
    {
        // git remote add: 新しいリモートを追加する
        Args = $"remote add {name.Quoted()} {url.Quoted()}";
        return await ExecAsync();
    }

    /// <summary>
    /// リモートを削除する。
    /// git remote remove を実行する。
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
    /// リモートの名前を変更する。
    /// git remote rename を実行する。
    /// </summary>
    /// <param name="name">現在のリモート名。</param>
    /// <param name="to">新しいリモート名。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> RenameAsync(string name, string to)
    {
        // git remote rename: リモート名を変更する
        Args = $"remote rename {name.Quoted()} {to.Quoted()}";
        return await ExecAsync();
    }

    /// <summary>
    /// リモートの古い追跡ブランチを削除する。
    /// git remote prune を実行する。
    /// </summary>
    /// <param name="name">プルーニング対象のリモート名。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> PruneAsync(string name)
    {
        // git remote prune: リモートに存在しなくなった追跡ブランチを削除する
        Args = $"remote prune {name.Quoted()}";
        return await ExecAsync();
    }

    /// <summary>
    /// リモートのURLを取得する。
    /// git remote get-url を実行する。
    /// </summary>
    /// <param name="name">リモート名。</param>
    /// <param name="isPush">プッシュ用URLを取得するかどうか。</param>
    /// <returns>リモートのURL文字列。取得失敗時は空文字列。</returns>
    public async Task<string> GetURLAsync(string name, bool isPush)
    {
        // git remote get-url [--push]: リモートのフェッチ/プッシュURLを取得する
        Args = "remote get-url" + (isPush ? " --push " : " ") + name.Quoted();

        var rs = await ReadToEndAsync();
        return rs.IsSuccess ? rs.StdOut.Trim() : string.Empty;
    }

    /// <summary>
    /// リモートのURLを設定する。
    /// git remote set-url を実行する。
    /// </summary>
    /// <param name="name">リモート名。</param>
    /// <param name="url">設定するURL。</param>
    /// <param name="isPush">プッシュ用URLを設定するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> SetURLAsync(string name, string url, bool isPush)
    {
        // git remote set-url [--push]: リモートのフェッチ/プッシュURLを設定する
        Args = "remote set-url" + (isPush ? " --push " : " ") + $"{name.Quoted()} {url.Quoted()}";
        return await ExecAsync();
    }

    /// <summary>
    /// リモートのプッシュ専用URLを削除してフェッチURLに戻す。
    /// git remote set-url --push --delete を実行する。
    /// </summary>
    /// <param name="name">リモート名。</param>
    /// <param name="pushUrl">削除するプッシュURL。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> DeletePushURLAsync(string name, string pushUrl)
    {
        // git remote set-url --push --delete: プッシュ専用URLを削除する
        Args = $"remote set-url --push --delete {name.Quoted()} {pushUrl.Quoted()}";
        return await ExecAsync();
    }

    /// <summary>
    /// リモートの到達可能性を確認する。
    /// git ls-remote --quiet を実行し、接続が成功すれば到達可能と判定する。
    /// 参照ゼロ件（空リポジトリ、タグのみのリポジトリ）も「接続できた」として到達可能扱いする。
    /// </summary>
    /// <param name="remote">確認するリモート名。</param>
    /// <param name="cancelToken">タイムアウト用のキャンセルトークン。</param>
    /// <returns>到達可能ならtrue、失敗・タイムアウト・認証エラーならfalse。</returns>
    public async Task<bool> CheckReachabilityAsync(string remote, CancellationToken cancelToken)
    {
        // リモート個別のSSHキー設定を反映する（git config を 1 回起動するコスト）
        await ResolveSSHKeyAsync(remote);
        return await RunLsRemoteAsync(remote, cancelToken);
    }

    /// <summary>
    /// 事前に解決済みの SSH キーを使ってリモートの到達可能性を確認する。
    /// 呼び出し側が <see cref="Config.ReadAllAsync"/> で全 remote.*.sshkey を一括取得済みの場合に使う。
    /// これにより、リモート 1 件ごとに git config プロセスを起動せずに済み、プロセス起動コストを削減する。
    /// </summary>
    /// <param name="remote">確認するリモート名。</param>
    /// <param name="preResolvedSSHKey">
    /// <see cref="Command.ResolveSSHKeyValue"/> で解決済みの SSH キーパス。空文字列はシステムデフォルト。
    /// </param>
    /// <param name="cancelToken">タイムアウト用のキャンセルトークン。</param>
    /// <returns>到達可能ならtrue、失敗・タイムアウト・認証エラーならfalse。</returns>
    public async Task<bool> CheckReachabilityAsync(string remote, string preResolvedSSHKey, CancellationToken cancelToken)
    {
        SSHKey = preResolvedSSHKey ?? string.Empty;
        return await RunLsRemoteAsync(remote, cancelToken);
    }

    /// <summary>
    /// <see cref="CheckReachabilityAsync(string, CancellationToken)"/> と
    /// <see cref="CheckReachabilityAsync(string, string, CancellationToken)"/> の共通実装部分。
    /// SSH キー解決後の ls-remote 実行と結果判定を行う。
    /// </summary>
    private async Task<bool> RunLsRemoteAsync(string remote, CancellationToken cancelToken)
    {
        // --quiet: 進捗表示を抑制。--exit-code と --heads は故意に付けない。
        //   --exit-code + --heads だと「ブランチゼロ件のリモート」が exit 2 になり、接続できているのに
        //   到達不可と誤判定されるため（新規作成直後の空リポ、タグ専用リポ等）。
        Args = $"ls-remote --quiet {remote.Quoted()}";

        // バックグラウンド確認のためエラートーストを抑制する
        RaiseError = false;
        CancellationToken = cancelToken;

        var rs = await ReadToEndAsync();
        return rs.IsSuccess;
    }

    /// <summary>
    /// リモートに指定ブランチが存在するかを確認する。
    /// git ls-remote を実行して結果を確認する。
    /// </summary>
    /// <param name="remote">確認するリモート名。</param>
    /// <param name="branch">確認するブランチ名。</param>
    /// <returns>リモートにブランチが存在すればtrue。</returns>
    public async Task<bool> HasBranchAsync(string remote, string branch)
    {
        // リモートに紐づくSSH秘密鍵パスをgit configから取得し、なければグローバル設定にフォールバック
        await ResolveSSHKeyAsync(remote);

        // git ls-remote: リモートの参照一覧を取得して指定ブランチの存在を確認する
        Args = $"ls-remote {remote.Quoted()} {branch.Quoted()}";

        var rs = await ReadToEndAsync();
        return rs.IsSuccess && rs.StdOut.Trim().Length > 0;
    }
}
