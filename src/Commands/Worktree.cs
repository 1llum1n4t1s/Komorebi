using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// ワークツリーの管理操作を提供するgitコマンド。
/// git worktree のサブコマンドでワークツリーの一覧取得・追加・削除・ロック管理を行う。
/// </summary>
public class Worktree : Command
{
    /// <summary>
    /// Worktreeコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    public Worktree(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
    }

    /// <summary>
    /// 全てのワークツリーを一覧取得する。
    /// git worktree list --porcelain を実行してパースする。
    /// </summary>
    /// <returns>ワークツリー情報のリスト。</returns>
    public async Task<List<Models.Worktree>> ReadAllAsync()
    {
        // git worktree list --porcelain: 機械解析しやすい形式でワークツリー一覧を取得する
        Args = "worktree list --porcelain";

        var rs = await ReadToEndAsync().ConfigureAwait(false);
        var worktrees = new List<Models.Worktree>();
        Models.Worktree last = null;
        if (rs.IsSuccess)
        {
            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // "worktree <path>" 行: 新しいワークツリーエントリの開始
                if (line.StartsWith("worktree ", StringComparison.Ordinal))
                {
                    last = new Models.Worktree() { FullPath = line[9..].Trim() };
                    worktrees.Add(last);
                    continue;
                }

                if (last is null)
                    continue;

                // "bare" 行: ベアリポジトリであることを示す
                if (line.StartsWith("bare", StringComparison.Ordinal))
                {
                    last.IsBare = true;
                }
                // "HEAD <sha>" 行: HEADのコミットSHA
                else if (line.StartsWith("HEAD ", StringComparison.Ordinal))
                {
                    last.Head = line[5..].Trim();
                }
                // "branch <ref>" 行: チェックアウトしているブランチの参照
                else if (line.StartsWith("branch ", StringComparison.Ordinal))
                {
                    last.Branch = line[7..].Trim();
                }
                // "detached" 行: デタッチドHEAD状態
                else if (line.StartsWith("detached", StringComparison.Ordinal))
                {
                    last.IsDetached = true;
                }
                // "locked" 行: ワークツリーがロックされている
                else if (line.StartsWith("locked", StringComparison.Ordinal))
                {
                    last.IsLocked = true;
                }
            }
        }

        return worktrees;
    }

    /// <summary>
    /// 新しいワークツリーを追加する。
    /// git worktree add を実行する。
    /// </summary>
    /// <param name="fullpath">ワークツリーの作成先フルパス。</param>
    /// <param name="name">チェックアウトするブランチ名。</param>
    /// <param name="createNew">新しいブランチを作成するかどうか。</param>
    /// <param name="tracking">追跡するリモートブランチ。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> AddAsync(string fullpath, string name, bool createNew, string tracking)
    {
        var builder = new StringBuilder(1024);

        // git worktree add: 新しいワークツリーを作成する
        builder.Append("worktree add ");

        // --track: リモートブランチを追跡する
        if (!string.IsNullOrEmpty(tracking))
            builder.Append("--track ");

        // -b: 新しいブランチを作成する / -B: 既存ブランチをリセットして使用する
        if (!string.IsNullOrEmpty(name))
            builder.Append(createNew ? "-b " : "-B ").Append(name).Append(' ');

        // ワークツリーの作成先パスを指定する
        builder.Append(fullpath.Quoted()).Append(' ');

        // チェックアウトする開始点を指定する
        if (!string.IsNullOrEmpty(tracking))
            builder.Append(tracking);
        else if (!string.IsNullOrEmpty(name) && !createNew)
            builder.Append(name);

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 無効なワークツリー情報を削除する。
    /// git worktree prune を実行する。
    /// </summary>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> PruneAsync()
    {
        // git worktree prune -v: 無効なワークツリー情報を詳細表示付きで削除する
        Args = "worktree prune -v";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// ワークツリーをロックする。
    /// git worktree lock を実行する。
    /// </summary>
    /// <param name="fullpath">ロックするワークツリーのフルパス。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> LockAsync(string fullpath)
    {
        // git worktree lock: ワークツリーをロックして誤削除を防ぐ
        Args = $"worktree lock {fullpath.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// ワークツリーのロックを解除する。
    /// git worktree unlock を実行する。
    /// </summary>
    /// <param name="fullpath">ロック解除するワークツリーのフルパス。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> UnlockAsync(string fullpath)
    {
        // git worktree unlock: ワークツリーのロックを解除する
        Args = $"worktree unlock {fullpath.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// ワークツリーを削除する。
    /// git worktree remove を実行する。
    /// </summary>
    /// <param name="fullpath">削除するワークツリーのフルパス。</param>
    /// <param name="force">強制削除するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> RemoveAsync(string fullpath, bool force)
    {
        // git worktree remove [-f]: ワークツリーを削除する（-fで強制削除）
        if (force)
            Args = $"worktree remove -f {fullpath.Quoted()}";
        else
            Args = $"worktree remove {fullpath.Quoted()}";

        return await ExecAsync().ConfigureAwait(false);
    }
}
