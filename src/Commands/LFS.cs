using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     Git LFS (Large File Storage) の各種操作を提供するコマンドクラス。
///     git lfs のサブコマンドを使用してLFSのインストール、追跡、同期、ロック管理を行う。
/// </summary>
public class LFS : Command
{
    /// <summary>
    ///     LFSコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    public LFS(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
    }

    /// <summary>
    ///     Git LFSをリポジトリにインストールする。
    ///     git lfs install --local を実行する。
    /// </summary>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> InstallAsync()
    {
        // git lfs install --local: ローカルリポジトリにLFSフックを設定する
        Args = "lfs install --local";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     指定パターンのファイルをLFS追跡対象に追加する。
    ///     git lfs track を実行する。
    /// </summary>
    /// <param name="pattern">追跡パターン（例: "*.psd"）。</param>
    /// <param name="isFilenameMode">ファイル名モードで追跡するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> TrackAsync(string pattern, bool isFilenameMode)
    {
        var builder = new StringBuilder();

        // git lfs track: 指定パターンのファイルをLFS管理に追加する
        builder.Append("lfs track ");

        // --filename: パスではなくファイル名でマッチさせる
        builder.Append(isFilenameMode ? "--filename " : string.Empty);
        builder.Append(pattern.Quoted());

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     リモートからLFSオブジェクトをフェッチする。
    ///     git lfs fetch を実行する。
    /// </summary>
    /// <param name="remote">フェッチ元のリモート名。</param>
    public async Task FetchAsync(string remote)
    {
        // git lfs fetch: リモートからLFSオブジェクトをダウンロードする
        Args = $"lfs fetch {remote}";
        await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     リモートからLFSオブジェクトをプルする。
    ///     git lfs pull を実行する。
    /// </summary>
    /// <param name="remote">プル元のリモート名。</param>
    public async Task PullAsync(string remote)
    {
        // git lfs pull: リモートからLFSオブジェクトをダウンロードしてチェックアウトする
        Args = $"lfs pull {remote}";
        await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     リモートにLFSオブジェクトをプッシュする。
    ///     git lfs push を実行する。
    /// </summary>
    /// <param name="remote">プッシュ先のリモート名。</param>
    public async Task PushAsync(string remote)
    {
        // git lfs push: LFSオブジェクトをリモートにアップロードする
        Args = $"lfs push {remote}";
        await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     不要なLFSオブジェクトを削除する。
    ///     git lfs prune を実行する。
    /// </summary>
    public async Task PruneAsync()
    {
        // git lfs prune: 古い・不要なLFSオブジェクトをローカルから削除する
        Args = "lfs prune";
        await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     リモートのLFSファイルロック一覧を取得する。
    ///     git lfs locks --json を実行してJSON形式で結果を取得する。
    /// </summary>
    /// <param name="remote">対象のリモート名。</param>
    /// <returns>LFSロック情報のリスト。</returns>
    public async Task<List<Models.LFSLock>> GetLocksAsync(string remote)
    {
        // git lfs locks --json: ロック一覧をJSON形式で取得する
        Args = $"lfs locks --json --remote={remote}";

        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (rs.IsSuccess)
        {
            try
            {
                // JSON出力をLFSLockモデルのリストにデシリアライズする
                var locks = JsonSerializer.Deserialize(rs.StdOut, JsonCodeGen.Default.ListLFSLock);
                return locks;
            }
            catch
            {
                // デシリアライズ失敗時は無視する
            }
        }

        return [];
    }

    /// <summary>
    ///     リモートのLFSファイルをロックする。
    ///     git lfs lock を実行する。
    /// </summary>
    /// <param name="remote">対象のリモート名。</param>
    /// <param name="file">ロックするファイルパス。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> LockAsync(string remote, string file)
    {
        // git lfs lock: 指定ファイルをロックする
        Args = $"lfs lock --remote={remote} {file.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     リモートのLFSファイルロックを解除する。
    ///     git lfs unlock を実行する。
    /// </summary>
    /// <param name="remote">対象のリモート名。</param>
    /// <param name="file">ロック解除するファイルパス。</param>
    /// <param name="force">他人のロックも強制解除するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> UnlockAsync(string remote, string file, bool force)
    {
        var builder = new StringBuilder();

        // git lfs unlock: 指定ファイルのロックを解除する
        builder
            .Append("lfs unlock --remote=")
            .Append(remote)
            .Append(force ? " -f " : " ")
            .Append(file.Quoted());

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     複数のLFSファイルロックを一括解除する。
    ///     git lfs unlock を複数ファイル指定で実行する。
    /// </summary>
    /// <param name="remote">対象のリモート名。</param>
    /// <param name="files">ロック解除するファイルパスのリスト。</param>
    /// <param name="force">他人のロックも強制解除するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> UnlockMultipleAsync(string remote, List<string> files, bool force)
    {
        var builder = new StringBuilder();

        // git lfs unlock: 複数ファイルのロックを一括解除する
        builder
            .Append("lfs unlock --remote=")
            .Append(remote)
            .Append(force ? " -f" : " ");

        // 各ファイルパスを引数に追加する
        foreach (string file in files)
            builder.Append(' ').Append(file.Quoted());

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }
}
