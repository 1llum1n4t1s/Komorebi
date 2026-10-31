using Komorebi.Models;

namespace Komorebi.Tests.Models;

public sealed class LegacyLogMigrationTests
{
    [Fact]
    public void CollectLegacyMarkers_RecordsStaleMarkersAndPreservesLiveAndCurrentMarkers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"komorebi-log-migration-{Guid.NewGuid():N}");
        var old = Path.Combine(root, "old");
        var current = Path.Combine(root, "current");
        Directory.CreateDirectory(old);
        Directory.CreateDirectory(current);
        var currentName = $"startup-{Environment.ProcessId}.marker";
        var stale = Path.Combine(old, currentName);
        var live = Path.Combine(old, "startup-live.marker");
        File.WriteAllText(stale, "pid=-1\nversion=legacy-marker-test\nstage=Init\n");
        File.WriteAllText(live, $"pid={Environment.ProcessId}\n");
        var oldLink = Path.Combine(root, "old-link");
        CreateDirectoryLink(oldLink, old);

        StartupDiagnostics.Bind(current);
        try
        {
            StartupDiagnostics.CollectLegacyMarkers(oldLink);
            Assert.True(File.Exists(stale));
            StartupDiagnostics.CollectLegacyMarkers(old);
            Assert.False(File.Exists(stale));
            Assert.True(File.Exists(live));
            Assert.True(File.Exists(Path.Combine(current, currentName)));
            Assert.Contains(StartupDiagnostics.PreviousIncompleteStartups, detail => detail.Contains("legacy-marker-test"));
        }
        finally
        {
            StartupDiagnostics.MarkCompleted();
        }
    }

    [Fact]
    public void Move_DoesNotFollowDirectoryLinks()
    {
        var root = Path.Combine(Path.GetTempPath(), $"komorebi-log-migration-{Guid.NewGuid():N}");
        var old = Path.Combine(root, "old");
        var current = Path.Combine(root, "current");
        Directory.CreateDirectory(old);
        Directory.CreateDirectory(current);
        var log = Path.Combine(old, "Komorebi_link.log");
        File.WriteAllText(log, "old");
        var oldLink = Path.Combine(root, "old-link");
        CreateDirectoryLink(oldLink, old);
        var destinationLink = Path.Combine(root, "current-link");
        CreateDirectoryLink(destinationLink, current);

        LegacyLogMigration.Move(oldLink, current);
        LegacyLogMigration.Move(old, destinationLink);
        Assert.True(File.Exists(log));
        Assert.Empty(Directory.GetFiles(current));
        LegacyLogMigration.RemoveEmptyDirectory(oldLink);
        Assert.True(Directory.Exists(oldLink));
    }

    private static void CreateDirectoryLink(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }
        var info = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("/c");
        info.ArgumentList.Add("mklink");
        info.ArgumentList.Add("/J");
        info.ArgumentList.Add(link);
        info.ArgumentList.Add(target);
        using var process = System.Diagnostics.Process.Start(info)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    [Fact]
    public void Move_PreservesConflictsAndRetriesLockedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"komorebi-log-migration-{Guid.NewGuid():N}");
        var old = Path.Combine(root, "old");
        var current = Path.Combine(root, "current");
        Directory.CreateDirectory(old);
        Directory.CreateDirectory(current);
        var name = "Komorebi_20261031.log";
        File.WriteAllText(Path.Combine(old, name), "old");
        File.WriteAllText(Path.Combine(current, name), "current");
        var locked = Path.Combine(old, "Komorebi_20261030.log");
        File.WriteAllText(locked, "locked");
        File.WriteAllText(Path.Combine(old, "other.txt"), "unrelated");

        using (new FileStream(locked, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            LegacyLogMigration.Move(old, current);
            Assert.True(File.Exists(locked));
        }
        Assert.Equal("current", File.ReadAllText(Path.Combine(current, name)));
        Assert.Equal("old", File.ReadAllText(Assert.Single(Directory.GetFiles(current, "*_legacy_*.log"))));
        LegacyLogMigration.Move(old, current);
        Assert.False(File.Exists(locked));
        Assert.Equal("locked", File.ReadAllText(Path.Combine(current, Path.GetFileName(locked))));
        Assert.Equal("unrelated", File.ReadAllText(Path.Combine(old, "other.txt")));
        LegacyLogMigration.RemoveEmptyDirectory(old);
        Assert.True(Directory.Exists(old));
    }

    [Fact]
    public void Move_ConcurrentCallsPreserveEveryLogAndRemoveEmptySource()
    {
        var root = Path.Combine(Path.GetTempPath(), $"komorebi-log-migration-{Guid.NewGuid():N}");
        var old = Path.Combine(root, "old");
        var current = Path.Combine(root, "current");
        Directory.CreateDirectory(old);
        for (var i = 0; i < 20; i++)
            File.WriteAllText(Path.Combine(old, $"Komorebi_{i}.log"), i.ToString());

        Parallel.For(0, 4, _ => LegacyLogMigration.Move(old, current));
        LegacyLogMigration.Move(old, current);
        Assert.False(Directory.Exists(old));
        LegacyLogMigration.RemoveEmptyDirectory(root);
        Assert.True(Directory.Exists(root));
        var empty = Path.Combine(root, "empty");
        Directory.CreateDirectory(empty);
        LegacyLogMigration.RemoveEmptyDirectory(empty);
        Assert.False(Directory.Exists(empty));
        Assert.Equal(20, Directory.GetFiles(current, "*.log").Length);
        for (var i = 0; i < 20; i++)
            Assert.Equal(i.ToString(), File.ReadAllText(Path.Combine(current, $"Komorebi_{i}.log")));
    }
}
