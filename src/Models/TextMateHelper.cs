using System;
using System.Collections.Generic;
using System.IO;

using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;

using AvaloniaEdit;
using AvaloniaEdit.TextMate;

using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace Komorebi.Models;

/// <summary>
///     TextMateの文法定義ユーティリティ。
///     標準のTextMateSharpでサポートされない追加言語のスコープと文法を提供する。
/// </summary>
public static class GrammarUtility
{
    /// <summary>追加でサポートする文法定義の一覧（TOML, Kotlin, Haxe, JSP, Vue等）</summary>
    private static readonly ExtraGrammar[] s_extraGrammars =
    [
        new ExtraGrammar("source.toml", [".toml"], "toml.json"),
        new ExtraGrammar("source.kotlin", [".kotlin", ".kt", ".kts"], "kotlin.json"),
        new ExtraGrammar("source.hx", [".hx"], "haxe.json"),
        new ExtraGrammar("source.hxml", [".hxml"], "hxml.json"),
        new ExtraGrammar("text.html.jsp", [".jsp", ".jspf", ".tag"], "jsp.json"),
        new ExtraGrammar("source.vue", [".vue"], "vue.json"),
    ];

    /// <summary>
    ///     ファイル名からTextMateのスコープ名を取得する。
    ///     追加文法を優先的にチェックし、見つからない場合は標準のレジストリにフォールバックする。
    /// </summary>
    /// <param name="file">ファイル名（拡張子から判定）</param>
    /// <param name="reg">標準のレジストリオプション</param>
    /// <returns>TextMateのスコープ名</returns>
    public static string GetScope(string file, RegistryOptions reg)
    {
        var extension = Path.GetExtension(file);
        // 特定の拡張子を既知の言語にマッピング
        if (extension == ".h")
            extension = ".cpp";
        else if (extension is ".resx" or ".plist" or ".manifest")
            extension = ".xml";
        else if (extension == ".command")
            extension = ".sh";

        foreach (var grammar in s_extraGrammars)
        {
            foreach (var ext in grammar.Extensions)
            {
                if (ext.Equals(extension, StringComparison.OrdinalIgnoreCase))
                    return grammar.Scope;
            }
        }

        return reg.GetScopeByExtension(extension);
    }

    /// <summary>
    ///     スコープ名から文法定義を取得する。
    ///     追加文法はアプリケーションリソースから読み込む。
    /// </summary>
    /// <param name="scopeName">TextMateのスコープ名</param>
    /// <param name="reg">標準のレジストリオプション</param>
    /// <returns>文法定義（IRawGrammar）</returns>
    public static IRawGrammar GetGrammar(string scopeName, RegistryOptions reg)
    {
        foreach (var grammar in s_extraGrammars)
        {
            if (grammar.Scope.Equals(scopeName, StringComparison.OrdinalIgnoreCase))
            {
                var asset = AssetLoader.Open(new Uri($"avares://Komorebi/Resources/Grammars/{grammar.File}",
                    UriKind.RelativeOrAbsolute));

                try
                {
                    using var reader = new StreamReader(asset);
                    return GrammarReader.ReadGrammarSync(reader);
                }
                catch
                {
                    break;
                }
            }
        }

        return reg.GetGrammar(scopeName);
    }

    /// <summary>追加文法定義のレコード型（スコープ名、対応拡張子リスト、文法ファイル名）</summary>
    private record ExtraGrammar(string Scope, List<string> Extensions, string File)
    {
        /// <summary>TextMateのスコープ名</summary>
        public readonly string Scope = Scope;
        /// <summary>対応するファイル拡張子のリスト</summary>
        public readonly List<string> Extensions = Extensions;
        /// <summary>文法定義のJSONファイル名</summary>
        public readonly string File = File;
    }
}

/// <summary>
///     IRegistryOptionsのラッパークラス。
///     追加文法のサポートと最後に使用したスコープのキャッシュを提供する。
/// </summary>
public class RegistryOptionsWrapper(ThemeName defaultTheme) : IRegistryOptions
{
    /// <summary>最後に設定されたスコープ名（重複設定の回避に使用）</summary>
    public string LastScope { get; set; } = string.Empty;

    public IRawTheme GetTheme(string scopeName) => _backend.GetTheme(scopeName);
    public IRawTheme GetDefaultTheme() => _backend.GetDefaultTheme();
    public IRawTheme LoadTheme(ThemeName name) => _backend.LoadTheme(name);
    public ICollection<string> GetInjections(string scopeName) => _backend.GetInjections(scopeName);
    public IRawGrammar GetGrammar(string scopeName) => GrammarUtility.GetGrammar(scopeName, _backend);
    public string GetScope(string filename) => GrammarUtility.GetScope(filename, _backend);

    /// <summary>標準のTextMateSharpレジストリオプション</summary>
    private readonly RegistryOptions _backend = new(defaultTheme);
}

/// <summary>
///     TextMateシンタックスハイライトのヘルパークラス。
///     エディターへのインストール、テーマ切替、ファイル種別に応じた文法設定を行う。
/// </summary>
public static class TextMateHelper
{
    /// <summary>
    ///     エディターにTextMateハイライトをインストールする。
    ///     現在のアプリケーションテーマに応じてDarkPlus/LightPlusテーマを選択する。
    /// </summary>
    /// <param name="editor">対象のテキストエディター</param>
    /// <returns>TextMateインストレーションインスタンス</returns>
    public static TextMate.Installation CreateForEditor(TextEditor editor)
    {
        return editor.InstallTextMate(Application.Current?.ActualThemeVariant == ThemeVariant.Dark ?
            new RegistryOptionsWrapper(ThemeName.DarkPlus) :
            new RegistryOptionsWrapper(ThemeName.LightPlus));
    }

    /// <summary>
    ///     アプリケーションの現在のテーマに合わせてTextMateテーマを切り替える
    /// </summary>
    /// <param name="installation">TextMateインストレーション</param>
    public static void SetThemeByApp(TextMate.Installation installation)
    {
        if (installation is { RegistryOptions: RegistryOptionsWrapper reg })
        {
            var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
            installation.SetTheme(reg.LoadTheme(isDark ? ThemeName.DarkPlus : ThemeName.LightPlus));
        }
    }

    /// <summary>
    ///     ファイル名に基づいてTextMateの文法を設定する。
    ///     スコープが変更された場合のみ再設定し、GCを実行する。
    /// </summary>
    /// <param name="installation">TextMateインストレーション</param>
    /// <param name="filePath">ファイルパス（拡張子から文法を判定）</param>
    public static void SetGrammarByFileName(TextMate.Installation installation, string filePath)
    {
        if (installation is { RegistryOptions: RegistryOptionsWrapper reg } && !string.IsNullOrEmpty(filePath))
        {
            var scope = reg.GetScope(filePath);
            if (reg.LastScope != scope)
            {
                reg.LastScope = scope;
                installation.SetGrammar(reg.GetScope(filePath));
                GC.Collect();
            }
        }
    }
}
