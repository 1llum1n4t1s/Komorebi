using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

using Avalonia;
using Avalonia.Controls;

namespace Komorebi.Native;

/// <summary>
/// Linux固有のバックエンド実装。
/// xdg-openコマンド、PATH検索、AppImageポータブルモードに対応する。
/// </summary>
[SupportedOSPlatform("linux")]
internal class Linux : OS.IBackend
{
    /// <summary>
    /// AvaloniaアプリケーションビルダーにLinux固有の設定を適用する。
    /// X11プラットフォームでIMEを有効化する。
    /// </summary>
    public void SetupApp(AppBuilder builder)
    {
        // X11プラットフォームでIME（入力メソッド）を有効にする
        builder.With(new X11PlatformOptions() { EnableIme = true });
    }

    /// <summary>
    /// ウィンドウにLinux固有の設定を適用する。
    /// システムウィンドウフレーム使用設定に応じてクロムを切り替える。
    /// </summary>
    public void SetupWindow(Window window)
    {
        if (OS.UseSystemWindowFrame)
        {
            // システムウィンドウフレームを使用する場合（フルデコレーション）
            window.WindowDecorations = WindowDecorations.Full;
        }
        else
        {
            // カスタムウィンドウフレームを使用する場合（デコレーションなし）
            window.WindowDecorations = WindowDecorations.None;
            window.Classes.Add("custom_window_frame");
        }
    }

    /// <summary>Linux では no-op（upstream 29cf5fc5 互換のスタブ）。</summary>
    public void HideSelf() { }

    /// <summary>Linux では no-op（upstream 29cf5fc5 互換のスタブ）。</summary>
    public void HideOtherApplications() { }

    /// <summary>Linux では no-op（upstream 29cf5fc5 互換のスタブ）。</summary>
    public void ShowAllApplications() { }

    /// <summary>
    /// Linuxのアプリケーションデータディレクトリパスを返す。
    /// AppImageポータブルモード、~/.komorebi、旧設定ディレクトリからの移行に対応する。
    /// </summary>
    public string GetDataDir()
    {
        // AppImageのポータブルモードを確認する
        var appImage = Environment.GetEnvironmentVariable("APPIMAGE");
        if (!string.IsNullOrEmpty(appImage) && File.Exists(appImage))
        {
            var portableDir = Path.Combine(Path.GetDirectoryName(appImage)!, "data");
            if (Directory.Exists(portableDir))
                return portableDir;
        }

        // 標準データディレクトリ: ~/.komorebi
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dataDir = Path.Combine(home, ".komorebi");
        if (Directory.Exists(dataDir))
            return dataDir;

        // 旧データディレクトリ ~/.config/Komorebi からの移行を試みる
        var oldDataDir = Path.Combine(home, ".config", "Komorebi");
        if (Directory.Exists(oldDataDir))
        {
            try
            {
                Directory.Move(oldDataDir, dataDir);
            }
            catch
            {
                // Ignore errors
            }
        }

        return dataDir;
    }

    /// <summary>
    /// PATH環境変数からgit実行ファイルを検索する。
    /// </summary>
    public string FindGitExecutable()
    {
        return FindExecutable("git");
    }

    /// <summary>
    /// 指定されたシェル/ターミナルの実行ファイルをPATHから検索する。
    /// カスタムタイプの場合は空文字を返す。
    /// </summary>
    public string FindTerminal(Models.ShellOrTerminal shell)
    {
        // カスタムタイプはユーザーが直接パスを指定するため検索しない
        if (shell.Type.Equals("custom", StringComparison.Ordinal))
            return string.Empty;

        return FindExecutable(shell.Exec);
    }

    /// <summary>
    /// 外部マージ/diffツールの実行ファイルをLinuxシステムから検索する。
    /// PATH環境変数から検索する。
    /// </summary>
    public string FindExternalMergerExecFile(string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var found = FindExecutable(pattern);
            if (!string.IsNullOrEmpty(found))
                return found;
        }

        return null;
    }

    /// <summary>
    /// Linuxにインストールされている外部エディタ/IDEを検出する。
    /// PATHから各エディタのコマンドを検索する。
    /// </summary>
    public List<Models.ExternalTool> FindExternalTools()
    {
        var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var finder = new Models.ExternalToolsFinder();
        // 各エディタのコマンドをPATHから検索する
        finder.VSCode(() => FindExecutable("code"));
        finder.VSCodeInsiders(() => FindExecutable("code-insiders"));
        finder.VSCodium(() => FindExecutable("codium"));
        finder.Cursor(() => FindExecutable("cursor"));
        finder.FindJetBrainsFromToolbox(() => Path.Combine(localAppDataDir, "JetBrains/Toolbox"));
        finder.SublimeText(() => FindExecutable("subl"));
        finder.Zed(() =>
        {
            // Zedは"zeditor"と"zed"の両方のコマンド名で検索する
            var exec = FindExecutable("zeditor");
            return string.IsNullOrEmpty(exec) ? FindExecutable("zed") : exec;
        });
        return finder.Tools;
    }

    /// <summary>
    /// BROWSER環境変数またはxdg-openでデフォルトブラウザを開く。
    /// </summary>
    public void OpenBrowser(string url)
    {
        // BROWSER環境変数が設定されていればそれを使用し、なければxdg-openを使う
        var browser = Environment.GetEnvironmentVariable("BROWSER");
        if (string.IsNullOrEmpty(browser))
            browser = "xdg-open";
        Process.Start(browser, url.Quoted())?.Dispose();
    }

    /// <summary>
    /// xdg-openでファイルマネージャーを開く。
    /// ファイルの場合はその親ディレクトリを開く。
    /// </summary>
    public void OpenInFileManager(string path)
    {
        if (Directory.Exists(path))
        {
            // ディレクトリの場合はそのまま開く
            Process.Start("xdg-open", path.Quoted())?.Dispose();
        }
        else
        {
            // ファイルの場合は親ディレクトリを開く
            var dir = Path.GetDirectoryName(path);
            if (Directory.Exists(dir))
                Process.Start("xdg-open", dir.Quoted())?.Dispose();
        }
    }

    /// <summary>
    /// 指定された作業ディレクトリでターミナルを起動する。
    /// </summary>
    public void OpenTerminal(string workdir, string args)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cwd = string.IsNullOrEmpty(workdir) ? home : workdir;

        // ターミナルプロセスを起動する
        var startInfo = new ProcessStartInfo();
        startInfo.WorkingDirectory = cwd;
        startInfo.FileName = OS.ShellOrTerminal;
        startInfo.Arguments = args;

        try
        {
            Process.Start(startInfo)?.Dispose();
        }
        catch (Exception e)
        {
            App.RaiseException(workdir, App.Text("Error.FailedToStartTerminal", OS.ShellOrTerminal, e.Message));
        }
    }

    /// <summary>
    /// xdg-openでデフォルトアプリケーションでファイルを開く。
    /// </summary>
    public void OpenWithDefaultEditor(string file)
    {
        var proc = Process.Start("xdg-open", file.Quoted());
        if (proc is not null)
        {
            // プロセスの終了を待って結果を確認する
            proc.WaitForExit();

            if (proc.ExitCode != 0)
                App.RaiseException("", App.Text("Error.FailedToOpenFile", file));

            proc.Close();
        }
    }

    /// <summary>
    /// PATH環境変数および ~/.local/bin から実行ファイルを検索する。
    /// </summary>
    private static string FindExecutable(string filename)
    {
        // PATH環境変数の各ディレクトリを順に確認する
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in paths)
        {
            var test = Path.Combine(path, filename);
            if (File.Exists(test))
                return test;
        }

        // ~/.local/bin も確認する
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", filename);
        return File.Exists(local) ? local : string.Empty;
    }
}
