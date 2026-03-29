using System;
using System.Collections.Generic;

using Avalonia.Media;

namespace Komorebi.Models;

/// <summary>
/// システムおよびバンドルされたフォントの一覧を管理する静的クラス
/// </summary>
public static class InstalledFont
{
    /// <summary>
    /// 全フォントファミリ名の一覧（システム + バンドル）
    /// </summary>
    public static List<string> All { get; }

    /// <summary>
    /// 等幅フォントファミリ名の一覧（システム + バンドル）
    /// </summary>
    public static List<string> Monospace { get; }

    /// <summary>
    /// src/Resources/Fonts/に含まれるバンドルフォントファミリ
    /// </summary>
    private static readonly HashSet<string> s_bundledAll = new(StringComparer.Ordinal)
    {
        "IBM Plex Sans JP",
        "JetBrains Mono",
        "M PLUS 2",
        "Moralerspace Neon JPDOC",
        "Murecho",
        "Noto Sans JP",
        "PlemolJP",
        "UDEV Gothic JPDOC",
        "Zen Kaku Gothic New",
    };

    /// <summary>
    /// バンドルされた等幅フォントファミリ
    /// </summary>
    private static readonly HashSet<string> s_bundledMono = new(StringComparer.Ordinal)
    {
        "JetBrains Mono",
        "PlemolJP",
        "UDEV Gothic JPDOC",
    };

    static InstalledFont()
    {
        var allNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var monoNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var font in FontManager.Current.SystemFonts)
        {
            allNames.Add(font.Name);

            if (FontManager.Current.TryGetGlyphTypeface(
                    new Typeface(font), out var glyph) && glyph.Metrics.IsFixedPitch)
                monoNames.Add(font.Name);
        }

        foreach (var name in s_bundledAll)
            allNames.Add(name);

        foreach (var name in s_bundledMono)
            monoNames.Add(name);

        All = new List<string>(allNames);
        Monospace = new List<string>(monoNames);
    }
}
