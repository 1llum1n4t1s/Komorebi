// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Komorebi.Views;

/// <summary>
/// コマンドログのタイムスタンプを表示するコントロール。
/// </summary>
public class CommandLogTime : TextBlock
{
    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>
    /// コントロールがアンロードされた際の処理。
    /// </summary>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        StopTimer();
    }

    /// <summary>
    /// データコンテキストが変更された際の処理。
    /// </summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        StopTimer();

        if (DataContext is ViewModels.CommandLog log)
            SetupCommandLog(log);
        else
            Text = string.Empty;
    }

    /// <summary>
    /// SetupCommandLogの処理を行う。
    /// </summary>
    private void SetupCommandLog(ViewModels.CommandLog log)
    {
        Text = GetDisplayText(log);
        if (log.IsComplete)
            return;

        // upstream acbb2e9a: スレッドプールの Timer から UI スレッドへ Invoke すると、
        // 終了時に TaskCanceledException が発生する。UI スレッド上で動く DispatcherTimer を使う。
        _refreshTimer = new DispatcherTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(100);
        _refreshTimer.Tick += (_, _) =>
        {
            Text = GetDisplayText(log);
            if (log.IsComplete)
                StopTimer();
        };
        _refreshTimer.Start();
    }

    /// <summary>
    /// StopTimerの処理を行う。
    /// </summary>
    private void StopTimer()
    {
        if (_refreshTimer is not null)
        {
            _refreshTimer.Stop();
            _refreshTimer = null;
        }
    }

    /// <summary>
    /// GetDisplayTextの処理を行う。
    /// </summary>
    private static string GetDisplayText(ViewModels.CommandLog log)
    {
        var endTime = log.IsComplete ? log.EndTime : DateTime.Now;
        var duration = endTime - log.StartTime;

        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:G3} min";

        if (duration.TotalSeconds >= 1)
            return $"{duration.TotalSeconds:G3} s";

        return $"{duration.TotalMilliseconds:G3} ms";
    }

    private DispatcherTimer _refreshTimer = null;
}
