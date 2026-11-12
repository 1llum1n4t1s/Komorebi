using Komorebi.Tests.Commands;
using Komorebi.ViewModels;

namespace Komorebi.Tests.ViewModels;

/// <summary>
/// 「完了後に force push」の送信先を git config（branch.&lt;name&gt;.remote / merge）から確定することを検証する。
/// remote 名に '/' を含む場合に、upstream 参照名を最初の '/' で分割すると別の remote へ force push してしまう回帰を防ぐ。
/// </summary>
public class SquashOrFixupHeadPushTargetTests
{
    [Fact]
    public async Task ResolveForcePushTarget_UsesConfiguredRemoteContainingSlash()
    {
        using var repo = new GitFixture();
        repo.Commit("a.txt", "a", "first");
        repo.Git("remote", "add", "foo", "https://example.invalid/foo.git");
        // 新しい git は `remote add` で既存 remote の部分名になる名前を拒否するが、設定ファイルを直接書いた
        // （古い git で作った・手で編集した）リポジトリでは両方が共存しうるので、config で直接作る。
        repo.Git("config", "remote.foo/bar.url", "https://example.invalid/foo-bar.git");
        repo.Git("config", "remote.foo/bar.fetch", "+refs/heads/*:refs/remotes/foo/bar/*");
        repo.Git("config", "branch.main.remote", "foo/bar");
        repo.Git("config", "branch.main.merge", "refs/heads/baz");

        // 旧実装は "foo" / "bar/baz" へ push していた
        var target = await SquashOrFixupHead.ResolveForcePushTargetAsync(repo.Root, "main", "refs/remotes/foo/bar/baz");
        Assert.Equal(("foo/bar", "baz"), target);

        // remote "foo" のブランチ "bar/baz" を追跡している場合はそちらになる（同じ upstream 参照名でも config で区別）
        repo.Git("config", "branch.main.remote", "foo");
        repo.Git("config", "branch.main.merge", "refs/heads/bar/baz");
        target = await SquashOrFixupHead.ResolveForcePushTargetAsync(repo.Root, "main", "refs/remotes/foo/bar/baz");
        Assert.Equal(("foo", "bar/baz"), target);
    }

    [Fact]
    public async Task ResolveForcePushTarget_ReturnsNullWhenConfigDoesNotMatchUpstream()
    {
        using var repo = new GitFixture();
        repo.Commit("a.txt", "a", "first");
        repo.Git("remote", "add", "origin", "https://example.invalid/origin.git");

        // 追跡設定が無い
        Assert.Null(await SquashOrFixupHead.ResolveForcePushTargetAsync(repo.Root, "main", "refs/remotes/origin/main"));

        // 設定はあるが upstream と一致しない
        repo.Git("config", "branch.main.remote", "origin");
        repo.Git("config", "branch.main.merge", "refs/heads/other");
        Assert.Null(await SquashOrFixupHead.ResolveForcePushTargetAsync(repo.Root, "main", "refs/remotes/origin/main"));

        // ローカル追跡（remote = "."）は push 先にしない
        repo.Git("config", "branch.main.remote", ".");
        repo.Git("config", "branch.main.merge", "refs/heads/main");
        Assert.Null(await SquashOrFixupHead.ResolveForcePushTargetAsync(repo.Root, "main", "refs/remotes/./main"));
    }
}
