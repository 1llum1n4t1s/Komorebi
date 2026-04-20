using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Komorebi.Native;

/// <summary>
/// Windows固有のバックエンド実装。
/// レジストリ検索、DWM API、Shell APIなどWindows固有のAPIを使用する。
/// </summary>
[SupportedOSPlatform("windows")]
internal class Windows : OS.IBackend
{
    /// <summary>
    /// DWM（Desktop Window Manager）のマージン構造体。ウィンドウフレーム拡張に使用する。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    /// <summary>
    /// DWMフレームをクライアント領域に拡張するWin32 API。
    /// </summary>
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    /// <summary>
    /// PATH環境変数から実行ファイルを検索するWin32 API。
    /// </summary>
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern bool PathFindOnPath([In, Out] StringBuilder pszFile, [In] string[] ppszOtherDirs);

    /// <summary>
    /// ファイルパスからアイテムIDリストを作成するShell API。
    /// </summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern IntPtr ILCreateFromPathW(string pszPath);

    /// <summary>
    /// アイテムIDリストを解放するShell API。
    /// </summary>
    [DllImport("shell32.dll", SetLastError = false)]
    private static extern void ILFree(IntPtr pidl);

    /// <summary>
    /// エクスプローラーでフォルダを開いてアイテムを選択するShell API。
    /// </summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, int cild, IntPtr apidl, int dwFlags);

    /// <summary>
    /// AvaloniaアプリケーションビルダーにWindows固有の設定を適用する。
    /// Windows 10ではドロップシャドウの問題を修正する。
    /// </summary>
    public void SetupApp(AppBuilder builder)
    {
        // Windows 10（ビルド22000未満）でのドロップシャドウ問題を修正する
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            Window.WindowStateProperty.Changed.AddClassHandler<Window>((w, _) => FixWindowFrameOnWin10(w));
            Control.LoadedEvent.AddClassHandler<Window>((w, _) => FixWindowFrameOnWin10(w));
        }
    }

    /// <summary>
    /// ウィンドウにWindows固有のクロムレス設定を適用する。
    /// </summary>
    public void SetupWindow(Window window)
    {
        // カスタムタイトルバーを使用するためデコレーションなしに設定する
        window.WindowDecorations = WindowDecorations.None;
        window.BorderThickness = new Thickness(1);
    }

    /// <summary>Windows では no-op（upstream 29cf5fc5 互換のスタブ）。</summary>
    public void HideSelf() { }

    /// <summary>Windows では no-op（upstream 29cf5fc5 互換のスタブ）。</summary>
    public void HideOtherApplications() { }

    /// <summary>Windows では no-op（upstream 29cf5fc5 互換のスタブ）。</summary>
    public void ShowAllApplications() { }

    /// <summary>
    /// アプリケーションデータディレクトリのパスを返す。
    /// ポータブルモード（実行ファイル横のdataフォルダ）を優先する。
    /// </summary>
    public string GetDataDir()
    {
        // ポータブルモード: 実行ファイルと同じディレクトリのdataフォルダを確認する
        var execFile = Process.GetCurrentProcess().MainModule!.FileName;
        var portableDir = Path.Combine(Path.GetDirectoryName(execFile)!, "data");
        if (Directory.Exists(portableDir))
            return portableDir;

        // 通常モード: AppDataフォルダを使用する
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Komorebi");
    }

    /// <summary>
    /// Windowsレジストリおよびパス検索でgit実行ファイルを探す。
    /// </summary>
    public string FindGitExecutable()
    {
        // レジストリからGit for Windowsのインストールパスを探す
        var reg = Microsoft.Win32.RegistryKey.OpenBaseKey(
            Microsoft.Win32.RegistryHive.LocalMachine,
            Microsoft.Win32.RegistryView.Registry64);

        var git = reg.OpenSubKey(@"SOFTWARE\GitForWindows");
        if (git?.GetValue("InstallPath") is string installPath)
            return Path.Combine(installPath, "bin", "git.exe");

        // PATH環境変数からgit.exeを検索する
        var builder = new StringBuilder("git.exe", 259);
        if (!PathFindOnPath(builder, null))
            return null;

        var exePath = builder.ToString();
        if (!string.IsNullOrEmpty(exePath))
            return exePath;

        return null;
    }

    /// <summary>
    /// 指定されたシェル/ターミナルの実行ファイルパスを検索する。
    /// git-bash、PowerShell、cmd、Windows Terminalに対応する。
    /// </summary>
    public string FindTerminal(Models.ShellOrTerminal shell)
    {
        switch (shell.Type)
        {
            case "git-bash":
                // Git Bashの実行ファイルを探す
                if (string.IsNullOrEmpty(OS.GitExecutable))
                    break;

                var binDir = Path.GetDirectoryName(OS.GitExecutable)!;
                var bash = Path.GetFullPath(Path.Combine(binDir, "..", "git-bash.exe"));
                if (!File.Exists(bash))
                    break;

                return bash;
            case "pwsh":
                // PowerShellをレジストリとPATHから探す
                var localMachine = Microsoft.Win32.RegistryKey.OpenBaseKey(
                        Microsoft.Win32.RegistryHive.LocalMachine,
                        Microsoft.Win32.RegistryView.Registry64);

                var pwsh = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\pwsh.exe");
                if (pwsh is not null)
                {
                    var path = pwsh.GetValue(null) as string;
                    if (File.Exists(path))
                        return path;
                }

                var pwshFinder = new StringBuilder("powershell.exe", 512);
                if (PathFindOnPath(pwshFinder, null))
                    return pwshFinder.ToString();

                break;
            case "cmd":
                // コマンドプロンプトのパスを返す
                return @"C:\Windows\System32\cmd.exe";
            case "wt":
                // Windows TerminalをPATHから探す
                var wtFinder = new StringBuilder("wt.exe", 512);
                if (PathFindOnPath(wtFinder, null))
                    return wtFinder.ToString();

                break;
        }

        return string.Empty;
    }

    /// <summary>
    /// 外部マージ/diffツールの実行ファイルをWindowsシステムから検索する。
    /// App Pathsレジストリ → Program Files → PATH環境変数の順に探索する。
    /// </summary>
    /// <param name="patterns">検索する実行ファイル名の配列（例: "WinMergeU.exe"）</param>
    /// <returns>見つかった実行ファイルのフルパス。見つからない場合はnull。</returns>
    public string FindExternalMergerExecFile(string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            // 1. App Pathsレジストリから検索する（WinMerge等が登録している）
            var appPath = FindExecFileFromAppPaths(pattern);
            if (!string.IsNullOrEmpty(appPath))
                return appPath;

            // 2. Program Files ディレクトリを検索する
            var programFilesPath = FindExecFileFromProgramFiles(pattern);
            if (!string.IsNullOrEmpty(programFilesPath))
                return programFilesPath;

            // 3. PATH環境変数から検索する
            var pathEnvPath = FindExecFileFromPath(pattern);
            if (!string.IsNullOrEmpty(pathEnvPath))
                return pathEnvPath;
        }

        return null;
    }

    /// <summary>
    /// Windowsにインストールされている外部エディタ/IDEを検出する。
    /// VSCode、Cursor、JetBrains、Sublime Text、Visual Studio、Zedに対応する。
    /// </summary>
    public List<Models.ExternalTool> FindExternalTools()
    {
        var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var finder = new Models.ExternalToolsFinder();
        // 各エディタの検索を実行する
        finder.VSCode(FindVSCode);
        finder.VSCodeInsiders(FindVSCodeInsiders);
        finder.VSCodium(FindVSCodium);
        finder.Cursor(() => Path.Combine(localAppDataDir, @"Programs\Cursor\Cursor.exe"));
        finder.FindJetBrainsFromToolbox(() => Path.Combine(localAppDataDir, @"JetBrains\Toolbox"));
        finder.SublimeText(FindSublimeText);
        finder.Zed(FindZed);
        FindVisualStudio(finder);
        return finder.Tools;
    }

    /// <summary>
    /// デフォルトブラウザでURLを開く。
    /// cmd /c start 経由は cmd.exe のメタキャラクタを解釈するためコマンド注入の余地があり、
    /// 任意コード実行に至るため、ShellExecute 直叩き（UseShellExecute=true）に切り替える。
    /// 事前に Uri で http/https/mailto スキームのみを許可することで、不正スキームがシェルに渡るのを防ぐ。
    /// </summary>
    public void OpenBrowser(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeMailto)
            return;

        var info = new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true,
        };
        Process.Start(info)?.Dispose();
    }

    /// <summary>
    /// 指定された作業ディレクトリでターミナルを起動する。
    /// </summary>
    public void OpenTerminal(string workdir, string args)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cwd = string.IsNullOrEmpty(workdir) ? home : workdir;
        var terminal = OS.ShellOrTerminal;

        // ターミナルの存在を確認する
        if (!File.Exists(terminal))
        {
            App.RaiseException(workdir, App.Text("Error.TerminalNotSpecified"));
            return;
        }

        // ターミナルプロセスを起動する
        var startInfo = new ProcessStartInfo();
        startInfo.WorkingDirectory = cwd;
        startInfo.FileName = terminal;
        startInfo.Arguments = args;
        Process.Start(startInfo)?.Dispose();
    }

    /// <summary>
    /// エクスプローラーで指定パスを開く。ファイルの場合はそのファイルを選択状態にする。
    /// </summary>
    public void OpenInFileManager(string path)
    {
        if (File.Exists(path))
        {
            // Shell APIを使ってファイルを選択した状態でエクスプローラーを開く
            var pidl = ILCreateFromPathW(new FileInfo(path).FullName);

            try
            {
                SHOpenFolderAndSelectItems(pidl, 0, 0, 0);
            }
            finally
            {
                ILFree(pidl);
            }

            return;
        }

        // ディレクトリの場合はそのまま開く
        var dir = new DirectoryInfo(path).FullName + Path.DirectorySeparatorChar;
        Process.Start(new ProcessStartInfo(dir)
        {
            UseShellExecute = true,
            CreateNoWindow = true,
        })?.Dispose();
    }

    /// <summary>
    /// Windowsのcmd /c startコマンドでデフォルトエディタを開く。
    /// </summary>
    public void OpenWithDefaultEditor(string file)
    {
        var info = new FileInfo(file);
        var start = new ProcessStartInfo("cmd", $"""/c start "" {info.FullName.Quoted()}""");
        start.CreateNoWindow = true;
        Process.Start(start)?.Dispose();
    }

    #region HELPER_METHODS
    /// <summary>
    /// Windows 10でのウィンドウフレーム描画問題を修正する。
    /// DWM APIを使ってフレームをクライアント領域に拡張する。
    /// </summary>
    private static void FixWindowFrameOnWin10(Window w)
    {
        // レンダリングフレームでDWMフレーム拡張を実行する
        Dispatcher.UIThread.Post(() =>
        {
            var platformHandle = w.TryGetPlatformHandle();
            if (platformHandle is null)
                return;

            var margins = new MARGINS { cxLeftWidth = 1, cxRightWidth = 1, cyTopHeight = 1, cyBottomHeight = 1 };
            DwmExtendFrameIntoClientArea(platformHandle.Handle, ref margins);
        }, DispatcherPriority.Render);
    }

    /// <summary>
    /// 指定パス内のVisual Studioソリューションファイルを検索して起動オプション一覧を生成する。
    /// </summary>
    private List<Models.ExternalTool.LaunchOption> GenerateVSProjectLaunchOptions(string path)
    {
        var root = new DirectoryInfo(path);
        if (!root.Exists)
            return null;

        List<Models.ExternalTool.LaunchOption> options = [];
        var prefixLen = root.FullName.Length;
        // ディレクトリを再帰的に探索して.slnと.slnxファイルを収集する
        root.WalkFiles(f =>
        {
            if (f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                var display = f[prefixLen..].TrimStart(Path.DirectorySeparatorChar);
                options.Add(new(display, f.Quoted()));
            }
        });
        return options;
    }
    #endregion

    #region EXTERNAL_EDITOR_FINDER

    // レジストリ値名の定数（重複排除）
    private const string RegistryDisplayIcon = "DisplayIcon";
    private const string RegistryUninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";

    /// <summary>
    /// WindowsレジストリからDisplayIconを検索する共通メソッド。
    /// システムインストール（LocalMachine）とユーザーインストール（CurrentUser）の両方を確認する。
    /// 3メソッド（FindVSCode/FindVSCodeInsiders/FindVSCodium）の重複を統合。
    /// </summary>
    /// <param name="systemGuid">システムインストールのレジストリGUID。</param>
    /// <param name="userGuid">ユーザーインストールのレジストリGUID。</param>
    /// <returns>DisplayIconの値。見つからない場合は空文字列。</returns>
    private static string FindRegistryDisplayIcon(string systemGuid, string userGuid)
    {
        // システムインストールを確認（using でリソースリークを防止）
        using var localMachine = Microsoft.Win32.RegistryKey.OpenBaseKey(
                Microsoft.Win32.RegistryHive.LocalMachine,
                Microsoft.Win32.RegistryView.Registry64);
        using var systemKey = localMachine.OpenSubKey($@"{RegistryUninstallPath}{systemGuid}_is1");
        if (systemKey?.GetValue(RegistryDisplayIcon) is string systemIcon)
            return systemIcon;

        // ユーザーインストールを確認
        using var currentUser = Microsoft.Win32.RegistryKey.OpenBaseKey(
                Microsoft.Win32.RegistryHive.CurrentUser,
                Microsoft.Win32.RegistryView.Registry64);
        using var userKey = currentUser.OpenSubKey($@"{RegistryUninstallPath}{userGuid}_is1");
        return userKey?.GetValue(RegistryDisplayIcon) as string ?? string.Empty;
    }

    private string FindVSCode()
        => FindRegistryDisplayIcon("{EA457B21-F73E-494C-ACAB-524FDE069978}", "{771FD6B0-FA20-440A-A002-3B3BAC16DC50}");

    private string FindVSCodeInsiders()
        => FindRegistryDisplayIcon("{1287CAD5-7C8D-410D-88B9-0D1EE4A83FF2}", "{217B4C08-948D-4276-BFBB-BEE930AE5A2C}");

    private string FindVSCodium()
        => FindRegistryDisplayIcon("{88DA3577-054F-4CA1-8122-7D820494CFFB}", "{2E1F05D1-C245-4562-81EE-28188DB6FD17}");

    /// <summary>
    /// WindowsレジストリからSublime Textのインストールパスを検索する。
    /// バージョン3と4の両方を確認する。using でリソースリークを防止。
    /// </summary>
    private string FindSublimeText()
    {
        using var localMachine = Microsoft.Win32.RegistryKey.OpenBaseKey(
                Microsoft.Win32.RegistryHive.LocalMachine,
                Microsoft.Win32.RegistryView.Registry64);

        // Sublime Text 4
        using var sublime = localMachine.OpenSubKey($@"{RegistryUninstallPath}Sublime Text_is1");
        if (sublime?.GetValue(RegistryDisplayIcon) is string icon4)
            return Path.Combine(Path.GetDirectoryName(icon4)!, "subl.exe");

        // Sublime Text 3
        using var sublime3 = localMachine.OpenSubKey($@"{RegistryUninstallPath}Sublime Text 3_is1");
        if (sublime3?.GetValue(RegistryDisplayIcon) is string icon3)
            return Path.Combine(Path.GetDirectoryName(icon3)!, "subl.exe");

        return string.Empty;
    }

    /// <summary>
    /// vswhereツールを使用してVisual Studioのインストールを検出する。
    /// プレリリース版も含めて検索する。
    /// </summary>
    private void FindVisualStudio(Models.ExternalToolsFinder finder)
    {
        // vswhereの存在を確認する
        var vswhere = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe");
        if (!File.Exists(vswhere))
            return;

        // vswhereをJSON形式で実行してインストール情報を取得する
        var startInfo = new ProcessStartInfo();
        startInfo.FileName = vswhere;
        startInfo.Arguments = "-format json -prerelease -utf8";
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        startInfo.RedirectStandardOutput = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;

        try
        {
            using var proc = Process.Start(startInfo)!;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode == 0)
            {
                // JSONをデシリアライズしてVisual Studioインスタンスごとにツールを登録する
                var instances = JsonSerializer.Deserialize(output, JsonCodeGen.Default.ListVisualStudioInstance);
                foreach (var instance in instances)
                {
                    var exec = instance.ProductPath;
                    var icon = instance.IsPrerelease ? "vs-preview" : "vs";
                    finder.TryAdd(instance.DisplayName, icon, () => exec, GenerateVSProjectLaunchOptions);
                }
            }
        }
        catch
        {
            // Just ignore.
        }
    }

    /// <summary>
    /// WindowsレジストリおよびPATHからZedエディタを検索する。
    /// </summary>
    private string FindZed()
    {
        using var currentUser = Microsoft.Win32.RegistryKey.OpenBaseKey(
                Microsoft.Win32.RegistryHive.CurrentUser,
                Microsoft.Win32.RegistryView.Registry64);

        // NOTE: this is the official Zed Preview reg data.
        using var preview = currentUser.OpenSubKey($@"{RegistryUninstallPath}{{F70E4811-D0E2-4D88-AC99-D63752799F95}}_is1");
        if (preview?.GetValue(RegistryDisplayIcon) is string icon)
            return icon;

        // PATHからzed.exeを検索する
        var findInPath = new StringBuilder("zed.exe", 512);
        if (PathFindOnPath(findInPath, null))
            return findInPath.ToString();

        return string.Empty;
    }
    #endregion

    #region EXTERNAL_MERGER_FINDER
    /// <summary>
    /// App Pathsレジストリから実行ファイルのパスを検索する。
    /// WinMerge等のアプリケーションはここに登録している。
    /// </summary>
    /// <param name="exeName">実行ファイル名（例: "WinMergeU.exe"）</param>
    /// <returns>見つかった場合はフルパス、見つからない場合はnull。</returns>
    private static string FindExecFileFromAppPaths(string exeName)
    {
        try
        {
            using var localMachine = Microsoft.Win32.RegistryKey.OpenBaseKey(
                Microsoft.Win32.RegistryHive.LocalMachine,
                Microsoft.Win32.RegistryView.Registry64);

            using var appPaths = localMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exeName}");
            if (appPaths?.GetValue(null) is string path && File.Exists(path))
                return path;
        }
        catch
        {
            // レジストリアクセス失敗時は無視する
        }

        return null;
    }

    /// <summary>
    /// Program Filesディレクトリから実行ファイルを検索する。
    /// 1階層目のサブフォルダのみを対象として高速に探索する。
    /// </summary>
    /// <param name="exeName">実行ファイル名（例: "WinMergeU.exe"）</param>
    /// <returns>見つかった場合はフルパス、見つからない場合はnull。</returns>
    private static string FindExecFileFromProgramFiles(string exeName)
    {
        // Program Files と Program Files (x86) の両方を確認する
        var programDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };

        foreach (var programDir in programDirs)
        {
            if (string.IsNullOrEmpty(programDir) || !Directory.Exists(programDir))
                continue;

            try
            {
                // 各サブフォルダ直下の実行ファイルを確認する（1階層のみ）
                foreach (var subDir in Directory.GetDirectories(programDir))
                {
                    var candidate = Path.Combine(subDir, exeName);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch
            {
                // アクセス権限エラー等は無視する
            }
        }

        return null;
    }

    /// <summary>
    /// PATH環境変数から実行ファイルを検索する。
    /// </summary>
    /// <param name="exeName">実行ファイル名（例: "WinMergeU.exe"）</param>
    /// <returns>見つかった場合はフルパス、見つからない場合はnull。</returns>
    private static string FindExecFileFromPath(string exeName)
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in paths)
        {
            var candidate = Path.Combine(path, exeName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
    #endregion
}
