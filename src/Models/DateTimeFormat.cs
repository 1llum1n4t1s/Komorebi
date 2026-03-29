using System;
using System.Collections.Generic;

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
    public static readonly List<DateTimeFormat> Supported = new List<DateTimeFormat>
    {
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
    };

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
    /// 現在日時をこのフォーマットで表示した例文
    /// </summary>
    public string Example
    {
        get => DateTime.Now.ToString(DateFormat);
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
            return localTime.ToString(actived.DateFormat);

        // 24時間制か12時間制（AM/PM付き）かでフォーマットを切り替え
        var format = Use24Hours ? $"{actived.DateFormat} HH:mm:ss" : $"{actived.DateFormat} hh:mm:ss tt";
        return localTime.ToString(format);
    }
}
