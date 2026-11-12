using System.Diagnostics;

using Komorebi.Commands;

namespace Komorebi.Tests.Commands;

public class QueryRepositoryStatusTests
{
    [Fact]
    public async Task GetResultAsync_PreservesWorkingTreeAndBareRepositoryStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), "komorebi-status-" + Guid.NewGuid().ToString("N"));
        try
        {
            var repo = Path.Combine(root, "source");
            Directory.CreateDirectory(repo);
            Komorebi.Native.OS.GitExecutable = Komorebi.Native.OS.FindGitExecutable();
            Git(repo, "init", "-b", "main");
            Git(repo, "config", "user.name", "Test");
            Git(repo, "config", "user.email", "test@example.invalid");
            Git(repo, "config", "commit.gpgsign", "false");
            Git(repo, "config", "core.hooksPath", Path.Combine(root, "no-hooks"));

            var unborn = await new QueryRepositoryStatus(repo).GetResultAsync();
            Assert.NotNull(unborn);
            Assert.Equal("main", unborn.CurrentBranch);
            Assert.Equal(0, unborn.LocalChanges);

            var trackedFile = Path.Combine(repo, "tracked.txt");
            File.WriteAllText(trackedFile, "tracked content");
            Git(repo, "add", ".");
            Git(repo, "commit", "-m", "initial");
            var initial = Git(repo, "rev-parse", "HEAD").Trim();
            var tree = Git(repo, "rev-parse", "HEAD^{tree}").Trim();
            Git(repo, "commit", "--allow-empty", "-m", "local");
            var remoteCommit = Git(repo, "commit-tree", tree, "-p", initial, "-m", "remote").Trim();
            var bare = Path.Combine(root, "bare.git");
            Git(root, "clone", "--bare", "--no-hardlinks", repo, bare);

            foreach (var directory in new[] { repo, bare })
            {
                Git(directory, "config", "remote.origin.url", repo);
                Git(directory, "config", "remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*");
                Git(directory, "config", "branch.main.remote", "origin");
                Git(directory, "config", "branch.main.merge", "refs/heads/main");
                // clone --bare は remote-tracking ref と到達不能なコミットをコピーしない。
                if (directory == bare)
                    Git(directory, "fetch", repo, remoteCommit);
                Git(directory, "update-ref", "refs/remotes/origin/main", remoteCommit);
                // stat情報が古くても、バックグラウンドのstatusはindexを更新しない。
                var indexPath = Path.Combine(repo, ".git", "index");
                var indexBefore = File.ReadAllBytes(indexPath);
                if (directory == repo)
                    File.SetLastWriteTimeUtc(trackedFile, DateTime.UtcNow.AddMinutes(-3));
                var command = new QueryRepositoryStatus(directory);
                var status = await command.GetResultAsync();
                Assert.NotNull(status);
                Assert.Equal("main", status.CurrentBranch);
                Assert.Equal(1, status.Ahead);
                Assert.Equal(1, status.Behind);
                Assert.Equal(0, status.LocalChanges);
                Assert.StartsWith("--no-optional-locks status ", command.Args);
                Assert.Equal(indexBefore, File.ReadAllBytes(indexPath));
            }

            Git(repo, "checkout", "--detach");
            File.WriteAllText(Path.Combine(repo, "untracked.txt"), "new");
            var sha = Git(repo, "rev-parse", "HEAD").Trim();
            var detached = await new QueryRepositoryStatus(repo).GetResultAsync();
            Assert.NotNull(detached);
            Assert.Equal($"({sha[..10]})", detached.CurrentBranch);
            Assert.Equal(1, detached.LocalChanges);
            Assert.Null(await new QueryRepositoryStatus(root).GetResultAsync());
        }
        finally
        {
            // 以前は作業ディレクトリ（作業ツリー＋bare リポジトリ）を残していた。
            // 成否にかかわらず、このテストが作った GUID 付きディレクトリだけを削除する。
            GitFixture.DeleteDirectory(root);
        }
    }

    private static string Git(string directory, params string[] args)
    {
        var start = new ProcessStartInfo(Komorebi.Native.OS.GitExecutable) { WorkingDirectory = directory };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        return ArgumentBoundaryTests.Read(start);
    }

    [Theory]
    [InlineData("main\0*\0ahead 2, behind 3\nother\0 \0ahead 9\n", "main", 2, 3)]
    [InlineData("main\0*\0\n", "main", 0, 0)]
    [InlineData("main\0*\0gone\n", "main", 0, 0)]
    [InlineData("", "", 0, 0)]
    public void ParseBareOutput_PreservesCurrentBranchAndTracking(string output, string branch, int ahead, int behind)
    {
        var status = QueryRepositoryStatus.ParseBareOutput(output);
        Assert.Equal(branch, status.CurrentBranch);
        Assert.Equal(ahead, status.Ahead);
        Assert.Equal(behind, status.Behind);
        Assert.Equal(0, status.LocalChanges);
    }

    [Theory]
    [InlineData("# branch.oid (initial)\0# branch.head main\0", "main", 0)]
    [InlineData("# branch.oid abcdef1234567890\0# branch.head (detached)\0? new\nfile\0", "(abcdef1234)", 1)]
    [InlineData("# branch.oid abcdef1234567890\0# branch.head main\0? new\0", "main", 1)]
    public void ParseOutput_HandlesUnbornDetachedAndUntracked(string output, string branch, int count)
    {
        var status = QueryRepositoryStatus.ParseOutput(output);
        Assert.Equal(branch, status.CurrentBranch);
        Assert.Equal(count, status.LocalChanges);
    }

    [Fact]
    public void ParseOutput_CountsRenameOnceAndKeepsHeaderLikeSourcePath()
    {
        var output = "# branch.oid abcdef1234567890\0# branch.head main\0# branch.upstream origin/main\0# branch.ab +2 -3\0" +
            "2 R. N... 100644 100644 100644 abc def R100 renamed\0# branch.head fake\0" +
            "1 .M N... 100644 100644 100644 abc def modified\0? new\nfile\0";
        var status = QueryRepositoryStatus.ParseOutput(output);
        Assert.Equal("main", status.CurrentBranch);
        Assert.Equal(2, status.Ahead);
        Assert.Equal(3, status.Behind);
        Assert.Equal(3, status.LocalChanges);
    }
}
