using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Komorebi.Models;

/// <summary>
/// 外部エディタ/IDEツールを表すクラス。リポジトリを外部ツールで開く機能を提供する。
/// </summary>
public class ExternalTool
{
    /// <summary>
    /// 外部ツールの起動オプション（ワークスペースファイル選択など）
    /// </summary>
    public class LaunchOption
    {
        /// <summary>オプションの表示タイトル</summary>
        public string Title { get; set; }
        /// <summary>起動時のコマンドライン引数</summary>
        public string Args { get; set; }

        /// <summary>
        /// 起動オプションを初期化する
        /// </summary>
        /// <param name="title">表示タイトル</param>
        /// <param name="args">コマンドライン引数</param>
        public LaunchOption(string title, string args)
        {
            Title = title;
            Args = args;
        }
    }

    /// <summary>ツールの表示名</summary>
    public string Name { get; }
    /// <summary>実行ファイルのパス</summary>
    public string ExecFile { get; }
    /// <summary>ツールのアイコン画像</summary>
    public Bitmap IconImage { get; }

    /// <summary>
    /// 外部ツールのコンストラクタ
    /// </summary>
    /// <param name="name">表示名</param>
    /// <param name="icon">アイコンリソース名</param>
    /// <param name="execFile">実行ファイルパス</param>
    /// <param name="optionsGenerator">起動オプション生成関数（省略可）</param>
    public ExternalTool(string name, string icon, string execFile, Func<string, List<LaunchOption>> optionsGenerator = null)
    {
        Name = name;
        ExecFile = execFile;

        _optionsGenerator = optionsGenerator;

        try
        {
            var asset = AssetLoader.Open(new Uri($"avares://Komorebi/Resources/Images/ExternalToolIcons/{icon}.png",
                UriKind.RelativeOrAbsolute));
            IconImage = new Bitmap(asset);
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// 指定リポジトリに対する起動オプションのリストを生成する
    /// </summary>
    /// <param name="repo">リポジトリパス</param>
    /// <returns>起動オプションのリスト（ない場合はnull）</returns>
    public List<LaunchOption> MakeLaunchOptions(string repo)
    {
        return _optionsGenerator?.Invoke(repo);
    }

    /// <summary>
    /// 外部ツールを指定した引数で起動する
    /// </summary>
    /// <param name="args">コマンドライン引数</param>
    public void Launch(string args)
    {
        if (File.Exists(ExecFile))
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = ExecFile,
                Arguments = args,
                UseShellExecute = false,
            })?.Dispose();
        }
    }

    /// <summary>起動オプション生成関数</summary>
    private Func<string, List<LaunchOption>> _optionsGenerator = null;
}

/// <summary>
/// Visual Studioのインストール済みインスタンス情報
/// </summary>
public class VisualStudioInstance
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("productPath")]
    public string ProductPath { get; set; } = string.Empty;

    [JsonPropertyName("isPrerelease")]
    public bool IsPrerelease { get; set; } = false;
}

/// <summary>
/// JetBrains Toolboxの状態情報（state.jsonのデシリアライズ用）
/// </summary>
public class JetBrainsState
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 0;
    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = string.Empty;
    [JsonPropertyName("tools")]
    public List<JetBrainsTool> Tools { get; set; } = [];
}

/// <summary>
/// JetBrains Toolboxの個別ツール情報
/// </summary>
public class JetBrainsTool
{
    /// <summary>チャンネルID</summary>
    [JsonPropertyName("channelId")]
    public string ChannelId { get; set; }
    /// <summary>ツールID（例: intellij, rider等）</summary>
    [JsonPropertyName("toolId")]
    public string ToolId { get; set; }
    /// <summary>製品コード（例: RD, PS等）</summary>
    [JsonPropertyName("productCode")]
    public string ProductCode { get; set; }
    /// <summary>リリースチャンネルタグ</summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; }
    /// <summary>表示名</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }
    /// <summary>表示バージョン</summary>
    [JsonPropertyName("displayVersion")]
    public string DisplayVersion { get; set; }
    /// <summary>ビルド番号</summary>
    [JsonPropertyName("buildNumber")]
    public string BuildNumber { get; set; }
    /// <summary>インストール先ディレクトリ</summary>
    [JsonPropertyName("installLocation")]
    public string InstallLocation { get; set; }
    /// <summary>起動コマンドファイル名</summary>
    [JsonPropertyName("launchCommand")]
    public string LaunchCommand { get; set; }
}

/// <summary>
/// 外部ツールのカスタマイズ設定（external_editors.jsonから読み込み）
/// </summary>
public class ExternalToolCustomization
{
    /// <summary>ツール名からカスタム実行パスへのマッピング</summary>
    [JsonPropertyName("tools")]
    public Dictionary<string, string> Tools { get; set; } = [];
    /// <summary>検出対象から除外するツール名のリスト</summary>
    [JsonPropertyName("excludes")]
    public List<string> Excludes { get; set; } = [];
}

/// <summary>
/// インストール済みの外部エディタ/IDEを自動検出するクラス。
/// VS Code、JetBrains、Sublime Text等をサポートする。
/// </summary>
public class ExternalToolsFinder
{
    /// <summary>検出された外部ツールのリスト</summary>
    public List<ExternalTool> Tools
    {
        get;
        private set;
    } = [];

    /// <summary>
    /// カスタム設定ファイルを読み込んで初期化する
    /// </summary>
    public ExternalToolsFinder()
    {
        var customPathsConfig = Path.Combine(Native.OS.DataDir, "external_editors.json");
        try
        {
            if (File.Exists(customPathsConfig))
            {
                using var stream = File.OpenRead(customPathsConfig);
                _customization = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.ExternalToolCustomization);
            }
        }
        catch
        {
            // Ignore
        }

        _customization ??= new ExternalToolCustomization();
    }

    /// <summary>
    /// 外部ツールの検出と登録を試みる。除外リストにある場合はスキップ。
    /// </summary>
    /// <param name="name">ツール名</param>
    /// <param name="icon">アイコンリソース名</param>
    /// <param name="finder">実行ファイルパスの検索関数</param>
    /// <param name="optionsGenerator">起動オプション生成関数</param>
    public void TryAdd(string name, string icon, Func<string> finder, Func<string, List<ExternalTool.LaunchOption>> optionsGenerator = null)
    {
        if (_customization.Excludes.Contains(name))
            return;

        if (_customization.Tools.TryGetValue(name, out var customPath) && File.Exists(customPath))
        {
            Tools.Add(new ExternalTool(name, icon, customPath, optionsGenerator));
        }
        else
        {
            var path = finder();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                Tools.Add(new ExternalTool(name, icon, path, optionsGenerator));
        }
    }

    /// <summary>Visual Studio Codeの検出を試みる</summary>
    public void VSCode(Func<string> platformFinder)
    {
        TryAdd("Visual Studio Code", "vscode", platformFinder, GenerateVSCodeLaunchOptions);
    }

    /// <summary>Visual Studio Code Insidersの検出を試みる</summary>
    public void VSCodeInsiders(Func<string> platformFinder)
    {
        TryAdd("Visual Studio Code - Insiders", "vscode_insiders", platformFinder, GenerateVSCodeLaunchOptions);
    }

    /// <summary>VSCodiumの検出を試みる</summary>
    public void VSCodium(Func<string> platformFinder)
    {
        TryAdd("VSCodium", "codium", platformFinder, GenerateVSCodeLaunchOptions);
    }

    /// <summary>Sublime Textの検出を試みる</summary>
    public void SublimeText(Func<string> platformFinder)
    {
        TryAdd("Sublime Text", "sublime_text", platformFinder);
    }

    /// <summary>Zedエディタの検出を試みる</summary>
    public void Zed(Func<string> platformFinder)
    {
        TryAdd("Zed", "zed", platformFinder);
    }

    /// <summary>Cursorエディタの検出を試みる</summary>
    public void Cursor(Func<string> platformFinder)
    {
        TryAdd("Cursor", "cursor", platformFinder);
    }

    /// <summary>
    /// JetBrains Toolboxからインストール済みIDEを検出して登録する
    /// </summary>
    /// <param name="platformFinder">Toolboxのデータディレクトリを返す関数</param>
    public void FindJetBrainsFromToolbox(Func<string> platformFinder)
    {
        var exclude = new List<string> { "fleet", "dotmemory", "dottrace", "resharper-u", "androidstudio" };
        var supportedIcons = new List<string> { "CL", "DB", "DL", "DS", "GO", "JB", "PC", "PS", "PY", "QA", "QD", "RD", "RM", "RR", "WRS", "WS" };
        var state = Path.Combine(platformFinder(), "state.json");
        if (File.Exists(state))
        {
            try
            {
                using var stream = File.OpenRead(state);
                var stateData = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.JetBrainsState);
                foreach (var tool in stateData.Tools)
                {
                    if (exclude.Contains(tool.ToolId.ToLowerInvariant()))
                        continue;

                    Tools.Add(new ExternalTool(
                        $"{tool.DisplayName} {tool.DisplayVersion}",
                        supportedIcons.Contains(tool.ProductCode) ? $"JetBrains/{tool.ProductCode}" : "JetBrains/JB",
                        Path.Combine(tool.InstallLocation, tool.LaunchCommand)));
                }
            }
            catch
            {
                // Ignore exceptions.
            }
        }
    }

    /// <summary>
    /// VS Code系エディタの起動オプションを生成する。
    /// リポジトリ内の.code-workspaceファイルを検索してオプション化する。
    /// </summary>
    /// <param name="path">リポジトリパス</param>
    /// <returns>ワークスペースファイルの起動オプションリスト</returns>
    private List<ExternalTool.LaunchOption> GenerateVSCodeLaunchOptions(string path)
    {
        var root = new DirectoryInfo(path);
        if (!root.Exists)
            return null;

        var options = new List<ExternalTool.LaunchOption>();
        var prefixLen = root.FullName.Length;
        root.WalkFiles(f =>
        {
            if (f.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase))
            {
                var display = f[prefixLen..].TrimStart(Path.DirectorySeparatorChar);
                options.Add(new(display, f.Quoted()));
            }
        }, 2);
        return options;
    }

    /// <summary>カスタム設定（パス上書き・除外リスト）</summary>
    private ExternalToolCustomization _customization = null;
}
