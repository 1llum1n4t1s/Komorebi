namespace Komorebi.Models;

/// <summary>
/// gitのstderrメッセージをパターンマッチし、対応するローカライズキーを返すヘルパー。
/// 元の英語メッセージはそのまま表示し、翻訳+対処法をヒントとして追加表示する。
/// </summary>
public static partial class GitErrorHelper
{
    /// <summary>
    /// gitエラーメッセージに対応するローカライズキーを返す。
    /// 該当パターンがない場合はnullを返す。
    /// </summary>
    public static string GetHintKey(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return string.Empty;

        // index.lock: 他のgitプロセスが実行中、またはクラッシュ後のロック残留
        if (errorMessage.Contains("index.lock", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("Unable to create", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains(".lock", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.IndexLock";

        // マージコンフリクト
        if (errorMessage.Contains("CONFLICT", System.StringComparison.Ordinal) ||
            errorMessage.Contains("Merge conflict", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("fix conflicts", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Automatic merge failed", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.MergeConflict";

        // Detached HEAD
        if (errorMessage.Contains("detached HEAD", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("HEAD detached", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.DetachedHead";

        // リモートにpushが拒否された（non-fast-forward）
        if (errorMessage.Contains("non-fast-forward", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Updates were rejected", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("rejected]", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("fetch first", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.PushRejected";

        // 未コミット変更があるため操作できない
        if (errorMessage.Contains("uncommitted changes", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("local changes", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("overwritten", System.StringComparison.OrdinalIgnoreCase)) ||
            errorMessage.Contains("Please commit your changes", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Please, commit your changes", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.UncommittedChanges";

        // 認証失敗
        if (errorMessage.Contains("Authentication failed", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("could not read Username", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("Permission denied", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("publickey", System.StringComparison.OrdinalIgnoreCase)) ||
            errorMessage.Contains("fatal: could not read Password", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.AuthFailed";

        // リモートが見つからない
        if (errorMessage.Contains("Could not resolve host", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("repository not found", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("does not appear to be a git repository", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.RemoteNotFound";

        // ブランチが既に存在する
        if (errorMessage.Contains("already exists", System.StringComparison.OrdinalIgnoreCase) &&
            (errorMessage.Contains("branch", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("tag", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.AlreadyExists";

        // untracked filesが上書きされる
        if (errorMessage.Contains("untracked working tree files", System.StringComparison.OrdinalIgnoreCase) &&
            errorMessage.Contains("overwritten", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.UntrackedOverwritten";

        // ネットワークエラー
        if (errorMessage.Contains("Connection timed out", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Connection refused", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Could not connect", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("unable to access", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("SSL", System.StringComparison.OrdinalIgnoreCase)) ||
            errorMessage.Contains("Network is unreachable", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.NetworkError";

        // リベース中
        if (errorMessage.Contains("rebase in progress", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("interactive rebase already started", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.RebaseInProgress";

        // 空のコミット
        if (errorMessage.Contains("nothing to commit", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("nothing added to commit", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.NothingToCommit";

        // サブモジュール
        if (errorMessage.Contains("submodule", System.StringComparison.OrdinalIgnoreCase) &&
            (errorMessage.Contains("not initialized", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("not a git repository", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.SubmoduleError";

        // LFS
        if (errorMessage.Contains("git-lfs", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("lfs", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("not installed", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.LfsError";

        // ファイルが大きすぎる
        if (errorMessage.Contains("large files", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("file is too large", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("exceeds GitHub's file size limit", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.FileTooLarge";

        // チェリーピックのコンフリクト
        if (errorMessage.Contains("cherry-pick", System.StringComparison.OrdinalIgnoreCase) &&
            (errorMessage.Contains("conflict", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("failed", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.CherryPickConflict";

        // マージ中の操作（マージ未完了）
        if (errorMessage.Contains("merge is not possible", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("You have not concluded your merge", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("MERGE_HEAD exists", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.MergeNotFinished";

        // リモートブランチの追跡設定がない
        if (errorMessage.Contains("no tracking information", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("no upstream branch", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("does not track a remote branch", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("The current branch .* has no upstream", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.NoUpstream";

        // 浅いクローン（shallow clone）の制限
        if (errorMessage.Contains("shallow update not allowed", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("shallow clone", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("grafted", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("shallow", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.ShallowClone";

        // ディスク容量不足
        if (errorMessage.Contains("No space left on device", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("not enough disk space", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("out of disk space", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.DiskFull";

        // パーミッション拒否（ファイルシステム）
        if (errorMessage.Contains("Permission denied", System.StringComparison.OrdinalIgnoreCase) &&
            !errorMessage.Contains("publickey", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.PermissionDenied";

        // 不正なリファレンス名
        if (errorMessage.Contains("not a valid ref name", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("is not a valid branch name", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("bad revision", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("unknown revision", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.InvalidRef";

        // リモートの参照先が見つからない（ブランチ削除済み等）
        if (errorMessage.Contains("couldn't find remote ref", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Remote branch .* not found", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.RemoteRefNotFound";

        // コミットがリセットされた/存在しない
        if (errorMessage.Contains("bad object", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("missing object", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("object file .* is empty", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.CorruptObject";

        // gitリポジトリではない
        if (errorMessage.Contains("not a git repository", System.StringComparison.OrdinalIgnoreCase) &&
            !errorMessage.Contains("submodule", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.NotARepo";

        // スタッシュ関連エラー
        if (errorMessage.Contains("No stash entries found", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("Too few arguments", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("stash", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.StashError";

        // クリーンフィルター/smudgeフィルターエラー
        if (errorMessage.Contains("clean filter", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("smudge filter", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("external filter", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("failed", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.FilterError";

        // hookの実行失敗
        if (errorMessage.Contains("hook", System.StringComparison.OrdinalIgnoreCase) &&
            (errorMessage.Contains("rejected", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("failed", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("exit code", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.HookFailed";

        // パッチの適用失敗
        if (errorMessage.Contains("patch does not apply", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("patch failed", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("error: could not apply", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.PatchFailed";

        // SSHホスト鍵変更警告（中間者攻撃の可能性）
        if (errorMessage.Contains("REMOTE HOST IDENTIFICATION HAS CHANGED", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("host key", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("has changed", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.HostKeyChanged";

        // SSHホスト鍵検証失敗（known_hostsに未登録、または接続拒否）
        if (errorMessage.Contains("Host key verification failed", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.HostKeyVerifyFailed";

        // worktree関連のエラー
        if (errorMessage.Contains("worktree", System.StringComparison.OrdinalIgnoreCase) &&
            (errorMessage.Contains("already checked out", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("is already linked", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("locked", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.WorktreeError";

        return string.Empty;
    }

    /// <summary>
    /// SSHのaskpassメッセージに対応するローカライズキーを返す。
    /// 該当パターンがない場合は空文字を返す。
    /// </summary>
    public static string GetAskpassHintKey(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        // ホスト鍵変更警告（中間者攻撃の可能性）
        if (message.Contains("REMOTE HOST IDENTIFICATION HAS CHANGED", System.StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Host key verification failed", System.StringComparison.OrdinalIgnoreCase))
            return "Text.Askpass.Hint.HostKeyChanged";

        // 初回接続時のホスト鍵確認
        if (message.Contains("authenticity", System.StringComparison.OrdinalIgnoreCase) &&
            (message.Contains("can't be established", System.StringComparison.OrdinalIgnoreCase) ||
             message.Contains("continue connecting", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.Askpass.Hint.HostKeyNew";

        // パスフレーズ入力（"Enter passphrase for key ..." のプロンプト）
        if (message.Contains("Enter passphrase", System.StringComparison.OrdinalIgnoreCase))
            return "Text.Askpass.Hint.Passphrase";

        // パスワード入力（"Password for ..." のプロンプト）
        if (message.Contains("Password for", System.StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Enter password", System.StringComparison.OrdinalIgnoreCase))
            return "Text.Askpass.Hint.Password";

        return string.Empty;
    }
}
