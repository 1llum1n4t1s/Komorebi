using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Controls;

namespace Komorebi.Native;

/// <summary>
/// OS固有機能へのファサード（静的クラス）。
/// プラットフォームごとの実装（Windows/MacOS/Linux）を <see cref="IBackend"/> インターフェース経由で切り替える。
/// </summary>
public static partial class OS
{
    /// <summary>
    /// プラットフォーム固有の処理を定義するバックエンドインターフェース。
    /// </summary>
    public interface IBackend
    {
        /// <summary>
        /// Avaloniaアプリケーションビルダーにプラットフォーム固有の設定を適用する。
        /// </summary>
        void SetupApp(AppBuilder builder);

        /// <summary>
        /// ウィンドウにプラットフォーム固有の設定（タイトルバー、フレーム等）を適用する。
        /// </summary>
        void SetupWindow(Window window);

        /// <summary>
        /// アプリケーションデータディレクトリのパスを取得する。
        /// </summary>
        string GetDataDir();

        /// <summary>
        /// システム上のgit実行ファイルのパスを検索して返す。
        /// </summary>
        string FindGitExecutable();

        /// <summary>
        /// 指定されたシェル/ターミナルの実行ファイルパスを検索して返す。
        /// </summary>
        string FindTerminal(Models.ShellOrTerminal shell);

        /// <summary>
        /// システムにインストールされている外部ツール（エディタ等）を検出して一覧を返す。
        /// </summary>
        List<Models.ExternalTool> FindExternalTools();

        /// <summary>
        /// 外部マージ/diffツールの実行ファイルをシステムから検索する。
        /// レジストリ、Program Files、PATH等のプラットフォーム固有の方法で探索する。
        /// </summary>
        /// <param name="patterns">検索する実行ファイル名のパターン配列</param>
        /// <returns>見つかった実行ファイルのフルパス。見つからない場合はnull。</returns>
        string FindExternalMergerExecFile(string[] patterns);

        /// <summary>
        /// 指定された作業ディレクトリでターミナルを開く。
        /// </summary>
        void OpenTerminal(string workdir, string args);

        /// <summary>
        /// ファイルマネージャーで指定パスを開く。
        /// </summary>
        void OpenInFileManager(string path);

        /// <summary>
        /// デフォルトブラウザで指定URLを開く。
        /// </summary>
        void OpenBrowser(string url);

        /// <summary>
        /// デフォルトエディタで指定ファイルを開く。
        /// </summary>
        void OpenWithDefaultEditor(string file);
    }

    /// <summary>
    /// アプリケーションデータの保存先ディレクトリパス。
    /// </summary>
    public static string DataDir
    {
        get;
        private set;
    } = string.Empty;

    /// <summary>
    /// gitの実行ファイルパス。変更時にバージョン情報を自動更新する。
    /// </summary>
    public static string GitExecutable
    {
        get => _gitExecutable;
        set
        {
            if (_gitExecutable != value)
            {
                _gitExecutable = value;
                // gitパスが変わったらバージョン情報を再取得する
                UpdateGitVersion();
            }
        }
    }

    /// <summary>
    /// gitのバージョン文字列（例: "2.44.0"）。
    /// </summary>
    public static string GitVersionString
    {
        get;
        private set;
    } = string.Empty;

    /// <summary>
    /// gitのバージョンを <see cref="Version"/> オブジェクトとして保持する。
    /// </summary>
    public static Version GitVersion
    {
        get;
        private set;
    } = new Version(0, 0, 0);

    /// <summary>
    /// git credential helperの名前（デフォルト: "manager"）。
    /// </summary>
    public static string CredentialHelper
    {
        get;
        set;
    } = "manager";

    /// <summary>
    /// 使用するシェルまたはターミナルの実行ファイルパス。
    /// </summary>
    public static string ShellOrTerminal
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// シェル/ターミナル起動時の追加引数。
    /// </summary>
    public static string ShellOrTerminalArgs
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// 検出された外部ツール（エディタ等）の一覧。
    /// </summary>
    public static List<Models.ExternalTool> ExternalTools
    {
        get;
        set;
    } = [];

    /// <summary>
    /// 外部マージツールの種類インデックス（0から）。
    /// </summary>
    public static int ExternalMergerType
    {
        get;
        set;
    } = 0;

    /// <summary>
    /// 外部マージツールの実行ファイルパス。
    /// </summary>
    public static string ExternalMergerExecFile
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// 外部マージツールに渡すマージ用引数。
    /// </summary>
    public static string ExternalMergeArgs
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// 外部マージツールに渡すdiff用引数。
    /// </summary>
    public static string ExternalDiffArgs
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// システムウィンドウフレームを使用するかどうか（Linux専用）。
    /// </summary>
    public static bool UseSystemWindowFrame
    {
        get => OperatingSystem.IsLinux() && _enableSystemWindowFrame;
        set => _enableSystemWindowFrame = value;
    }

    /// <summary>
    /// 静的コンストラクタ。実行中のOSに応じて適切なバックエンド実装を選択する。
    /// </summary>
    static OS()
    {
        // 現在のOSを判定してバックエンドを初期化する
        if (OperatingSystem.IsWindows())
            _backend = new Windows();
        else if (OperatingSystem.IsMacOS())
            _backend = new MacOS();
        else if (OperatingSystem.IsLinux())
            _backend = new Linux();
        else
            throw new PlatformNotSupportedException();
    }

    /// <summary>
    /// アプリケーションデータディレクトリを初期化する。存在しなければ作成する。
    /// </summary>
    public static void SetupDataDir()
    {
        // バックエンドからデータディレクトリパスを取得する
        DataDir = _backend.GetDataDir();
        // ディレクトリが存在しない場合は作成する
        if (!Directory.Exists(DataDir))
            Directory.CreateDirectory(DataDir);
    }

    /// <summary>
    /// Avaloniaアプリケーションビルダーにプラットフォーム固有の設定を適用する。
    /// </summary>
    public static void SetupApp(AppBuilder builder)
    {
        _backend.SetupApp(builder);
    }

    /// <summary>
    /// システムにインストールされている外部ツールを検出して設定する。
    /// </summary>
    public static void SetupExternalTools()
    {
        ExternalTools = _backend.FindExternalTools();
    }

    /// <summary>
    /// ウィンドウにプラットフォーム固有の設定を適用する。
    /// </summary>
    public static void SetupForWindow(Window window)
    {
        _backend.SetupWindow(window);
    }

    /// <summary>
    /// システム上のgit実行ファイルを検索して返す。
    /// </summary>
    public static string FindGitExecutable()
    {
        return _backend.FindGitExecutable();
    }

    /// <summary>
    /// 指定されたシェル/ターミナルが利用可能かテストする。
    /// </summary>
    public static bool TestShellOrTerminal(Models.ShellOrTerminal shell)
    {
        return !string.IsNullOrEmpty(_backend.FindTerminal(shell));
    }

    /// <summary>
    /// 使用するシェル/ターミナルを設定する。
    /// </summary>
    public static void SetShellOrTerminal(Models.ShellOrTerminal shell)
    {
        // シェルの実行ファイルパスを検索して設定する
        ShellOrTerminal = shell is not null ? _backend.FindTerminal(shell) : string.Empty;
        ShellOrTerminalArgs = shell.Args;
    }

    /// <summary>
    /// 外部diffまたはマージツールの設定情報を取得する。
    /// </summary>
    /// <param name="onlyDiff">trueの場合はdiff用引数を、falseの場合はマージ用引数を使用する。</param>
    /// <returns>ツール情報。設定が無効な場合はnull。</returns>
    public static Models.DiffMergeTool GetDiffMergeTool(bool onlyDiff)
    {
        // マージツールタイプが範囲外の場合はnullを返す
        if (ExternalMergerType < 0 || ExternalMergerType >= Models.ExternalMerger.Supported.Count)
            return null;

        // カスタムツール（タイプ0以外）の場合、実行ファイルの存在を確認する
        if (ExternalMergerType != 0 && (string.IsNullOrEmpty(ExternalMergerExecFile) || !File.Exists(ExternalMergerExecFile)))
            return null;

        return new Models.DiffMergeTool(ExternalMergerExecFile, onlyDiff ? ExternalDiffArgs : ExternalMergeArgs);
    }

    /// <summary>
    /// 選択中のマージツールタイプに基づいて実行ファイルパスを自動選択する。
    /// </summary>
    public static void AutoSelectExternalMergeToolExecFile()
    {
        if (ExternalMergerType >= 0 && ExternalMergerType < Models.ExternalMerger.Supported.Count)
        {
            // サポートされているマージツール一覧から選択されたツールを取得する
            var merger = Models.ExternalMerger.Supported[ExternalMergerType];
            // 検出済み外部ツールから一致するものを探す
            var externalTool = ExternalTools.Find(x => x.Name.Equals(merger.Name, StringComparison.Ordinal));
            if (externalTool is not null)
            {
                ExternalMergerExecFile = externalTool.ExecFile;
            }
            else if (!string.IsNullOrEmpty(merger.Finder))
            {
                // プラットフォーム固有の方法で実行ファイルを検索する
                var patterns = merger.GetPatternsToFindExecFile();
                var found = _backend.FindExternalMergerExecFile(patterns);
                ExternalMergerExecFile = found ?? string.Empty;
            }
            else
            {
                ExternalMergerExecFile = string.Empty;
            }

            // diff用とマージ用のコマンド引数を設定する
            ExternalDiffArgs = merger.DiffCmd;
            ExternalMergeArgs = merger.MergeCmd;
        }
        else
        {
            // 範囲外の場合は全てクリアする
            ExternalMergerExecFile = string.Empty;
            ExternalDiffArgs = string.Empty;
            ExternalMergeArgs = string.Empty;
        }
    }

    /// <summary>
    /// ファイルマネージャーで指定パスを開く。
    /// </summary>
    public static void OpenInFileManager(string path)
    {
        _backend.OpenInFileManager(path);
    }

    /// <summary>
    /// デフォルトブラウザで指定URLを開く。
    /// </summary>
    public static void OpenBrowser(string url)
    {
        _backend.OpenBrowser(url);
    }

    /// <summary>
    /// 指定された作業ディレクトリでターミナルを開く。
    /// ターミナルが未設定の場合はエラーを表示する。
    /// </summary>
    public static void OpenTerminal(string workdir)
    {
        // ターミナルが設定されていない場合はエラーを通知する
        if (string.IsNullOrEmpty(ShellOrTerminal))
            App.RaiseException(workdir, App.Text("Error.TerminalNotSpecified"));
        else
            _backend.OpenTerminal(workdir, ShellOrTerminalArgs);
    }

    /// <summary>
    /// デフォルトエディタで指定ファイルを開く。
    /// </summary>
    public static void OpenWithDefaultEditor(string file)
    {
        _backend.OpenWithDefaultEditor(file);
    }

    /// <summary>
    /// ルートパスとサブパスを結合して絶対パスを返す。
    /// Windows環境ではスラッシュをバックスラッシュに変換する。
    /// </summary>
    public static string GetAbsPath(string root, string sub)
    {
        var fullpath = Path.Combine(root, sub);
        // Windowsの場合はパス区切り文字を変換する
        if (OperatingSystem.IsWindows())
            return fullpath.Replace('/', '\\');

        return fullpath;
    }

    /// <summary>
    /// パスのホームディレクトリ部分を "~" に置換した相対パスを返す。
    /// Windows環境ではそのまま返す。
    /// </summary>
    public static string GetRelativePathToHome(string path)
    {
        // Windowsではチルダ展開しない
        if (OperatingSystem.IsWindows())
            return path;

        // ホームディレクトリのプレフィックスを "~" に置換する
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var prefixLen = home.EndsWith('/') ? home.Length - 1 : home.Length;
        if (path.StartsWith(home, StringComparison.Ordinal))
            return $"~{path.AsSpan(prefixLen)}";

        return path;
    }

    /// <summary>
    /// git実行ファイルからバージョン情報を取得して更新する。
    /// </summary>
    private static void UpdateGitVersion()
    {
        // gitパスが未設定または存在しない場合はリセットする
        if (string.IsNullOrEmpty(_gitExecutable) || !File.Exists(_gitExecutable))
        {
            GitVersionString = string.Empty;
            GitVersion = new Version(0, 0, 0);
            return;
        }

        // git --version コマンドを実行してバージョン文字列を取得する
        var start = new ProcessStartInfo();
        start.FileName = _gitExecutable;
        start.Arguments = "--version";
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.StandardOutputEncoding = Encoding.UTF8;
        start.StandardErrorEncoding = Encoding.UTF8;

        try
        {
            using var proc = Process.Start(start)!;
            var rs = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(rs))
            {
                GitVersionString = rs.Trim();

                // 正規表現でメジャー・マイナー・ビルド番号を抽出する
                var match = REG_GIT_VERSION().Match(GitVersionString);
                if (match.Success)
                {
                    var major = int.Parse(match.Groups[1].Value);
                    var minor = int.Parse(match.Groups[2].Value);
                    var build = int.Parse(match.Groups[3].Value);
                    GitVersion = new Version(major, minor, build);
                    GitVersionString = GitVersionString[11..].Trim();
                }
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    /// <summary>
    /// gitバージョン文字列からバージョン番号を抽出する正規表現。
    /// </summary>
    [GeneratedRegex(@"^git version[\s\w]*(\d+)\.(\d+)[\.\-](\d+).*$")]
    private static partial Regex REG_GIT_VERSION();

    /// <summary>プラットフォーム固有のバックエンド実装。</summary>
    private static IBackend _backend = null;
    /// <summary>gitの実行ファイルパス。</summary>
    private static string _gitExecutable = string.Empty;
    /// <summary>システムウィンドウフレームを有効にするかどうか。</summary>
    private static bool _enableSystemWindowFrame = false;
}
