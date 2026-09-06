using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Komorebi.Native;

internal static partial class CommandCancellation
{
    internal static bool PrepareProcessGroup(ProcessStartInfo start)
    {
        string? helper = null;
        if (OperatingSystem.IsLinux())
            helper = File.Exists("/usr/bin/setsid") ? "/usr/bin/setsid" : "/bin/setsid";
        else if (OperatingSystem.IsMacOS())
            helper = Path.Combine(AppContext.BaseDirectory, "setsid");
        if (helper == null || !File.Exists(helper))
            return false;

        start.Arguments = start.FileName.Quoted() + " " + start.Arguments;
        start.FileName = helper;
        return true;
    }

    internal static void Terminate(Process process, bool ownsProcessGroup)
    {
        try
        {
            if (process.HasExited)
                return;

            // 独立セッションで起動した子だけに SIGTERM を送る。アプリ自身のグループは対象にしない。
            if (!OperatingSystem.IsWindows() && ownsProcessGroup && Kill(-process.Id, 15) == 0 && process.WaitForExit(2000))
                return;

            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // 終了と中止要求の競合は完了として扱う。
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // 終了済みの子に対する OS エラーをキャンセル元へ伝播させない。
        }
    }

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int Kill(int pid, int signal);
}
