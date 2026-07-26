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

    /// <summary>
    /// <see cref="All"/> の名前引き用インデックス。候補の存在確認を O(1) にする。
    /// </summary>
    private static readonly Lazy<HashSet<string>> s_allSet =
        new(() => new HashSet<string>(All, StringComparer.OrdinalIgnoreCase));

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

    /// <summary>
    /// ロケールに応じたデフォルトUIフォントを「単一の」フォント名として解決する。
    /// <see cref="GetLocaleDefaults"/> の候補リストの中で、システムに実際に
    /// インストールされている最初のフォント名を返す。一致するものがなければ
    /// 候補リスト先頭の名前をそのまま返す（Avalonia 側のフォントフォールバックに委ねる）。
    /// 設定画面の <c>ComboBox</c> は単一フォント名を <c>SelectedItem</c> として扱うため、
    /// 初期値もカンマ区切り文字列ではなく単一名でなければ未選択表示になる。
    /// </summary>
    public static string ResolveDefaultFont(string locale)
        => PickFirstMatching(GetLocaleDefaults(locale).Default, IsInstalled);

    /// <summary>
    /// ロケールに応じた等幅フォントを「単一の」フォント名として解決する。
    /// 仕様は <see cref="ResolveDefaultFont"/> と同様だが、判定対象は候補名だけに限定する。
    ///
    /// 以前は <see cref="Monospace"/>（= 全インストールフォントの GlyphTypeface を生成して
    /// IsFixedPitch を調べたリスト）と突き合わせていた。この解決処理は Preferences の
    /// フィールド初期化子から起動のたびに走るため、毎回システム内の全フォントファイルを
    /// DirectWrite 経由で開いて読み込むことになり、起動コストが大きいうえに
    /// Skia のフォントストリーム読み取り（SkDWriteFontFileStream::read）を数百回叩いていた。
    /// 判定を候補フォントだけに絞ることで、結果を変えずに読み取り回数を数件まで落とす。
    /// </summary>
    public static string ResolveMonospaceFont(string locale)
        => PickFirstMatching(GetLocaleDefaults(locale).Monospace, IsInstalledMonospace);

    /// <summary>指定名がシステムにインストールされているかを判定する。</summary>
    private static bool IsInstalled(string name)
        => s_allSet.Value.Contains(name);

    /// <summary>指定名がインストール済みで、かつ等幅フォントかを判定する。</summary>
    private static bool IsInstalledMonospace(string name)
    {
        if (!IsInstalled(name))
            return false;

        try
        {
            return FontManager.Current.TryGetGlyphTypeface(new Typeface(name), out var glyph) &&
                   glyph.Metrics.IsFixedPitch;
        }
        catch
        {
            // FontManager が初期化されていない環境（テスト等）では未一致として扱う
            return false;
        }
    }

    private static string PickFirstMatching(string candidates, Func<string, bool> isMatch)
    {
        if (string.IsNullOrEmpty(candidates))
            return string.Empty;

        var parts = candidates.Split(',');

        // インストール済みフォント一覧が空 = FontManager 未初期化とみなし、判定自体を行わない。
        if (s_allSet.Value.Count > 0)
        {
            foreach (var part in parts)
            {
                var name = part.Trim();
                if (name.Length > 0 && isMatch(name))
                    return name;
            }
        }

        // インストール済みフォント一覧が取得できない（FontManager 未初期化等）か、
        // 候補が一つもインストールされていない場合は、先頭候補をそのまま返して
        // Avalonia のフォントフォールバックに任せる。
        foreach (var part in parts)
        {
            var name = part.Trim();
            if (name.Length > 0)
                return name;
        }

        return string.Empty;
    }
}
