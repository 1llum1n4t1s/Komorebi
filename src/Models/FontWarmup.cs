using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;

using Avalonia.Media;

namespace Komorebi.Models;

/// <summary>
/// 起動直後にフォールバックフォントの GlyphTypeface 生成をまとめて済ませるウォームアップ。
///
/// SkiaSharp 3.x には、GlyphTypeface 生成中の DirectWrite フォントストリーム読み取り
/// (<c>SkDWriteFontFileStream::read</c>) と GC ファイナライザが競合し、native 側で
/// use-after-free (0xc0000005) を起こす不具合クラスがある（SkiaSharp 4 のライフサイクル
/// 再設計で修正済みだが、Avalonia 12.x は 3.x 系依存のため差し替えできない）。
/// 生成済みの GlyphTypeface は Avalonia の FontManager 側にキャッシュされるため、
/// リポジトリ復元による大量アロケーション（= GC 多発）が始まる前の静かなタイミングで
/// 主要フォントと代表的なフォールバック先を一括生成し、危険経路が走る回数そのものを減らす。
/// さらに可能なら <see cref="GC.TryStartNoGCRegion(long)"/> でウォームアップ中の GC を止め、
/// ウォームアップ自身が競合を踏む確率も抑える。
/// </summary>
public static class FontWarmup
{
    /// <summary>
    /// フォールバック解決を誘発する代表コードポイント。コミットログ・diff で現実的に
    /// 出現しやすい文字種を script ごとに 1 つずつ選ぶ。同一フォントに解決されるものは
    /// ファミリ名で重複排除されるため、多めに並べても読み取りコストは増えない。
    /// </summary>
    private static readonly int[] s_probeCodepoints =
    [
        'A', // Latin（基本フォント）
        0x3042, // あ: ひらがな
        0x6F22, // 漢: CJK 統合漢字
        0xFF76, // ｶ: 半角カナ
        0xAC00, // 가: ハングル
        0x0416, // Ж: キリル
        0x03A9, // Ω: ギリシャ
        0x2192, // →: 矢印
        0x2502, // │: 罫線素片
        0x2605, // ★: 記号
        0x26A0, // ⚠: 警告記号
        0x2705, // ✅: BMP 絵文字
        0x1F600, // 😀: SMP 絵文字（カラー絵文字フォント）
    ];

    /// <summary>
    /// 指定ロケールの既定フォント候補・追加ファミリと、代表コードポイントのフォールバック先を
    /// まとめて GlyphTypeface 化する。失敗しても起動を妨げない（記録のみで継続する）。
    /// </summary>
    /// <param name="locale">現在のロケール（例: "ja_JP"）</param>
    /// <param name="preferredFamilies">設定済みフォントなど優先的に温めるファミリ名</param>
    public static void Run(string locale, params string?[] preferredFamilies)
    {
        try
        {
            var watch = Stopwatch.StartNew();
            var warmed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inNoGCRegion = TryStartNoGCRegion();
            try
            {
                WarmFamilies(locale, preferredFamilies, warmed);
                WarmFallbacks(warmed);
            }
            finally
            {
                EndNoGCRegion(inNoGCRegion);
            }

            Logger.Log($"FontWarmup: {warmed.Count} families, {watch.ElapsedMilliseconds}ms, NoGCRegion={inNoGCRegion}");
        }
        catch (Exception ex)
        {
            // ウォームアップは確率低減の最適化であり必須処理ではない。失敗しても起動は続ける。
            Logger.Log($"FontWarmup failed: {ex.Message}", LogLevel.Warning);
        }
    }

    /// <summary>
    /// 設定済みフォントとロケール既定チェーン（UI・等幅）の GlyphTypeface を生成する。
    /// </summary>
    private static void WarmFamilies(string locale, string?[] preferredFamilies, HashSet<string> warmed)
    {
        var (defaults, monospaces) = InstalledFont.GetLocaleDefaults(locale);
        var candidates = new List<string?>(preferredFamilies);
        candidates.AddRange(defaults.Split(','));
        candidates.AddRange(monospaces.Split(','));

        foreach (var candidate in candidates)
        {
            var name = candidate?.Trim();
            if (string.IsNullOrEmpty(name) || !warmed.Add(name))
                continue;

            try
            {
                FontManager.Current.TryGetGlyphTypeface(new Typeface(name), out _);
            }
            catch
            {
                // 個別フォントの失敗（未インストール等）は他のウォームアップを止めない
            }
        }
    }

    /// <summary>
    /// 代表コードポイントごとにフォールバック先フォントを解決し、GlyphTypeface を生成する。
    /// </summary>
    private static void WarmFallbacks(HashSet<string> warmed)
    {
        var culture = CultureInfo.CurrentUICulture;
        foreach (var codepoint in s_probeCodepoints)
        {
            try
            {
                if (FontManager.Current.TryMatchCharacter(
                        codepoint, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal,
                        null, culture, out var typeface) &&
                    warmed.Add(typeface.FontFamily.Name))
                {
                    FontManager.Current.TryGetGlyphTypeface(typeface, out _);
                }
            }
            catch
            {
                // 個別コードポイントの失敗は他のウォームアップを止めない
            }
        }
    }

    /// <summary>
    /// ウォームアップ用の NoGC 区間を開始する。予算はフォントファイル読み取りで発生する
    /// managed コピー（CJK フォントは 1 つで数 MB〜十数 MB）を見込んだ値から段階的に縮めて
    /// 確保を試み、確保できない環境では false を返して通常実行にフォールバックする。
    /// </summary>
    private static bool TryStartNoGCRegion()
    {
        long[] budgets = [192 * 1024 * 1024, 96 * 1024 * 1024, 32 * 1024 * 1024];
        foreach (var budget in budgets)
        {
            try
            {
                if (GC.TryStartNoGCRegion(budget))
                    return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                // この環境の GC 構成では予算が大きすぎる → 縮めて再試行
            }
            catch (InvalidOperationException)
            {
                // 既に NoGC 区間内 → 自前の区間は張らずそのまま実行する
                break;
            }
        }

        return false;
    }

    /// <summary>NoGC 区間を終了する。予算超過で既に区間が終わっていた場合は何もしない。</summary>
    private static void EndNoGCRegion(bool started)
    {
        if (!started)
            return;

        try
        {
            if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
                GC.EndNoGCRegion();
        }
        catch (InvalidOperationException)
        {
            // 割り当てが予算を超えて区間が自動終了していた場合は何もしない
        }
    }
}
