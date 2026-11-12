using Komorebi.Models;

namespace Komorebi.Tests.Models;

/// <summary>
/// SSH 鍵パーミッション自動修正の対象判定（通常ファイル・~/.ssh 配下または設定済みの鍵のみ）を検証する。
/// 実際の chmod（File.SetUnixFileMode）は Unix 専用なので、判定ロジックだけを OS 非依存で確認する。
/// </summary>
public sealed class SSHKeyPermissionFixerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"komorebi-sshkey-{Guid.NewGuid():N}");
    private readonly string _sshDir;

    public SSHKeyPermissionFixerTests()
    {
        _sshDir = Path.Combine(_root, ".ssh");
        Directory.CreateDirectory(_sshDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch
        {
            // 後始末の失敗はテスト結果に影響させない
        }
    }

    [Fact]
    public void IsFixableKeyFile_AcceptsRegularFileUnderSshDirectory()
    {
        var key = Path.Combine(_sshDir, "id_ed25519");
        File.WriteAllText(key, "key");
        Assert.True(SSHKeyPermissionFixer.IsFixableKeyFile(key, _sshDir, []));
    }

    [Fact]
    public void IsFixableKeyFile_AcceptsConfiguredKeyOutsideSshDirectory()
    {
        var key = Path.Combine(_root, "keys", "deploy");
        Directory.CreateDirectory(Path.GetDirectoryName(key)!);
        File.WriteAllText(key, "key");
        Assert.False(SSHKeyPermissionFixer.IsFixableKeyFile(key, _sshDir, []));
        Assert.True(SSHKeyPermissionFixer.IsFixableKeyFile(key, _sshDir, [string.Empty, key]));
    }

    [Fact]
    public void IsFixableKeyFile_RejectsDirectoryMissingAndUnrelatedFiles()
    {
        Assert.False(SSHKeyPermissionFixer.IsFixableKeyFile(_sshDir, _sshDir, [_sshDir]));
        Assert.False(SSHKeyPermissionFixer.IsFixableKeyFile(Path.Combine(_sshDir, "missing"), _sshDir, []));
        Assert.False(SSHKeyPermissionFixer.IsFixableKeyFile(string.Empty, _sshDir, []));

        // ~/.ssh と名前が前方一致するだけの別ディレクトリは配下とみなさない
        var lookalike = _sshDir + "-other";
        Directory.CreateDirectory(lookalike);
        var file = Path.Combine(lookalike, "id_rsa");
        File.WriteAllText(file, "key");
        Assert.False(SSHKeyPermissionFixer.IsFixableKeyFile(file, _sshDir, []));
    }

    [Fact]
    public void IsFixableKeyFile_RejectsSymbolicLink()
    {
        var target = Path.Combine(_root, "outside-target");
        File.WriteAllText(target, "not a key");
        var link = Path.Combine(_sshDir, "id_link");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            // Windows で開発者モード・権限が無い場合はシンボリックリンクを作れない
            Assert.Skip($"シンボリックリンクを作成できない環境: {e.Message}");
        }

        Assert.False(SSHKeyPermissionFixer.IsFixableKeyFile(link, _sshDir, [link]));
    }

    [Fact]
    public void IsFixableKeyFile_AcceptsRegularFileInNestedDirectoryUnderSshDirectory()
    {
        var nested = Path.Combine(_sshDir, "work", "keys");
        Directory.CreateDirectory(nested);
        var key = Path.Combine(nested, "id_ed25519");
        File.WriteAllText(key, "key");
        Assert.True(SSHKeyPermissionFixer.IsFixableKeyFile(key, _sshDir, []));
    }

    /// <summary>
    /// ~/.ssh 内のディレクトリのシンボリックリンク（Windows ではジャンクションと同じ reparse point）を経由して
    /// ~/.ssh 外のファイルを指すパスは、最終要素が通常ファイルでも修正対象にしない。
    /// </summary>
    [Fact]
    public void IsFixableKeyFile_RejectsFileReachedThroughLinkedDirectory()
    {
        var outside = Path.Combine(_root, "outside-dir");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "victim"), "not a key");
        var linkedDir = Path.Combine(_sshDir, "linked");
        CreateDirectoryLinkOrSkip(linkedDir, outside);

        var throughLink = Path.Combine(linkedDir, "victim");
        Assert.True(File.Exists(throughLink));
        Assert.False(SSHKeyPermissionFixer.IsFixableKeyFile(throughLink, _sshDir, []));

        // 途中にリンクが無くても、さらに深い階層のリンクも同様に拒否する
        var deeper = Path.Combine(_sshDir, "a");
        Directory.CreateDirectory(deeper);
        var deeperLink = Path.Combine(deeper, "b");
        CreateDirectoryLinkOrSkip(deeperLink, outside);
        Assert.False(SSHKeyPermissionFixer.IsFixableKeyFile(Path.Combine(deeperLink, "victim"), _sshDir, []));
    }

    /// <summary>
    /// ~/.ssh 自体が dotfiles 管理などでリンクになっている場合は、その実体の配下にある通常ファイルを対象にする。
    /// </summary>
    [Fact]
    public void IsFixableKeyFile_AcceptsKeyWhenSshDirectoryItselfIsLink()
    {
        var realSsh = Path.Combine(_root, "dotfiles-ssh");
        Directory.CreateDirectory(realSsh);
        File.WriteAllText(Path.Combine(realSsh, "id_ed25519"), "key");
        var linkedSsh = Path.Combine(_root, "linked-ssh");
        CreateDirectoryLinkOrSkip(linkedSsh, realSsh);

        Assert.True(SSHKeyPermissionFixer.IsFixableKeyFile(Path.Combine(linkedSsh, "id_ed25519"), linkedSsh, []));
    }

    /// <summary>
    /// 設定済みの鍵（リポジトリローカルの remote.*.sshkey を含む）と一致した場合も、途中の階層の
    /// リンク・ジャンクションを経由したパスは修正対象にしない。
    /// </summary>
    [Fact]
    public void IsFixableKeyFile_RejectsConfiguredKeyReachedThroughLinkedDirectory()
    {
        var outside = Path.Combine(_root, "outside-keys");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "deploy"), "key");
        var linkedDir = Path.Combine(_root, "repo", "keys");
        Directory.CreateDirectory(Path.GetDirectoryName(linkedDir)!);
        CreateDirectoryLinkOrSkip(linkedDir, outside);

        var throughLink = Path.Combine(linkedDir, "deploy");
        Assert.False(SSHKeyPermissionFixer.IsFixableKeyFile(throughLink, _sshDir, [throughLink]));
        // リンクを経由しない実体のパスで設定されていれば対象になり、検証済みパスとしてそのパスが返る
        var direct = Path.Combine(outside, "deploy");
        Assert.True(SSHKeyPermissionFixer.TryGetFixableKeyFile(direct, _sshDir, [direct], out var verified));
        Assert.Equal(Path.GetFullPath(direct), verified);
    }

    /// <summary>判定で確定したフルパス（相対パス・冗長な区切りを解決したもの）が返る。</summary>
    [Fact]
    public void TryGetFixableKeyFile_ReturnsVerifiedFullPath()
    {
        var key = Path.Combine(_sshDir, "id_ed25519");
        File.WriteAllText(key, "key");
        var indirect = Path.Combine(_sshDir, "sub", "..", "id_ed25519");
        Directory.CreateDirectory(Path.Combine(_sshDir, "sub"));
        Assert.True(SSHKeyPermissionFixer.TryGetFixableKeyFile(indirect, _sshDir, [], out var verified));
        Assert.Equal(Path.GetFullPath(key), verified);
        Assert.False(SSHKeyPermissionFixer.TryGetFixableKeyFile(Path.Combine(_sshDir, "missing"), _sshDir, [], out verified));
        Assert.Equal(string.Empty, verified);
    }

    private static void CreateDirectoryLinkOrSkip(string link, string target) => DirectoryLinks.CreateOrSkip(link, target);
}
