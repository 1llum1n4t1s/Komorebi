using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Komorebi.Native
{
    /// <summary>
    ///     macOS固有のバックエンド実装。
    ///     Homebrewのパス修正、macOS標準コマンド（open等）を使用する。
    /// </summary>
    [SupportedOSPlatform("macOS")]
    internal class MacOS : OS.IBackend
    {
        /// <summary>
        ///     AvaloniaアプリケーションビルダーにmacOS固有の設定を適用する。
        ///     デフォルトメニュー項目を無効化し、PATH環境変数を修正する。
        /// </summary>
        public void SetupApp(AppBuilder builder)
        {
            // macOSのデフォルトアプリケーションメニューを無効化する
            builder.With(new MacOSPlatformOptions()
            {
                DisableDefaultApplicationMenuItems = true,
            });

            // macOSのPATH環境変数を修正してHomebrewパスを含める
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                path = "/opt/homebrew/bin:/opt/homebrew/sbin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin";
            else if (!path.Contains("/opt/homebrew/", StringComparison.Ordinal))
                path = "/opt/homebrew/bin:/opt/homebrew/sbin:" + path;

            // カスタムPATHファイルが存在する場合はその内容で上書きする
            var customPathFile = Path.Combine(OS.DataDir, "PATH");
            if (File.Exists(customPathFile))
            {
                var env = File.ReadAllText(customPathFile).Trim();
                if (!string.IsNullOrEmpty(env))
                    path = env;
            }

            Environment.SetEnvironmentVariable("PATH", path);
        }

        /// <summary>
        ///     ウィンドウにmacOS固有のクロム設定を適用する。
        ///     システムクロム（信号機ボタン）を使用する。
        /// </summary>
        public void SetupWindow(Window window)
        {
            window.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.SystemChrome;
            window.ExtendClientAreaToDecorationsHint = true;
        }

        /// <summary>
        ///     macOSのアプリケーションデータディレクトリパスを返す。
        ///     ~/Library/Application Support/Komorebi を使用する。
        /// </summary>
        public string GetDataDir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Komorebi");
        }

        /// <summary>
        ///     macOSの一般的なパスからgit実行ファイルを検索する。
        ///     /usr/bin、/usr/local/bin、Homebrewのパスを順に確認する。
        /// </summary>
        public string FindGitExecutable()
        {
            // macOSでgitが存在する可能性のあるパスを順に確認する
            var gitPathVariants = new List<string>() {
                "/usr/bin/git",
                "/usr/local/bin/git",
                "/opt/homebrew/bin/git",
                "/opt/homebrew/opt/git/bin/git"
            };

            foreach (var path in gitPathVariants)
                if (File.Exists(path))
                    return path;

            return string.Empty;
        }

        /// <summary>
        ///     macOSのターミナルアプリの実行パスを返す。
        /// </summary>
        public string FindTerminal(Models.ShellOrTerminal shell)
        {
            return shell.Exec;
        }

        /// <summary>
        ///     外部マージ/diffツールの実行ファイルをmacOSシステムから検索する。
        ///     パターンに一致するファイルが存在するか確認する。
        /// </summary>
        public string FindExternalMergerExecFile(string[] patterns)
        {
            // macOSではExternalMergerのFinderフィールドにフルパスが定義されている。
            // AutoSelectExternalMergeToolExecFile()から呼ばれる前にFinderパスは
            // GetPatternsToFindExecFile()でファイル名のみに変換されるため、
            // PATHから検索を試みる。
            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var paths = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pattern in patterns)
            {
                foreach (var path in paths)
                {
                    var candidate = Path.Combine(path, pattern);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            return null;
        }

        /// <summary>
        ///     macOSにインストールされている外部エディタ/IDEを検出する。
        ///     /Applications 配下のアプリケーションバンドルを確認する。
        /// </summary>
        public List<Models.ExternalTool> FindExternalTools()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var finder = new Models.ExternalToolsFinder();
            // 各エディタのmacOS標準インストールパスを確認する
            finder.VSCode(() => "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code");
            finder.VSCodeInsiders(() => "/Applications/Visual Studio Code - Insiders.app/Contents/Resources/app/bin/code");
            finder.VSCodium(() => "/Applications/VSCodium.app/Contents/Resources/app/bin/codium");
            finder.Cursor(() => "/Applications/Cursor.app/Contents/Resources/app/bin/cursor");
            finder.FindJetBrainsFromToolbox(() => Path.Combine(home, "Library/Application Support/JetBrains/Toolbox"));
            finder.SublimeText(() => "/Applications/Sublime Text.app/Contents/SharedSupport/bin/subl");
            finder.Zed(() => File.Exists("/usr/local/bin/zed") ? "/usr/local/bin/zed" : "/Applications/Zed.app/Contents/MacOS/cli");
            return finder.Tools;
        }

        /// <summary>
        ///     macOSのopenコマンドでデフォルトブラウザを開く。
        /// </summary>
        public void OpenBrowser(string url)
        {
            Process.Start("open", url)?.Dispose();
        }

        /// <summary>
        ///     macOSのopenコマンドでFinderを開く。
        ///     ファイルの場合は -R オプションで親フォルダ内のファイルを選択表示する。
        /// </summary>
        public void OpenInFileManager(string path)
        {
            if (Directory.Exists(path))
                Process.Start("open", path.Quoted())?.Dispose();
            else if (File.Exists(path))
                Process.Start("open", $"{path.Quoted()} -R")?.Dispose();
        }

        /// <summary>
        ///     macOSのopenコマンドで指定ターミナルアプリを起動する。
        /// </summary>
        public void OpenTerminal(string workdir, string _)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = string.IsNullOrEmpty(workdir) ? home : workdir;
            // -a オプションでアプリケーション名を指定して起動する
            Process.Start("open", $"-a {OS.ShellOrTerminal} {dir.Quoted()}")?.Dispose();
        }

        /// <summary>
        ///     macOSのopenコマンドでデフォルトアプリケーションでファイルを開く。
        /// </summary>
        public void OpenWithDefaultEditor(string file)
        {
            Process.Start("open", file.Quoted())?.Dispose();
        }
    }
}
