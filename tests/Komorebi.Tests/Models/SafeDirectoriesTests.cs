using Komorebi.Models;

namespace Komorebi.Tests.Models;

public class SafeDirectoriesTests
{
    [Theory]
    [InlineData("fatal: detected dubious ownership in repository", true)]
    [InlineData("fatal: unsafe repository", true)]
    [InlineData("fatal: not a git repository", false)]
    public void IsUntrustedRepository_RecognizesOnlyOwnershipErrors(string output, bool expected)
    {
        Assert.Equal(expected, SafeDirectories.IsUntrustedRepository(output));
    }

    [Theory]
    [InlineData("work space")]
    [InlineData("it's")]
    public void TryGetSafeDirectoryValue_UsesOnlyMatchingGitSuggestion(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "komorebi-trust", name);
        var expected = path.Replace(Path.DirectorySeparatorChar, '/');
        var output = "git config --global --add safe.directory '" + expected.Replace("'", "'\\''") + "'";
        Assert.True(SafeDirectories.TryGetSafeDirectoryValue(path, output, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("/unrelated/repository")]
    [InlineData("/unrelated/*")]
    public void TryGetSafeDirectoryValue_DoesNotExpandTrustFromDiagnostic(string suggestion)
    {
        var path = Path.Combine(Path.GetTempPath(), "komorebi-trust", "requested");
        Assert.True(SafeDirectories.TryGetSafeDirectoryValue(path, $"git config --global --add safe.directory '{suggestion}'", out var value));
        Assert.Equal(path.Replace(Path.DirectorySeparatorChar, '/'), value);
    }

    [Fact]
    public void TryGetSafeDirectoryValue_RejectsCommandEmbeddedInRepositoryName()
    {
        var path = Path.Combine(Path.GetTempPath(), "--add safe.directory '*");
        var output = $"fatal: detected dubious ownership in repository at '{path}'";
        Assert.True(SafeDirectories.TryGetSafeDirectoryValue(path, output, out var value));
        Assert.Equal(path.Replace(Path.DirectorySeparatorChar, '/'), value);
        Assert.NotEqual("*", value);
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("file")]
    [InlineData("bare")]
    public void TryGetSafeDirectoryValue_UsesNearestRepositoryBoundaryRegardlessOfDiagnostic(string marker)
    {
        var outer = Path.Combine(Path.GetTempPath(), "komorebi-trust-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(outer, ".git"));
        var root = Path.Combine(outer, "repository");
        var child = Path.Combine(root, "nested", "directory");
        Directory.CreateDirectory(child);
        if (marker == "directory")
            Directory.CreateDirectory(Path.Combine(root, ".git"));
        else if (marker == "file")
            File.WriteAllText(Path.Combine(root, ".git"), "gitdir: ../metadata");
        else
        {
            File.WriteAllText(Path.Combine(root, "HEAD"), "ref: refs/heads/main");
            Directory.CreateDirectory(Path.Combine(root, "objects"));
            Directory.CreateDirectory(Path.Combine(root, "refs"));
        }

        foreach (var suggestion in new[] { root, outer, "*", outer.Replace('\\', '/') + "/*", "/unrelated/repository" })
        {
            var output = $"git config --global --add safe.directory '{suggestion.Replace('\\', '/')}'";
            Assert.True(SafeDirectories.TryGetSafeDirectoryValue(child, output, out var value));
            Assert.Equal(root.Replace(Path.DirectorySeparatorChar, '/'), value);
        }
    }

    /// <summary>テスト用に .git ディレクトリを持つ一時リポジトリ（実体はディレクトリだけ）を作る。</summary>
    private static string CreateFakeRepository(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), $"komorebi-autofix-{Guid.NewGuid():N}", name);
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        return root;
    }

    /// <summary>git が出す所有権エラーと同じ形の stderr（提案コマンド付き）を作る。</summary>
    private static string DubiousOwnershipStderr(string suggestion)
    {
        return $"fatal: detected dubious ownership in repository at '{suggestion}'\n" +
               "To add an exception for this directory, call:\n\n" +
               $"\tgit config --global --add safe.directory '{suggestion}'\n";
    }

    /// <summary>
    /// git を起動する前の事前検査: context 配下に実在するパスだけを候補にし、
    /// context 外・前方一致だけの兄弟・'*'・存在しないパス・context 自体が無い場合は git を起動させない。
    /// </summary>
    [Fact]
    public void IsAutoFixCandidate_RequiresExistingPathInsideExistingContext()
    {
        var repo = CreateFakeRepository("context");
        var parent = Path.GetDirectoryName(repo)!;
        var inside = Path.Combine(repo, "sub");
        Directory.CreateDirectory(inside);
        var sibling = repo + "-sibling";
        Directory.CreateDirectory(sibling);
        try
        {
            Assert.True(SafeDirectories.IsAutoFixCandidate(repo, repo));
            Assert.True(SafeDirectories.IsAutoFixCandidate(inside, repo));
            Assert.False(SafeDirectories.IsAutoFixCandidate(parent, repo));
            Assert.False(SafeDirectories.IsAutoFixCandidate(sibling, repo));
            Assert.False(SafeDirectories.IsAutoFixCandidate(Path.Combine(repo, "missing"), repo));
            Assert.False(SafeDirectories.IsAutoFixCandidate(repo + "/*", repo));
            Assert.False(SafeDirectories.IsAutoFixCandidate(repo, Path.Combine(parent, "no-such-context")));
            Assert.False(SafeDirectories.IsAutoFixCandidate(string.Empty, repo));
            Assert.False(SafeDirectories.IsAutoFixCandidate(repo, string.Empty));
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    /// <summary>
    /// UNC・デバイスパスの候補は、context が同じ形式でない限り文字列の段階（ファイルシステムに触れない段階）で拒否する。
    /// 文字列の段階で false なので、IsAutoFixCandidate は実在確認（Directory.Exists）へ進まず、
    /// 存在しないホストへの SMB 接続（NTLM 送出・タイムアウト）も起きない。
    /// </summary>
    [Theory]
    [InlineData(@"\\komorebi-unreachable.invalid\share\repo")]
    [InlineData("//komorebi-unreachable.invalid/share/repo")]
    [InlineData(@"\/komorebi-unreachable.invalid\share\repo")]
    [InlineData(@"\\?\C:\komorebi\repo")]
    [InlineData(@"\\.\C:\komorebi\repo")]
    [InlineData("%(prefix)///komorebi-unreachable.invalid/share/repo")]
    public void IsAutoFixCandidate_RejectsUncAndDevicePathsBeforeTouchingFileSystem(string candidate)
    {
        var repo = CreateFakeRepository("context");
        try
        {
            Assert.False(SafeDirectories.IsAutoFixCandidateByString(candidate, repo));

            // 公開判定も false。文字列段階で落ちるので待ち時間なく返る（ネットワークに出ないことの補助確認）
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Assert.False(SafeDirectories.IsAutoFixCandidate(candidate, repo));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"判定に {watch.Elapsed} かかった（外部へ接続した疑い）");

            Assert.False(SafeDirectories.TryGetAutoFixValue(candidate, repo, DubiousOwnershipStderr(candidate.Replace('\\', '/')), out _));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(repo)!, true);
        }
    }

    /// <summary>
    /// context 配下の文字列でも、リポジトリ内のリンク・ジャンクションを経由して context の外を指す候補は拒否する。
    /// </summary>
    [Fact]
    public void IsAutoFixCandidate_RejectsCandidateReachedThroughJunction()
    {
        var repo = CreateFakeRepository("context");
        var parent = Path.GetDirectoryName(repo)!;
        var outside = Path.Combine(parent, "outside-repo");
        Directory.CreateDirectory(Path.Combine(outside, ".git"));
        try
        {
            var linked = Path.Combine(repo, "linked");
            DirectoryLinks.CreateOrSkip(linked, outside);

            Assert.True(SafeDirectories.IsAutoFixCandidateByString(linked, repo));
            Assert.False(SafeDirectories.IsAutoFixCandidate(linked, repo));
            var suggestion = linked.Replace(Path.DirectorySeparatorChar, '/');
            Assert.False(SafeDirectories.TryGetAutoFixValue(linked, repo, DubiousOwnershipStderr(suggestion), out _));
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    /// <summary>ローカル stderr の提案パスが context 外なら、候補・算出値が context 配下でも拒否する。</summary>
    [Fact]
    public void TryGetAutoFixValue_RejectsSuggestionOutsideContext()
    {
        var repo = CreateFakeRepository("context");
        var parent = Path.GetDirectoryName(repo)!;
        try
        {
            var outsideSuggestion = parent.Replace(Path.DirectorySeparatorChar, '/');
            Assert.False(SafeDirectories.TryGetAutoFixValue(repo, repo, DubiousOwnershipStderr(outsideSuggestion), out _));
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void TryGetAutoFixValue_AcceptsUntrustedRepositoryInsideContext()
    {
        var repo = CreateFakeRepository("context");
        var submodule = Path.Combine(repo, "modules", "child");
        Directory.CreateDirectory(Path.Combine(submodule, ".git"));
        try
        {
            var expected = repo.Replace(Path.DirectorySeparatorChar, '/');
            Assert.True(SafeDirectories.TryGetAutoFixValue(repo, repo, DubiousOwnershipStderr(expected), out var value));
            Assert.Equal(expected, value);

            // context 配下の別リポジトリ（サブモジュール）は、そのリポジトリ自身の境界を信頼する
            var child = submodule.Replace(Path.DirectorySeparatorChar, '/');
            Assert.True(SafeDirectories.TryGetAutoFixValue(submodule, repo, DubiousOwnershipStderr(child), out value));
            Assert.Equal(child, value);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(repo)!, true);
        }
    }

    [Theory]
    [InlineData("*")]
    [InlineData("{repo}/*")]
    public void TryGetAutoFixValue_RejectsWildcardCandidates(string candidate)
    {
        var repo = CreateFakeRepository("wildcard");
        try
        {
            var path = candidate.Replace("{repo}", repo);
            Assert.False(SafeDirectories.TryGetAutoFixValue(path, repo, DubiousOwnershipStderr("*"), out var value));
            Assert.Empty(value);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(repo)!, true);
        }
    }

    [Fact]
    public void TryGetAutoFixValue_RejectsMissingPathAndNonOwnershipError()
    {
        var repo = CreateFakeRepository("missing");
        try
        {
            var missing = Path.Combine(repo, "does-not-exist");
            Assert.False(SafeDirectories.TryGetAutoFixValue(missing, repo, DubiousOwnershipStderr(missing.Replace('\\', '/')), out _));

            // ローカルで再実行した結果が所有権エラーでなければ（既に信頼済み・別のエラー）追加しない
            Assert.False(SafeDirectories.TryGetAutoFixValue(repo, repo, "fatal: not a git repository", out _));
            Assert.False(SafeDirectories.TryGetAutoFixValue(repo, repo, string.Empty, out _));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(repo)!, true);
        }
    }

    [Fact]
    public void TryGetAutoFixValue_RejectsExistingRepositoryOutsideContext()
    {
        var context = CreateFakeRepository("context");
        var outside = CreateFakeRepository("outside");
        // 名前の前方一致（context-sibling）を配下と誤認しないことも確認する
        var sibling = context + "-sibling";
        Directory.CreateDirectory(Path.Combine(sibling, ".git"));
        try
        {
            var outsideValue = outside.Replace(Path.DirectorySeparatorChar, '/');
            Assert.False(SafeDirectories.TryGetAutoFixValue(outside, context, DubiousOwnershipStderr(outsideValue), out _));
            Assert.False(SafeDirectories.TryGetAutoFixValue(sibling, context, DubiousOwnershipStderr(sibling.Replace('\\', '/')), out _));

            // context 自体が空・存在しない場合も追加しない
            Assert.False(SafeDirectories.TryGetAutoFixValue(outside, string.Empty, DubiousOwnershipStderr(outsideValue), out _));
            Assert.False(SafeDirectories.TryGetAutoFixValue(outside, Path.Combine(outside, "missing"), DubiousOwnershipStderr(outsideValue), out _));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(context)!, true);
            Directory.Delete(Path.GetDirectoryName(outside)!, true);
        }
    }

    /// <summary>
    /// Git for Windows は UNC パスの safe.directory 値に %(prefix)/ を付ける。ローカルの stderr の提案値が
    /// 実パスと一致する場合はその値（%(prefix)/ 付き）をそのまま使い、提案が無い場合も %(prefix)/ を補う。
    /// 管理共有（\\localhost\C$）へアクセスできない環境では実行できないためスキップする。
    /// </summary>
    [Fact]
    public void TryGetAutoFixValue_KeepsPrefixForUncPaths()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "UNC の %(prefix)/ は Git for Windows 固有。");

        var repo = CreateFakeRepository("unc");
        try
        {
            var full = Path.GetFullPath(repo);
            var unc = $@"\\localhost\{full[0]}$\{full[3..]}";
            Assert.SkipUnless(Directory.Exists(unc), "管理共有 \\\\localhost\\C$ にアクセスできない環境。");

            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(unc)).Replace('\\', '/');
            var suggested = "%(prefix)/" + normalized;
            Assert.True(SafeDirectories.TryGetAutoFixValue(unc, unc, DubiousOwnershipStderr(suggested), out var value));
            Assert.Equal(suggested, value);

            Assert.True(SafeDirectories.TryGetAutoFixValue(unc, unc, "fatal: detected dubious ownership in repository", out value));
            Assert.Equal(suggested, value);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(repo)!, true);
        }
    }

    [Fact]
    public void TryGetSafeDirectoryValue_NormalizesFallbackAndRejectsEmptyPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "komorebi-trust");
        Assert.True(SafeDirectories.TryGetSafeDirectoryValue(path + Path.DirectorySeparatorChar, "", out var value));
        Assert.Equal(path.Replace(Path.DirectorySeparatorChar, '/'), value);
        Assert.False(SafeDirectories.TryGetSafeDirectoryValue("", "", out _));
        Assert.False(SafeDirectories.TryGetSafeDirectoryValue(path + "/*", "", out _));
        var root = Path.GetPathRoot(path)!;
        Assert.True(SafeDirectories.TryGetSafeDirectoryValue(root, "", out value));
        Assert.Equal(root.Replace(Path.DirectorySeparatorChar, '/'), value);
    }
}
