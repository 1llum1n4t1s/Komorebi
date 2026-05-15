using System;
using System.Collections.Generic;
using System.Globalization;

namespace Komorebi.Models;

/// <summary>
/// 日時の表示フォーマットを管理するクラス。
/// サポートされる日付形式の一覧と、24時間/12時間表示の切り替え機能を提供する。
/// </summary>
public class DateTimeFormat
{
    /// <summary>
    /// サポートされている日付フォーマットの一覧
    /// </summary>
    public static readonly List<DateTimeFormat> Supported =
    [
        new("yyyy/MM/dd"),
        new("yyyy.MM.dd"),
        new("yyyy-MM-dd"),
        new("MM/dd/yyyy"),
        new("MM.dd.yyyy"),
        new("MM-dd-yyyy"),
        new("dd/MM/yyyy"),
        new("dd.MM.yyyy"),
        new("dd-MM-yyyy"),
        new("MMM d yyyy"),
        new("d MMM yyyy"),
    ];

    /// <summary>
    /// 現在選択されている日付フォーマットのインデックス
    /// </summary>
    public static int ActiveIndex
    {
        get;
        set;
    } = 0;

    /// <summary>
    /// 24時間表示を使用するかどうか（falseの場合はAM/PM表示）
    /// </summary>
    public static bool Use24Hours
    {
        get;
        set;
    } = true;

    /// <summary>
    /// 日付フォーマット文字列（例: "yyyy/MM/dd"）
    /// </summary>
    public string DateFormat
    {
        get;
    }

    /// <summary>
    /// 現在日時をこのフォーマットで表示した例文。
    /// upstream b2aba44c: 区切り文字が culture (例: de_DE) で "." に書き換えられないよう固定 culture を使う。
    /// </summary>
    public string Example
    {
        get => DateTime.Now.ToString(DateFormat, _culture);
    }

    /// <summary>
    /// "/" と ":" を区切り文字として固定するための CultureInfo。
    /// upstream b2aba44c: 既定 culture では DateTime.ToString が "/" を文化圏依存の区切り (".") に置換するため、
    /// CurrentCulture を Clone して区切りだけ固定する。
    /// <para>
    /// /rere P2#18: 起動時の <see cref="CultureInfo.CurrentCulture"/> をスナップショットするため、
    /// ランタイム中のロケール変更 (アプリ内で SetLocale を呼んだ後) には追従しない。
    /// "MMM d yyyy" / "d MMM yyyy" 形式の月名略称は起動時のカルチャに依存する点に注意。
    /// </para>
    /// </summary>
    private static readonly CultureInfo _culture = CreateCulture();

    private static CultureInfo CreateCulture()
    {
        var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        culture.DateTimeFormat.DateSeparator = "/";
        culture.DateTimeFormat.TimeSeparator = ":";
        return culture;
    }

    /// <summary>
    /// 指定した日付フォーマットで初期化する
    /// </summary>
    /// <param name="date">日付フォーマット文字列</param>
    public DateTimeFormat(string date)
    {
        DateFormat = date;
    }

    /// <summary>
    /// Unixタイムスタンプをフォーマットされた日時文字列に変換する
    /// </summary>
    /// <param name="timestamp">Unixタイムスタンプ（秒）</param>
    /// <param name="dateOnly">日付のみ表示する場合はtrue</param>
    /// <returns>フォーマット済みの日時文字列</returns>
    public static string Format(ulong timestamp, bool dateOnly = false)
    {
        // Unixエポックからの秒数をローカル時刻に変換
        var localTime = DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime();
        return Format(localTime, dateOnly);
    }

    /// <summary>
    /// DateTime値をフォーマットされた日時文字列に変換する
    /// </summary>
    /// <param name="localTime">ローカル日時</param>
    /// <param name="dateOnly">日付のみ表示する場合はtrue</param>
    /// <returns>フォーマット済みの日時文字列</returns>
    public static string Format(DateTime localTime, bool dateOnly = false)
    {
        var actived = Supported[ActiveIndex];
        if (dateOnly)
            return localTime.ToString(actived.DateFormat, _culture);

        // 24時間制か12時間制（AM/PM付き）かでフォーマットを切り替え
        // upstream b2aba44c: 区切り文字が culture 依存で書き換わらないよう固定 culture を渡す
        var format = Use24Hours ? $"{actived.DateFormat} HH:mm:ss" : $"{actived.DateFormat} hh:mm:ss tt";
        return localTime.ToString(format, _culture);
    }
}
