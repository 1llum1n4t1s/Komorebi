using System.Text.RegularExpressions;

namespace Komorebi.Models;

/// <summary>
/// gitのstderrメッセージをパターンマッチし、対応するローカライズキーを返すヘルパー。
/// 元の英語メッセージはそのまま表示し、翻訳+対処法をヒントとして追加表示する。
/// </summary>
public static partial class GitErrorHelper
{
    /// <summary>
    /// gitエラーメッセージに対応するローカライズキーを返す。
    /// 該当パターンがない場合は空文字を返す。
    /// </summary>
    public static string GetHintKey(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return string.Empty;

        // index.lock: 他のgitプロセスが実行中、またはクラッシュ後のロック残留
        if (errorMessage.Contains("index.lock", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("Unable to create", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains(".lock", System.StringComparison.OrdinalIgnoreCase)) ||
            (errorMessage.Contains("cannot lock ref", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("unable to create", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.IndexLock";

        // マージコンフリクト
        if (errorMessage.Contains("CONFLICT", System.StringComparison.Ordinal) ||
            errorMessage.Contains("Merge conflict", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("fix conflicts", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Automatic merge failed", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Resolve all conflicts manually", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("Stopping at", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("merge conflicts", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.MergeConflict";

        // Detached HEAD
        if (errorMessage.Contains("detached HEAD", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("HEAD detached", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.DetachedHead";

        // リモートのブランチ保護でpushが拒否された (PushRejected の前に判定: より特異)
        if (errorMessage.Contains("protected branch", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("GH006", System.StringComparison.Ordinal) &&
             errorMessage.Contains("Protected branch", System.StringComparison.OrdinalIgnoreCase)) ||
            errorMessage.Contains("refusing to allow", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("pre-receive hook declined", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("protected", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.ProtectedBranch";

        // リモートにpushが拒否された（non-fast-forward）
        if (errorMessage.Contains("non-fast-forward", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Updates were rejected", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("rejected]", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("fetch first", System.StringComparison.OrdinalIgnoreCase)) ||
            errorMessage.Contains("failed to push some refs", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("tip of your current branch is behind", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.PushRejected";

        // 未コミット変更があるため操作できない
        // 代表的な git メッセージ:
        //   - "You have uncommitted changes."
        //   - "You have unstaged changes." (例: pull --rebase 時)
        //   - "Your local changes to the following files would be overwritten by ..."
        //   - "Please commit your changes or stash them before you merge."
        //   - "Please commit or stash them." (pull --rebase 時の続報)
        //   - "cannot pull with rebase: You have unstaged changes."
        //   - "Cannot rebase: Your index contains uncommitted changes."
        //   - "Cannot rebase: You have unstaged changes."
        //   - "would be overwritten by checkout"
        if (errorMessage.Contains("uncommitted changes", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("unstaged changes", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("local changes", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("overwritten", System.StringComparison.OrdinalIgnoreCase)) ||
            errorMessage.Contains("Please commit your changes", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Please, commit your changes", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Please commit or stash", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("cannot pull with rebase", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("would be overwritten by", System.StringComparison.OrdinalIgnoreCase) &&
             (errorMessage.Contains("checkout", System.StringComparison.OrdinalIgnoreCase) ||
              errorMessage.Contains("merge", System.StringComparison.OrdinalIgnoreCase) ||
              errorMessage.Contains("rebase", System.StringComparison.OrdinalIgnoreCase))))
            return "Text.GitError.UncommittedChanges";

        // SSH秘密鍵のパーミッションが緩すぎる（認証失敗より先にチェック）
        if (errorMessage.Contains("UNPROTECTED PRIVATE KEY FILE", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("bad permissions", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("Permissions", System.StringComparison.Ordinal) &&
             errorMessage.Contains("are too open", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.KeyPermissionTooOpen";

        // SSHホスト鍵変更警告（中間者攻撃の可能性、認証失敗より先にチェック）
        if (errorMessage.Contains("REMOTE HOST IDENTIFICATION HAS CHANGED", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("host key", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("has changed", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.HostKeyChanged";

        // SSHホスト鍵検証失敗（known_hostsに未登録、または接続拒否）
        if (errorMessage.Contains("Host key verification failed", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.HostKeyVerifyFailed";

        // 認証失敗（HTTPS/SSHの両方をカバー）
        if (errorMessage.Contains("Authentication failed", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("could not read Username", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("could not read Password", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("Permission denied", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("publickey", System.StringComparison.OrdinalIgnoreCase)) ||
            errorMessage.Contains("Invalid username or password", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Bad credentials", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("terminal prompts disabled", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("HTTP 401", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("HTTP 403", System.StringComparison.OrdinalIgnoreCase)) ||
            errorMessage.Contains("requested URL returned error: 401", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("requested URL returned error: 403", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.AuthFailed";

        // リモートが見つからない / DNS解決失敗 / 不正なURL
        if (errorMessage.Contains("Could not resolve host", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("repository not found", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("does not appear to be a git repository", System.StringComparison.OrdinalIgnoreCase) &&
             !errorMessage.Contains("submodule", System.StringComparison.OrdinalIgnoreCase)) ||
            errorMessage.Contains("Repository .git/ does not exist", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("fatal: '", System.StringComparison.Ordinal) &&
             errorMessage.Contains("' does not appear to be a git repository", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.RemoteNotFound";

        // ブランチ/タグが既に存在する
        if ((errorMessage.Contains("already exists", System.StringComparison.OrdinalIgnoreCase) &&
             (errorMessage.Contains("branch", System.StringComparison.OrdinalIgnoreCase) ||
              errorMessage.Contains("tag", System.StringComparison.OrdinalIgnoreCase))) ||
            (errorMessage.Contains("cannot lock ref", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("exists", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("cannot create", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.AlreadyExists";

        // untracked filesが上書きされる
        if (errorMessage.Contains("untracked working tree files", System.StringComparison.OrdinalIgnoreCase) &&
            errorMessage.Contains("overwritten", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.UntrackedOverwritten";

        // ネットワークエラー (RPC / 接続 / SSL / プロキシなど)
        if (errorMessage.Contains("Connection timed out", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Connection refused", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Could not connect", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Couldn't connect to server", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Failed to connect to", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Network is unreachable", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("RPC failed", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("early EOF", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("transfer closed with outstanding read data", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("unable to access", System.StringComparison.OrdinalIgnoreCase) &&
             (errorMessage.Contains("SSL", System.StringComparison.OrdinalIgnoreCase) ||
              errorMessage.Contains("timed out", System.StringComparison.OrdinalIgnoreCase) ||
              errorMessage.Contains("resolve host", System.StringComparison.OrdinalIgnoreCase) ||
              errorMessage.Contains("Couldn't connect", System.StringComparison.OrdinalIgnoreCase) ||
              errorMessage.Contains("Failed to connect", System.StringComparison.OrdinalIgnoreCase))) ||
            errorMessage.Contains("Operation timed out", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("SSL certificate problem", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("server certificate verification failed", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.NetworkError";

        // リベース中
        if (errorMessage.Contains("rebase in progress", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("interactive rebase already started", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("It looks like 'git am' is in progress", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("rebase", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("already in progress", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.RebaseInProgress";

        // 空のコミット
        if (errorMessage.Contains("nothing to commit", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("nothing added to commit", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("no changes added to commit", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.NothingToCommit";

        // GPG署名失敗（commit/tag/rebase 時の "gpg failed to sign the data"）
        if (errorMessage.Contains("gpg failed to sign", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("failed to write commit object", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("gpg", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("signing failed", System.StringComparison.OrdinalIgnoreCase)) ||
            errorMessage.Contains("secret key not available", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Inappropriate ioctl for device", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.GpgSignFailed";

        // safe.directory 警告 (Windows / 共有ボリュームで頻発)
        if (errorMessage.Contains("dubious ownership", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("safe.directory", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.DubiousOwnership";

        // サブモジュール
        if (errorMessage.Contains("submodule", System.StringComparison.OrdinalIgnoreCase) &&
            (errorMessage.Contains("not initialized", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("not a git repository", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("No url found for submodule", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.SubmoduleError";

        // LFS
        if (errorMessage.Contains("git-lfs", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("lfs", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("not installed", System.StringComparison.OrdinalIgnoreCase)) ||
            (errorMessage.Contains("LFS", System.StringComparison.Ordinal) &&
             errorMessage.Contains("object", System.StringComparison.OrdinalIgnoreCase) &&
             (errorMessage.Contains("not found", System.StringComparison.OrdinalIgnoreCase) ||
              errorMessage.Contains("does not exist", System.StringComparison.OrdinalIgnoreCase))))
            return "Text.GitError.LfsError";

        // ファイルが大きすぎる
        if (errorMessage.Contains("large files", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("file is too large", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("exceeds GitHub's file size limit", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("File size limit exceeded", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.FileTooLarge";

        // チェリーピックのコンフリクト
        if (errorMessage.Contains("cherry-pick", System.StringComparison.OrdinalIgnoreCase) &&
            (errorMessage.Contains("conflict", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("failed", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("could not apply", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.CherryPickConflict";

        // マージ中の操作（マージ未完了）
        if (errorMessage.Contains("merge is not possible", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("You have not concluded your merge", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("MERGE_HEAD exists", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Exiting because of unfinished merge", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.MergeNotFinished";

        // リモートブランチの追跡設定がない
        if (errorMessage.Contains("no tracking information", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("no upstream branch", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("has no upstream", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("does not track a remote branch", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.NoUpstream";

        // 浅いクローン（shallow clone）の制限
        if (errorMessage.Contains("shallow update not allowed", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("shallow clone", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("cannot fetch shallow", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("grafted", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("shallow", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.ShallowClone";

        // ディスク容量不足
        if (errorMessage.Contains("No space left on device", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("not enough disk space", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("out of disk space", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("disk quota exceeded", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.DiskFull";

        // 不正なリファレンス名 (PermissionDenied の前に判定: より特異)
        if (errorMessage.Contains("not a valid ref name", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("is not a valid branch name", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("bad revision", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("unknown revision", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("ambiguous argument", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("refusing to update checked out branch", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.InvalidRef";

        // リモートの参照先が見つからない（ブランチ削除済み等）
        if (errorMessage.Contains("couldn't find remote ref", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("Remote branch", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("not found in upstream", System.StringComparison.OrdinalIgnoreCase)) ||
            (errorMessage.Contains("src refspec", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("does not match any", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.RemoteRefNotFound";

        // コミットがリセットされた/存在しない / オブジェクト破損
        if (errorMessage.Contains("bad object", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("missing object", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("bad signature", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("corrupt loose object", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("loose object", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("is corrupt", System.StringComparison.OrdinalIgnoreCase)) ||
            (errorMessage.Contains("object file", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("is empty", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.CorruptObject";

        // gitリポジトリではない
        if ((errorMessage.Contains("not a git repository", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("not in a git directory", System.StringComparison.OrdinalIgnoreCase)) &&
            !errorMessage.Contains("submodule", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.NotARepo";

        // スタッシュ関連エラー
        if (errorMessage.Contains("No stash entries found", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("is not a valid reference", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("stash", System.StringComparison.OrdinalIgnoreCase)) ||
            (errorMessage.Contains("Too few arguments", System.StringComparison.OrdinalIgnoreCase) &&
             errorMessage.Contains("stash", System.StringComparison.OrdinalIgnoreCase)) ||
            errorMessage.Contains("Stash entry is kept", System.StringComparison.OrdinalIgnoreCase))
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
             errorMessage.Contains("exit code", System.StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("declined", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.HookFailed";

        // パッチの適用失敗
        if (errorMessage.Contains("patch does not apply", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("patch failed", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("error: could not apply", System.StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("corrupt patch", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("error: patch fragment", System.StringComparison.OrdinalIgnoreCase)))
            return "Text.GitError.PatchFailed";

        // worktree関連のエラー
        // "is not a working tree" は worktree 文脈固有のため単独で判定（"worktree" 単語を含まない場合がある）
        if (errorMessage.Contains("is not a working tree", System.StringComparison.OrdinalIgnoreCase) ||
            (errorMessage.Contains("worktree", System.StringComparison.OrdinalIgnoreCase) &&
             (errorMessage.Contains("already checked out", System.StringComparison.OrdinalIgnoreCase) ||
              errorMessage.Contains("is already linked", System.StringComparison.OrdinalIgnoreCase) ||
              errorMessage.Contains("locked", System.StringComparison.OrdinalIgnoreCase) ||
              errorMessage.Contains("already exists", System.StringComparison.OrdinalIgnoreCase))))
            return "Text.GitError.WorktreeError";

        // パーミッション拒否（ファイルシステム） - 最後にチェック（他カテゴリの広い "Permission denied" を上書きされないように）
        if (errorMessage.Contains("Permission denied", System.StringComparison.OrdinalIgnoreCase) &&
            !errorMessage.Contains("publickey", System.StringComparison.OrdinalIgnoreCase))
            return "Text.GitError.PermissionDenied";

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

    /// <summary>
    /// SSH秘密鍵パーミッションエラーのメッセージから鍵ファイルパスを抽出する。
    /// "Permissions 0755 for '/path/to/key' are too open" または
    /// "Load key \"/path/to/key\": bad permissions" パターンに対応。
    /// </summary>
    /// <returns>抽出されたファイルパス。抽出できない場合はnull。</returns>
    public static string ExtractKeyPathFromPermissionError(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return null;

        // "Permissions XXXX for '/path/to/key' are too open"
        var match = PermissionKeyPathRegex().Match(errorMessage);
        if (match.Success)
            return match.Groups[1].Value;

        // "Load key \"/path/to/key\": bad permissions"
        match = LoadKeyPathRegex().Match(errorMessage);
        if (match.Success)
            return match.Groups[1].Value;

        return null;
    }

    [GeneratedRegex(@"Permissions\s+\d+\s+for\s+'([^']+)'")]
    private static partial Regex PermissionKeyPathRegex();

    [GeneratedRegex(@"Load key\s+""([^""]+)""")]
    private static partial Regex LoadKeyPathRegex();
}
