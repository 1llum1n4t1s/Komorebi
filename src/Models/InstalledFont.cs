using System;
using System.Collections.Generic;

using Avalonia.Media;

namespace Komorebi.Models;

/// <summary>
/// システムにインストールされたフォントの一覧を管理する静的クラス。
/// </summary>
public static class InstalledFont
{
    /// <summary>
    /// 全フォントファミリ名の一覧（システムフォント）。
    /// FontManager が利用できない環境（テスト等）では空リストを返す。
    /// </summary>
    public static List<string> All => s_all.Value;

    /// <summary>
    /// 等幅フォントファミリ名の一覧（システムフォント）。
    /// FontManager が利用できない環境（テスト等）では空リストを返す。
    /// </summary>
    public static List<string> Monospace => s_mono.Value;

    private static readonly Lazy<List<string>> s_all = new(LoadAll);
    private static readonly Lazy<List<string>> s_mono = new(LoadMono);

    private static List<string> LoadAll()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var font in FontManager.Current.SystemFonts)
                names.Add(font.Name);
        }
        catch
        {
            // FontManager が初期化されていない環境（テスト等）では空で返す
        }

        return new List<string>(names);
    }

    private static List<string> LoadMono()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var font in FontManager.Current.SystemFonts)
            {
                if (FontManager.Current.TryGetGlyphTypeface(
                        new Typeface(font), out var glyph) && glyph.Metrics.IsFixedPitch)
                    names.Add(font.Name);
            }
        }
        catch
        {
            // FontManager が初期化されていない環境（テスト等）では空で返す
        }

        return new List<string>(names);
    }

    /// <summary>
    /// ロケールごとに推奨されるデフォルトフォントとモノスペースフォントの定義。
    /// フォントをバンドルしないため、各ロケールの代表的なシステムフォントを
    /// 優先度順にカンマ区切りで定義し、最初にインストールされているものを使用する。
    /// </summary>
    public static (string Default, string Monospace) GetLocaleDefaults(string locale)
    {
        return locale switch
        {
            "ja_JP" => ("Hiragino Sans, Yu Gothic UI, Meiryo UI",
                        "Osaka-Mono, BIZ UDGothic, MS Gothic"),
            "zh_CN" => ("Microsoft YaHei, Noto Sans SC, Noto Sans CJK SC",
                        "Cascadia Mono, NSimSun, Noto Sans Mono CJK SC"),
            "zh_TW" => ("Microsoft JhengHei, Noto Sans TC, Noto Sans CJK TC",
                        "Cascadia Mono, MingLiU, Noto Sans Mono CJK TC"),
            "ko_KR" => ("Malgun Gothic, Noto Sans KR, Noto Sans CJK KR",
                        "Cascadia Mono, D2Coding, Noto Sans Mono CJK KR"),
            _ => ("Inter",
                  "Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono"),
        };
    }
}
