using System;

namespace Komorebi.Models;

/// <summary>
/// 数値を考慮した自然順ソートを提供する静的クラス。
/// 文字列中の数値部分を数値として比較する（例: "file2" &lt; "file10"）。
/// </summary>
public static class NumericSort
{
    /// <summary>
    /// 2つの文字列を数値を考慮して比較する。
    /// </summary>
    /// <param name="s1">比較する最初の文字列。</param>
    /// <param name="s2">比較する2番目の文字列。</param>
    /// <returns>s1がs2より小さい場合は負の値、等しい場合は0、大きい場合は正の値。</returns>
    public static int Compare(string s1, string s2)
    {
        // 同一参照チェック
        if (ReferenceEquals(s1, s2))
            return 0;
        if (s1 is null)
            return -1;
        if (s2 is null)
            return 1;

        int len1 = s1.Length;
        int len2 = s2.Length;

        int marker1 = 0;
        int marker2 = 0;

        // 文字列を走査し、数字/非数字のチャンクごとに比較
        while (marker1 < len1 && marker2 < len2)
        {
            char c1 = s1[marker1];
            char c2 = s2[marker2];

            // 数字と非数字が混在する場合は文字として比較
            bool isDigit1 = char.IsAsciiDigit(c1);
            bool isDigit2 = char.IsAsciiDigit(c2);
            if (isDigit1 != isDigit2)
                // upstream e66cccaf: 1 文字の比較でも文字列を確保せず span で比較する。
                return s1.AsSpan(marker1, 1).CompareTo(s2.AsSpan(marker2, 1), StringComparison.InvariantCultureIgnoreCase);

            // 同じ種類（数字/非数字）の連続文字チャンクの長さを算出
            int subLen1 = 1;
            while (marker1 + subLen1 < len1 && char.IsAsciiDigit(s1[marker1 + subLen1]) == isDigit1)
                subLen1++;

            int subLen2 = 1;
            while (marker2 + subLen2 < len2 && char.IsAsciiDigit(s2[marker2 + subLen2]) == isDigit2)
                subLen2++;

            // 数字チャンクは桁数が異なれば桁数で大小が決まる（従来どおり）。
            if (isDigit1 && subLen1 != subLen2)
                return subLen1 - subLen2;

            // upstream e66cccaf: Substring の代わりに span で比較し、ソート時の文字列確保をなくす。
            var sub1 = s1.AsSpan(marker1, subLen1);
            var sub2 = s2.AsSpan(marker2, subLen2);

            marker1 += subLen1;
            marker2 += subLen2;

            int result;
            if (isDigit1)
                // 同桁数の数字チャンクは序数（辞書順）で比較
                result = sub1.SequenceCompareTo(sub2);
            else
                // 非数字チャンクは大文字小文字無視で比較
                result = sub1.CompareTo(sub2, StringComparison.InvariantCultureIgnoreCase);

            if (result != 0)
                return result;
        }

        // 残りの長さで比較
        return len1 - len2;
    }
}
