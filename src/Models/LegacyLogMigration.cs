using System;
using System.IO;

namespace Komorebi.Models;

/// <summary>旧保存先の診断ログを、新しい保存先へ移す。</summary>
internal static class LegacyLogMigration
{
    internal static void Move(string sourceDirectory, string destinationDirectory)
    {
        try
        {
            if (!IsSafeDirectory(sourceDirectory) || HasLink(destinationDirectory))
                return;

            Directory.CreateDirectory(destinationDirectory);
            if (HasLink(destinationDirectory))
                return;

            foreach (var source in Directory.EnumerateFiles(sourceDirectory, "Komorebi_*.log"))
            {
                try
                {
                    if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                        continue;

                    // 実行中の旧版が書いているファイルは開けず、次回に持ち越す。
                    using var lockedSource = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                    var destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
                    if (File.Exists(destination))
                        destination = Path.Combine(destinationDirectory, $"{Path.GetFileNameWithoutExtension(source)}_legacy_{Guid.NewGuid():N}.log");
                    File.Move(source, destination);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            // 空でなくなった場合も含め、削除できなければ次回に持ち越す。
            Directory.Delete(sourceDirectory);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal static void RemoveEmptyDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory) && !HasLink(directory))
                Directory.Delete(directory);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal static bool IsSafeDirectory(string directory)
    {
        try
        {
            return Directory.Exists(directory) && !HasLink(directory);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool HasLink(string path)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(path)); directory is not null; directory = directory.Parent)
        {
            try
            {
                if ((File.GetAttributes(directory.FullName) & FileAttributes.ReparsePoint) != 0)
                    return true;
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        return false;
    }
}
