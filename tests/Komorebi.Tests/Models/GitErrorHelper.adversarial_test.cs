using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// GitErrorHelper.GetHintKey / GetAskpassHintKey に対する嫌がらせテスト。
    /// パターンマッチの優先順位、境界値、競合パターン、極端な入力を攻撃する。
    /// </summary>
    public class GitErrorHelperAdversarialTests
    {
        // ===============================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ===============================================================

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>nullで例外にならないこと</summary>
        [Fact]
        public void GetHintKey_Null_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, GitErrorHelper.GetHintKey(null));
        }

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>空文字列で例外にならないこと</summary>
        [Fact]
        public void GetHintKey_EmptyString_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, GitErrorHelper.GetHintKey(""));
        }

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>空白のみで例外にならないこと</summary>
        [Fact]
        public void GetHintKey_WhitespaceOnly_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, GitErrorHelper.GetHintKey("   \t\n  "));
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>巨大文字列（100KB）でクラッシュしないこと</summary>
        [Fact]
        public void GetHintKey_HugeString_DoesNotCrash()
        {
            var huge = new string('x', 100_000);
            var ex = Record.Exception(() => GitErrorHelper.GetHintKey(huge));
            Assert.Null(ex);
        }

        /// <adversarial category="boundary" severity="medium" />
        /// <summary>制御文字・ヌルバイトを含むメッセージでクラッシュしないこと</summary>
        [Fact]
        public void GetHintKey_ControlCharacters_DoesNotCrash()
        {
            var msg = "fatal: \x00\x01\x02 index.lock \r\n\t";
            var result = GitErrorHelper.GetHintKey(msg);
            Assert.Equal("Text.GitError.IndexLock", result);
        }

        /// <adversarial category="boundary" severity="medium" />
        /// <summary>Unicodeゼロ幅文字を含むメッセージでパターンマッチが動作すること</summary>
        [Fact]
        public void GetHintKey_ZeroWidthCharacters_StillMatches()
        {
            // ゼロ幅文字がパターン内に挿入された場合はマッチしない（正しい動作）
            var msg = "index\u200B.lock";
            var result = GitErrorHelper.GetHintKey(msg);
            Assert.Equal(string.Empty, result);
        }

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>GetAskpassHintKey: nullで例外にならないこと</summary>
        [Fact]
        public void GetAskpassHintKey_Null_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, GitErrorHelper.GetAskpassHintKey(null));
        }

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>GetAskpassHintKey: 空文字列で例外にならないこと</summary>
        [Fact]
        public void GetAskpassHintKey_EmptyString_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, GitErrorHelper.GetAskpassHintKey(""));
        }

        // ===============================================================
        // 🔀 パターン優先順位・競合（Pattern Priority）
        // ===============================================================

        /// <adversarial category="state" severity="critical" />
        /// <summary>HostKeyChangedとHostKeyVerifyFailedが同じメッセージに存在する場合、HostKeyChangedが優先されること</summary>
        [Fact]
        public void GetHintKey_HostKeyChangedTakesPriorityOverVerifyFailed()
        {
            // 実際のSSHエラーメッセージ: 両方を含む
            var msg = "REMOTE HOST IDENTIFICATION HAS CHANGED!\nHost key verification failed.";
            Assert.Equal("Text.GitError.HostKeyChanged", GitErrorHelper.GetHintKey(msg));
        }

        /// <adversarial category="state" severity="critical" />
        /// <summary>HostKeyVerifyFailedは単独で出た場合にのみマッチすること</summary>
        [Fact]
        public void GetHintKey_HostKeyVerifyFailed_AloneMatches()
        {
            var msg = "Host key verification failed.\nfatal: Could not read from remote repository.";
            Assert.Equal("Text.GitError.HostKeyVerifyFailed", GitErrorHelper.GetHintKey(msg));
        }

        /// <adversarial category="state" severity="high" />
        /// <summary>「password」を含むがSSHパスワードプロンプトではないメッセージがPasswordにマッチしないこと</summary>
        [Fact]
        public void GetAskpassHintKey_PasswordInNonPasswordContext_DoesNotMatchPassword()
        {
            // 「password」を含むが "Password for" でも "Enter password" でもないメッセージ
            var msg = "Your password has expired. Please change your password.";
            var result = GitErrorHelper.GetAskpassHintKey(msg);
            Assert.Equal(string.Empty, result);
        }

        /// <adversarial category="state" severity="high" />
        /// <summary>「passphrase」を含むがパスフレーズプロンプトではないメッセージがPassphraseにマッチしないこと</summary>
        [Fact]
        public void GetAskpassHintKey_PassphraseInNonPromptContext_DoesNotMatchPassphrase()
        {
            // "Enter passphrase" ではなく単に "passphrase" を含むだけのメッセージ
            var msg = "Bad passphrase was provided. Please try again.";
            var result = GitErrorHelper.GetAskpassHintKey(msg);
            Assert.Equal(string.Empty, result);
        }

        /// <adversarial category="state" severity="critical" />
        /// <summary>HostKeyChangedメッセージにpasswordが含まれてもHostKeyChangedが返ること</summary>
        [Fact]
        public void GetAskpassHintKey_HostKeyChangedWithPassword_ReturnsHostKeyChanged()
        {
            var msg = "Host key verification failed.\nPlease enter your password for retry.";
            Assert.Equal("Text.Askpass.Hint.HostKeyChanged", GitErrorHelper.GetAskpassHintKey(msg));
        }

        /// <adversarial category="state" severity="high" />
        /// <summary>初回ホスト鍵確認の正確なプロンプトがHostKeyNewにマッチすること</summary>
        [Fact]
        public void GetAskpassHintKey_RealHostKeyNewPrompt_ReturnsHostKeyNew()
        {
            var msg = "The authenticity of host 'github.com (20.27.177.113)' can't be established.\n" +
                      "ED25519 key fingerprint is SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU\n" +
                      "This key is not known by any other names.\n" +
                      "Are you sure you want to continue connecting (yes/no/[fingerprint])?";
            Assert.Equal("Text.Askpass.Hint.HostKeyNew", GitErrorHelper.GetAskpassHintKey(msg));
        }

        /// <adversarial category="state" severity="high" />
        /// <summary>パスフレーズプロンプトの正確なメッセージがPassphraseにマッチすること</summary>
        [Fact]
        public void GetAskpassHintKey_RealPassphrasePrompt_ReturnsPassphrase()
        {
            var msg = "Enter passphrase for key '/Users/user/.ssh/id_ed25519': ";
            Assert.Equal("Text.Askpass.Hint.Passphrase", GitErrorHelper.GetAskpassHintKey(msg));
        }

        /// <adversarial category="state" severity="high" />
        /// <summary>パスワードプロンプトの正確なメッセージがPasswordにマッチすること</summary>
        [Fact]
        public void GetAskpassHintKey_RealPasswordPrompt_ReturnsPassword()
        {
            var msg = "Password for 'https://user@github.com': ";
            Assert.Equal("Text.Askpass.Hint.Password", GitErrorHelper.GetAskpassHintKey(msg));
        }

        // ===============================================================
        // 🎭 大文字小文字・エンコーディング（Case & Encoding）
        // ===============================================================

        /// <adversarial category="type" severity="high" />
        /// <summary>大文字小文字が混在したメッセージでもマッチすること</summary>
        [Theory]
        [InlineData("INDEX.LOCK")]
        [InlineData("Index.Lock")]
        [InlineData("iNdEx.lOcK")]
        public void GetHintKey_CaseInsensitive_IndexLock(string pattern)
        {
            Assert.Equal("Text.GitError.IndexLock", GitErrorHelper.GetHintKey(pattern));
        }

        /// <adversarial category="type" severity="high" />
        /// <summary>CONFLICT は CaseSensitive (Ordinal) でマッチすること</summary>
        [Fact]
        public void GetHintKey_Conflict_IsCaseSensitive()
        {
            // 大文字の "CONFLICT" のみマッチ（Ordinal比較）
            Assert.Equal("Text.GitError.MergeConflict", GitErrorHelper.GetHintKey("CONFLICT in file.txt"));
            // 小文字の "conflict" は Ordinal比較ではマッチしないが、
            // "Merge conflict" は OrdinalIgnoreCase なのでマッチする
            Assert.Equal("Text.GitError.MergeConflict", GitErrorHelper.GetHintKey("Merge conflict in file.txt"));
        }

        // ===============================================================
        // 🌪️ 実際のgitエラーメッセージ（Real-World Messages）
        // ===============================================================

        /// <adversarial category="chaos" severity="high" />
        /// <summary>実際のgit pushエラーメッセージからPushRejectedが検出されること</summary>
        [Fact]
        public void GetHintKey_RealPushRejected_Matches()
        {
            var msg = "To https://github.com/user/repo.git\n" +
                      " ! [rejected]        main -> main (non-fast-forward)\n" +
                      "error: failed to push some refs to 'https://github.com/user/repo.git'\n" +
                      "hint: Updates were rejected because the tip of your current branch is behind";
            Assert.Equal("Text.GitError.PushRejected", GitErrorHelper.GetHintKey(msg));
        }

        /// <adversarial category="chaos" severity="high" />
        /// <summary>実際のホスト鍵変更エラーメッセージからHostKeyChangedが検出されること</summary>
        [Fact]
        public void GetHintKey_RealHostKeyChanged_Matches()
        {
            var msg = "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n" +
                      "@    WARNING: REMOTE HOST IDENTIFICATION HAS CHANGED!     @\n" +
                      "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n" +
                      "IT IS POSSIBLE THAT SOMEONE IS DOING SOMETHING NASTY!\n" +
                      "Host key for github.com has changed and you have requested strict checking.\n" +
                      "Host key verification failed.\n" +
                      "fatal: Could not read from remote repository.";
            Assert.Equal("Text.GitError.HostKeyChanged", GitErrorHelper.GetHintKey(msg));
        }

        /// <adversarial category="chaos" severity="high" />
        /// <summary>実際のmacOS SSH検証失敗エラーからHostKeyVerifyFailedが検出されること</summary>
        [Fact]
        public void GetHintKey_RealMacOSHostKeyVerifyFailed_Matches()
        {
            var msg = "Cloning into 'Komorebi'...\n" +
                      "2026-03-30 00:53:20.348 Komorebi[2727:157767] error messaging the mach port for IMKCFRunLoopWakeUpReliable\n" +
                      "Host key verification failed.\n" +
                      "fatal: Could not read from remote repository.\n\n" +
                      "Please make sure you have the correct access rights\nand the repository exists.";
            Assert.Equal("Text.GitError.HostKeyVerifyFailed", GitErrorHelper.GetHintKey(msg));
        }

        /// <adversarial category="chaos" severity="high" />
        /// <summary>認証失敗メッセージにpasswordが含まれてもAuthFailedが返ること（順序依存でないこと）</summary>
        [Fact]
        public void GetHintKey_AuthFailedWithPassword_ReturnsAuthFailed()
        {
            var msg = "fatal: could not read Password for 'https://github.com': terminal prompts disabled";
            Assert.Equal("Text.GitError.AuthFailed", GitErrorHelper.GetHintKey(msg));
        }

        // ===============================================================
        // 💀 パターン網羅性（Coverage Assault）
        // ===============================================================

        /// <adversarial category="resource" severity="medium" />
        /// <summary>全パターンが少なくとも1つのマッチを持つこと</summary>
        [Theory]
        [InlineData("index.lock", "Text.GitError.IndexLock")]
        [InlineData("CONFLICT in file.txt", "Text.GitError.MergeConflict")]
        [InlineData("HEAD detached at abc123", "Text.GitError.DetachedHead")]
        [InlineData("Updates were rejected because", "Text.GitError.PushRejected")]
        [InlineData("Please commit your changes or stash them", "Text.GitError.UncommittedChanges")]
        [InlineData("Authentication failed for 'https://github.com'", "Text.GitError.AuthFailed")]
        [InlineData("remote: repository not found", "Text.GitError.RemoteNotFound")]
        [InlineData("branch 'main' already exists", "Text.GitError.AlreadyExists")]
        [InlineData("untracked working tree files would be overwritten", "Text.GitError.UntrackedOverwritten")]
        [InlineData("Connection timed out during fetch", "Text.GitError.NetworkError")]
        [InlineData("interactive rebase already started", "Text.GitError.RebaseInProgress")]
        [InlineData("nothing to commit, working tree clean", "Text.GitError.NothingToCommit")]
        [InlineData("submodule 'lib' not initialized", "Text.GitError.SubmoduleError")]
        [InlineData("git-lfs filter-process: not found", "Text.GitError.LfsError")]
        [InlineData("this exceeds GitHub's file size limit", "Text.GitError.FileTooLarge")]
        [InlineData("error: cherry-pick failed", "Text.GitError.CherryPickConflict")]
        [InlineData("You have not concluded your merge (MERGE_HEAD exists)", "Text.GitError.MergeNotFinished")]
        [InlineData("There is no tracking information for the current branch", "Text.GitError.NoUpstream")]
        [InlineData("shallow update not allowed", "Text.GitError.ShallowClone")]
        [InlineData("No space left on device", "Text.GitError.DiskFull")]
        [InlineData("Permission denied (filesystem)", "Text.GitError.PermissionDenied")]
        [InlineData("not a valid ref name", "Text.GitError.InvalidRef")]
        [InlineData("couldn't find remote ref feature/test", "Text.GitError.RemoteRefNotFound")]
        [InlineData("bad object abc123", "Text.GitError.CorruptObject")]
        [InlineData("fatal: not a git repository", "Text.GitError.NotARepo")]
        [InlineData("No stash entries found.", "Text.GitError.StashError")]
        [InlineData("clean filter 'lfs' failed", "Text.GitError.FilterError")]
        [InlineData("pre-commit hook rejected the push", "Text.GitError.HookFailed")]
        [InlineData("error: patch does not apply", "Text.GitError.PatchFailed")]
        [InlineData("REMOTE HOST IDENTIFICATION HAS CHANGED", "Text.GitError.HostKeyChanged")]
        [InlineData("Host key verification failed.", "Text.GitError.HostKeyVerifyFailed")]
        [InlineData("worktree 'main' is already checked out", "Text.GitError.WorktreeError")]
        public void GetHintKey_AllPatterns_MatchCorrectly(string message, string expectedKey)
        {
            Assert.Equal(expectedKey, GitErrorHelper.GetHintKey(message));
        }

        /// <adversarial category="resource" severity="medium" />
        /// <summary>マッチしないメッセージが空文字を返すこと</summary>
        [Theory]
        [InlineData("Everything up-to-date")]
        [InlineData("Already on 'main'")]
        [InlineData("Switched to branch 'feature'")]
        [InlineData("random text with no git error patterns")]
        public void GetHintKey_NonMatchingMessages_ReturnsEmpty(string message)
        {
            Assert.Equal(string.Empty, GitErrorHelper.GetHintKey(message));
        }

        // ===============================================================
        // 🔀 GetAskpassHintKey パターン競合（Askpass Pattern Conflicts）
        // ===============================================================

        /// <adversarial category="state" severity="critical" />
        /// <summary>「authenticity」だけではマッチしないこと（ピンポイントマッチの検証）</summary>
        [Fact]
        public void GetAskpassHintKey_AuthenticityAlone_DoesNotMatch()
        {
            var msg = "The authenticity of the data cannot be verified.";
            Assert.Equal(string.Empty, GitErrorHelper.GetAskpassHintKey(msg));
        }

        /// <adversarial category="state" severity="critical" />
        /// <summary>「continue connecting」だけではマッチしないこと</summary>
        [Fact]
        public void GetAskpassHintKey_ContinueConnectingAlone_DoesNotMatch()
        {
            var msg = "Do you want to continue connecting to the VPN?";
            Assert.Equal(string.Empty, GitErrorHelper.GetAskpassHintKey(msg));
        }

        /// <adversarial category="state" severity="high" />
        /// <summary>「Enter password」プロンプトがPasswordにマッチすること</summary>
        [Fact]
        public void GetAskpassHintKey_EnterPassword_ReturnsPassword()
        {
            Assert.Equal("Text.Askpass.Hint.Password", GitErrorHelper.GetAskpassHintKey("Enter password: "));
        }
    }
}
