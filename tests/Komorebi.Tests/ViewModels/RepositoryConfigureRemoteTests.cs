using System.Diagnostics;
using System.Reflection;

namespace Komorebi.Tests.ViewModels;

public class RepositoryConfigureRemoteTests : IDisposable
{
    public RepositoryConfigureRemoteTests()
    {
        Komorebi.Native.OS.SetupDataDir();

        var git = Komorebi.Native.OS.FindGitExecutable();
        Assert.False(string.IsNullOrWhiteSpace(git));
        Komorebi.Native.OS.GitExecutable = git;

        _repoDir = Path.Combine(Path.GetTempPath(), "komorebi-repo-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_repoDir);

        RunGit("init");
        RunGit("remote", "add", "origin", InitialUrl);
    }

    [Fact]
    public async Task SaveAsync_ProtocolSwitch_UpdatesGitRemoteAndRemoteModel()
    {
        var remote = new Komorebi.Models.Remote
        {
            Name = "origin",
            URL = InitialUrl,
        };

        var repo = new Komorebi.ViewModels.Repository(false, _repoDir, Path.Combine(_repoDir, ".git"));
        SetPrivateField(repo, "_settings", new Komorebi.Models.RepositorySettings());
        SetPrivateField(repo, "_remotes", new List<Komorebi.Models.Remote> { remote });

        var configure = new Komorebi.ViewModels.RepositoryConfigure(repo)
        {
            UserName = string.Empty,
            UserEmail = string.Empty,
            GPGUserSigningKey = string.Empty,
            HttpProxy = string.Empty,
        };

        configure.SelectedRemoteUrl = SwitchedUrl;
        configure.SelectedRemoteSSHKey = "dummy-key";

        await configure.SaveAsync();

        var savedUrl = RunGit("config", "--get", "remote.origin.url");
        Assert.Equal(SwitchedUrl, savedUrl);
        Assert.Equal(SwitchedUrl, remote.URL);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_repoDir))
                Directory.Delete(_repoDir, true);
        }
        catch
        {
            // Ignore cleanup failures in tests.
        }
    }

    private string RunGit(params string[] args)
    {
        var start = new ProcessStartInfo
        {
            FileName = Komorebi.Native.OS.GitExecutable,
            WorkingDirectory = _repoDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var arg in args)
            start.ArgumentList.Add(arg);

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', args)} failed: {stderr}");
        return stdout.Trim();
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private const string InitialUrl = "https://github.com/example/repo.git";
    private const string SwitchedUrl = "git@github.com:example/repo.git";

    private readonly string _repoDir;
}
