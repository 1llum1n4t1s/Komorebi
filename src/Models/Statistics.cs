using System;
using System.Collections.Generic;
using System.Globalization;

using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

using SkiaSharp;

namespace Komorebi.Models;

/// <summary>
/// 統計の表示モード
/// </summary>
public enum StatisticsMode
{
    /// <summary>全期間</summary>
    All,
    /// <summary>今月</summary>
    ThisMonth,
    /// <summary>今週</summary>
    ThisWeek,
}

/// <summary>
/// 統計レポートにおける著者ごとのコミット数情報
/// </summary>
public class StatisticsAuthor(User user, int count)
{
    /// <summary>ユーザー情報</summary>
    public User User { get; set; } = user;
    /// <summary>コミット数</summary>
    public int Count { get; set; } = count;
}

/// <summary>
/// 特定期間のコミット統計レポート。
/// チャートデータとユーザー別フィルタリングを管理する。
/// </summary>
public class StatisticsReport
{
    /// <summary>合計コミット数</summary>
    public int Total { get; set; } = 0;
    /// <summary>著者リスト（コミット数降順）</summary>
    public List<StatisticsAuthor> Authors { get; set; } = new();
    /// <summary>チャートのシリーズデータ</summary>
    public List<ISeries> Series { get; set; } = new();
    /// <summary>X軸の設定</summary>
    public List<Axis> XAxes { get; set; } = new();
    /// <summary>Y軸の設定</summary>
    public List<Axis> YAxes { get; set; } = new();
    /// <summary>選択中の著者（変更時にチャートを更新）</summary>
    public StatisticsAuthor SelectedAuthor { get => _selectedAuthor; set => ChangeAuthor(value); }

    /// <summary>
    /// 統計レポートを初期化する。モードに応じてX軸のサンプルデータを準備する。
    /// </summary>
    /// <param name="mode">統計モード（全期間/今月/今週）</param>
    /// <param name="start">期間の開始日時</param>
    public StatisticsReport(StatisticsMode mode, DateTime start)
    {
        _mode = mode;

        YAxes.Add(new Axis()
        {
            TextSize = 10,
            MinLimit = 0,
            SeparatorsPaint = new SolidColorPaint(new SKColor(0x40808080)) { StrokeThickness = 1 }
        });

        if (mode == StatisticsMode.ThisWeek)
        {
            for (int i = 0; i < 7; i++)
                _mapSamples.Add(start.AddDays(i), 0);

            XAxes.Add(new DateTimeAxis(TimeSpan.FromDays(1), v => WEEKDAYS[(int)v.DayOfWeek]) { TextSize = 10 });
        }
        else if (mode == StatisticsMode.ThisMonth)
        {
            var now = DateTime.Now;
            var maxDays = DateTime.DaysInMonth(now.Year, now.Month);
            for (int i = 0; i < maxDays; i++)
                _mapSamples.Add(start.AddDays(i), 0);

            XAxes.Add(new DateTimeAxis(TimeSpan.FromDays(1), v => $"{v:MM/dd}") { TextSize = 10 });
        }
        else
        {
            XAxes.Add(new DateTimeAxis(TimeSpan.FromDays(30), v => $"{v:yyyy/MM}") { TextSize = 10 });
        }
    }

    /// <summary>
    /// コミットをレポートに追加する。日時とユーザーの集計マップを更新する。
    /// </summary>
    /// <param name="time">コミットの日時</param>
    /// <param name="author">コミットの著者</param>
    public void AddCommit(DateTime time, User author)
    {
        Total++;

        DateTime normalized;
        if (_mode == StatisticsMode.ThisWeek || _mode == StatisticsMode.ThisMonth)
            normalized = time.Date;
        else
            normalized = new DateTime(time.Year, time.Month, 1).ToLocalTime();

        if (_mapSamples.TryGetValue(normalized, out var vs))
            _mapSamples[normalized] = vs + 1;
        else
            _mapSamples.Add(normalized, 1);

        if (_mapUsers.TryGetValue(author, out var vu))
            _mapUsers[author] = vu + 1;
        else
            _mapUsers.Add(author, 1);

        if (_mapUserSamples.TryGetValue(author, out var vus))
        {
            if (vus.TryGetValue(normalized, out var n))
                vus[normalized] = n + 1;
            else
                vus.Add(normalized, 1);
        }
        else
        {
            _mapUserSamples.Add(author, new Dictionary<DateTime, int>
            {
                { normalized, 1 }
            });
        }
    }

    /// <summary>
    /// データ収集を完了し、チャート用のシリーズデータを構築する
    /// </summary>
    public void Complete()
    {
        // ユーザーマップから著者リストを作成し、コミット数で降順ソート
        foreach (var kv in _mapUsers)
            Authors.Add(new StatisticsAuthor(kv.Key, kv.Value));

        Authors.Sort((l, r) => r.Count - l.Count);

        var samples = new List<DateTimePoint>();
        foreach (var kv in _mapSamples)
            samples.Add(new DateTimePoint(kv.Key, kv.Value));

        Series.Add(
            new ColumnSeries<DateTimePoint>()
            {
                Values = samples,
                Stroke = null,
                Fill = null,
                Padding = 1,
            }
        );

        _mapUsers.Clear();
        _mapSamples.Clear();
    }

    /// <summary>
    /// チャートの塗りつぶし色を変更する。著者選択時はメインシリーズを半透明にする。
    /// </summary>
    /// <param name="color">ARGB色値</param>
    public void ChangeColor(uint color)
    {
        _fillColor = color;

        var fill = new SKColor(color);

        if (Series.Count > 0 && Series[0] is ColumnSeries<DateTimePoint> total)
            total.Fill = new SolidColorPaint(_selectedAuthor is null ? fill : fill.WithAlpha(51));

        if (Series.Count > 1 && Series[1] is ColumnSeries<DateTimePoint> user)
            user.Fill = new SolidColorPaint(fill);
    }

    /// <summary>
    /// 選択著者を変更し、その著者のコミットデータを追加シリーズとして表示する
    /// </summary>
    /// <param name="author">選択する著者（nullで全体表示に戻す）</param>
    public void ChangeAuthor(StatisticsAuthor author)
    {
        if (author == _selectedAuthor)
            return;

        _selectedAuthor = author;
        Series.RemoveRange(1, Series.Count - 1);
        if (author is null || !_mapUserSamples.TryGetValue(author.User, out var userSamples))
        {
            ChangeColor(_fillColor);
            return;
        }

        var samples = new List<DateTimePoint>();
        foreach (var kv in userSamples)
            samples.Add(new DateTimePoint(kv.Key, kv.Value));

        Series.Add(
            new ColumnSeries<DateTimePoint>()
            {
                Values = samples,
                Stroke = null,
                Fill = null,
                Padding = 1,
            }
        );

        ChangeColor(_fillColor);
    }

    /// <summary>曜日の略称ラベル</summary>
    private static readonly string[] WEEKDAYS = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];
    /// <summary>統計モード</summary>
    private StatisticsMode _mode;
    /// <summary>ユーザーごとのコミット数マップ</summary>
    private Dictionary<User, int> _mapUsers = new();
    /// <summary>日時ごとのコミット数マップ</summary>
    private Dictionary<DateTime, int> _mapSamples = new();
    /// <summary>ユーザー×日時ごとのコミット数マップ</summary>
    private Dictionary<User, Dictionary<DateTime, int>> _mapUserSamples = new();
    /// <summary>現在選択中の著者</summary>
    private StatisticsAuthor _selectedAuthor = null;
    /// <summary>チャートの塗りつぶし色</summary>
    private uint _fillColor = 255;
}

/// <summary>
/// コミット統計を集計するクラス。全期間・今月・今週の3つのレポートを管理する。
/// </summary>
public class Statistics
{
    /// <summary>全期間のレポート</summary>
    public StatisticsReport All { get; }
    /// <summary>今月のレポート</summary>
    public StatisticsReport Month { get; }
    /// <summary>今週のレポート</summary>
    public StatisticsReport Week { get; }

    /// <summary>
    /// 統計を初期化する。現在の日時から今週・今月の開始日を計算する。
    /// </summary>
    public Statistics()
    {
        var today = DateTime.Now.ToLocalTime().Date;
        var weekOffset = (7 + (int)today.DayOfWeek - (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek) % 7;
        _thisWeekStart = today.AddDays(-weekOffset);
        _thisMonthStart = today.AddDays(1 - today.Day);

        All = new StatisticsReport(StatisticsMode.All, DateTime.MinValue);
        Month = new StatisticsReport(StatisticsMode.ThisMonth, _thisMonthStart);
        Week = new StatisticsReport(StatisticsMode.ThisWeek, _thisWeekStart);
    }

    /// <summary>
    /// コミットを統計に追加する。期間に応じた各レポートに振り分ける。
    /// </summary>
    /// <param name="author">「名前±メールアドレス」形式の著者文字列</param>
    /// <param name="timestamp">UNIXタイムスタンプ</param>
    public void AddCommit(string author, double timestamp)
    {
        var emailIdx = author.IndexOf('±');
        var email = author[(emailIdx + 1)..].ToLower(CultureInfo.CurrentCulture);
        if (!_users.TryGetValue(email, out var user))
        {
            user = User.FindOrAdd(author);
            _users.Add(email, user);
        }

        var time = DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime();
        if (time >= _thisWeekStart)
            Week.AddCommit(time, user);

        if (time >= _thisMonthStart)
            Month.AddCommit(time, user);

        All.AddCommit(time, user);
    }

    /// <summary>
    /// 全レポートのデータ収集を完了し、チャートデータを構築する
    /// </summary>
    public void Complete()
    {
        _users.Clear();

        All.Complete();
        Month.Complete();
        Week.Complete();
    }

    /// <summary>今月の開始日</summary>
    private readonly DateTime _thisMonthStart;
    /// <summary>今週の開始日</summary>
    private readonly DateTime _thisWeekStart;
    /// <summary>メールアドレスによるユーザー検索キャッシュ</summary>
    private readonly Dictionary<string, User> _users = new();
}
