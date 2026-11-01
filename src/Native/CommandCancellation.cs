using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Komorebi.Native;

internal static partial class CommandCancellation
{
    // 上流との差分: TERMでコマンドだけが先に終了しても、監督プロセスがグループを保持する。
    // 引数は固定スクリプトの位置引数で渡し、シェルのコードとして評価しない。
    private const string SupervisorScript = """
        trap '/bin/sleep 2; kill -s KILL -- -$$' TERM
        exec 3<&0
        (trap - INT QUIT; exec "$@" <&3 3<&-) &
        child=$!
        exec 3<&-
        wait "$child"
        exit $?
        """;

    internal static bool PrepareProcessGroup(ProcessStartInfo start)
    {
        string? helper = null;
        if (OperatingSystem.IsLinux())
            helper = File.Exists("/usr/bin/setsid") ? "/usr/bin/setsid" : "/bin/setsid";
        else if (OperatingSystem.IsMacOS())
            helper = Path.Combine(AppContext.BaseDirectory, "setsid");
        if (helper == null || !File.Exists(helper))
            return false;

        start.Arguments = "/bin/sh -c " + SupervisorScript.Quoted() + " -- " + start.FileName.Quoted() + " " + start.Arguments;
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
            if (!OperatingSystem.IsWindows() && ownsProcessGroup && Kill(-process.Id, 15) == 0)
            {
                if (process.WaitForExit(2000) || process.HasExited)
                    return;

                // 監督プロセスが生存している間だけ、所有グループの残存子へ強制終了を送る。
                if (Kill(-process.Id, 9) == 0)
                    return;
            }

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
        catch (AggregateException)
        {
            // 一部の子が先に終了した場合などの tree kill の失敗を中止要求元へ伝播させない。
        }
    }

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int Kill(int pid, int signal);
}
