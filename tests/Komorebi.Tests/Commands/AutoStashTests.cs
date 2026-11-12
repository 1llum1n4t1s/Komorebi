namespace Komorebi.Tests.Commands;

/// <summary>
/// Pull 等の自動 stash（PushAutoAsync / RestoreAutoAsync）の保存・復元契約を実 git で検証する。
/// 一時リポジトリの作成・設定・後始末は共通ヘルパー <see cref="GitFixture"/> に任せる。
/// </summary>
public class AutoStashTests : IDisposable
{
    private readonly GitFixture _repo = new();

    public AutoStashTests()
    {
        // 全テスト共通の基点コミット（file.txt = "base\n"）
        _repo.Commit("file.txt", "base\n", "base");
    }

    public void Dispose() => _repo.Dispose();

    private string FilePath => _repo.PathOf("file.txt");

    private Komorebi.Commands.Stash NewStash() => new(_repo.Root) { RaiseError = false };

    [Fact]
    public async Task FailedPull_RestoresBothIndexAndWorkingTree()
    {
        File.WriteAllText(FilePath, "staged\n");
        _repo.Git("add", "file.txt");
        File.WriteAllText(FilePath, "unstaged\n");
        var stash = NewStash();
        Assert.True(await stash.PushAutoAsync("PULL_AUTO_STASH"));
        Assert.Empty(_repo.Git("status", "--porcelain"));
        // 存在しないローカルremoteで失敗させる。ネットワークには接続しない。
        Assert.False(await new Komorebi.Commands.Pull(_repo.Root, _repo.PathOf("missing"), "main", false)
        { RaiseError = false }.RunAsync());
        await stash.RestoreAutoAsync(_repo.GitDir);
        Assert.Equal("unstaged\n", File.ReadAllText(FilePath));
        Assert.Equal("staged", _repo.Git("show", ":file.txt"));
        Assert.Empty(_repo.Git("stash", "list"));
    }

    [Theory]
    [InlineData("MERGE_HEAD", false)]
    [InlineData("CHERRY_PICK_HEAD", false)]
    [InlineData("REVERT_HEAD", false)]
    [InlineData("rebase-merge", true)]
    [InlineData("rebase-apply", true)]
    [InlineData("sequencer", true)]
    public async Task InterruptedOperation_KeepsStashUntilOperationEnds(string marker, bool directory)
    {
        File.WriteAllText(FilePath, "local\n");
        var stash = NewStash();
        Assert.True(await stash.PushAutoAsync("PULL_AUTO_STASH"));
        var sha = _repo.Git("rev-parse", "refs/stash");
        var path = Path.Combine(_repo.GitDir, marker);
        if (directory)
            Directory.CreateDirectory(path);
        else
            File.WriteAllText(path, _repo.Git("rev-parse", "HEAD"));
        await stash.RestoreAutoAsync(_repo.GitDir);
        Assert.Equal(sha, _repo.Git("rev-parse", "refs/stash"));
        Assert.Equal("base\n", File.ReadAllText(FilePath));
        if (directory)
            Directory.Delete(path);
        else
            File.Delete(path);
        await stash.RestoreAutoAsync(_repo.GitDir);
        Assert.Equal("local\n", File.ReadAllText(FilePath));
        Assert.Empty(_repo.Git("stash", "list"));
    }

    [Fact]
    public async Task MergeConflict_KeepsAutomaticStashAndConflictState()
    {
        _repo.Git("checkout", "-b", "other");
        File.WriteAllText(FilePath, "other\n");
        _repo.Git("commit", "-am", "other");
        _repo.Git("checkout", "main");
        File.WriteAllText(FilePath, "main\n");
        _repo.Git("commit", "-am", "main");
        File.WriteAllText(FilePath, "local\n");
        var stash = NewStash();
        Assert.True(await stash.PushAutoAsync("PULL_AUTO_STASH"));
        var sha = _repo.Git("rev-parse", "refs/stash");
        // 衝突で失敗することを確認しつつ merge 途中の状態を作る
        _repo.Run(false, "merge", "other");
        var conflict = File.ReadAllText(FilePath);
        Assert.Contains("<<<<<<<", conflict);
        await stash.RestoreAutoAsync(_repo.GitDir);
        Assert.Equal(sha, _repo.Git("rev-parse", "refs/stash"));
        Assert.Equal(conflict, File.ReadAllText(FilePath));
        Assert.True(File.Exists(Path.Combine(_repo.GitDir, "MERGE_HEAD")));
    }

    [Fact]
    public async Task RestoreConflict_KeepsSavedChangesInStash()
    {
        File.WriteAllText(FilePath, "local\n");
        var stash = NewStash();
        Assert.True(await stash.PushAutoAsync("PULL_AUTO_STASH"));
        var sha = _repo.Git("rev-parse", "refs/stash");
        File.WriteAllText(FilePath, "updated head\n");
        _repo.Git("commit", "-am", "updated");
        await stash.RestoreAutoAsync(_repo.GitDir);
        Assert.Equal(sha, _repo.Git("rev-parse", "refs/stash"));
        Assert.Equal("local", _repo.Git("show", "refs/stash:file.txt"));
        Assert.NotEmpty(_repo.Git("ls-files", "--unmerged"));
    }

    [Fact]
    public async Task AnotherStash_DoesNotRestoreOrDropSomeoneElsesChanges()
    {
        File.WriteAllText(FilePath, "ours\n");
        var stash = NewStash();
        Assert.True(await stash.PushAutoAsync("PULL_AUTO_STASH"));
        File.WriteAllText(FilePath, "theirs\n");
        _repo.Git("stash", "push", "-m", "another operation");
        var before = _repo.Git("stash", "list");
        await stash.RestoreAutoAsync(_repo.GitDir);
        Assert.Equal(before, _repo.Git("stash", "list"));
        Assert.Equal("base\n", File.ReadAllText(FilePath));
    }

    [Fact]
    public async Task NoLocalChanges_DoesNotPopAnExistingStash()
    {
        File.WriteAllText(FilePath, "older\n");
        _repo.Git("stash", "push", "-m", "existing");
        var before = _repo.Git("stash", "list");
        var stash = NewStash();
        Assert.True(await stash.PushAutoAsync("PULL_AUTO_STASH"));
        await stash.RestoreAutoAsync(_repo.GitDir);
        Assert.Equal(before, _repo.Git("stash", "list"));
        Assert.Equal("base\n", File.ReadAllText(FilePath));
    }
}
