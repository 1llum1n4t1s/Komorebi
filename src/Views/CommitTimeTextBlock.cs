using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Komorebi.Views
{
    /// <summary>
    ///     コミットのタイムスタンプを相対時間または絶対時間で表示するテキストブロック。
    /// </summary>
    public class CommitTimeTextBlock : TextBlock
    {
        public static readonly StyledProperty<bool> ShowAsDateTimeProperty =
            AvaloniaProperty.Register<CommitTimeTextBlock, bool>(nameof(ShowAsDateTime), true);

        public bool ShowAsDateTime
        {
            get => GetValue(ShowAsDateTimeProperty);
            set => SetValue(ShowAsDateTimeProperty, value);
        }

        public static readonly StyledProperty<bool> Use24HoursProperty =
            AvaloniaProperty.Register<CommitTimeTextBlock, bool>(nameof(Use24Hours), true);

        public bool Use24Hours
        {
            get => GetValue(Use24HoursProperty);
            set => SetValue(Use24HoursProperty, value);
        }

        public static readonly StyledProperty<int> DateTimeFormatProperty =
            AvaloniaProperty.Register<CommitTimeTextBlock, int>(nameof(DateTimeFormat));

        public int DateTimeFormat
        {
            get => GetValue(DateTimeFormatProperty);
            set => SetValue(DateTimeFormatProperty, value);
        }

        public static readonly StyledProperty<bool> UseAuthorTimeProperty =
            AvaloniaProperty.Register<CommitTimeTextBlock, bool>(nameof(UseAuthorTime), true);

        public bool UseAuthorTime
        {
            get => GetValue(UseAuthorTimeProperty);
            set => SetValue(UseAuthorTimeProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextBlock);

        /// <summary>
        ///     プロパティが変更された際の処理。
        /// </summary>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == UseAuthorTimeProperty)
            {
                SetCurrentValue(TextProperty, GetDisplayText());
            }
            else if (change.Property == ShowAsDateTimeProperty)
            {
                SetCurrentValue(TextProperty, GetDisplayText());

                if (ShowAsDateTime)
                {
                    StopTimer();
                    HorizontalAlignment = HorizontalAlignment.Left;
                }
                else
                {
                    StartTimer();
                    HorizontalAlignment = HorizontalAlignment.Center;
                }
            }
            else if (change.Property == DateTimeFormatProperty || change.Property == Use24HoursProperty)
            {
                if (ShowAsDateTime)
                    SetCurrentValue(TextProperty, GetDisplayText());
            }
        }

        /// <summary>
        ///     コントロールが読み込まれた際の処理。
        /// </summary>
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (!ShowAsDateTime)
                StartTimer();
        }

        /// <summary>
        ///     コントロールがアンロードされた際の処理。
        /// </summary>
        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            StopTimer();
        }

        /// <summary>
        ///     データコンテキストが変更された際の処理。
        /// </summary>
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            SetCurrentValue(TextProperty, GetDisplayText());
        }

        /// <summary>
        ///     StartTimerの処理を行う。
        /// </summary>
        private void StartTimer()
        {
            if (_refreshTimer != null)
                return;

            _refreshTimer = DispatcherTimer.Run(() =>
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    var text = GetDisplayText();
                    if (!text.Equals(Text, StringComparison.Ordinal))
                        Text = text;
                });

                return true;
            }, TimeSpan.FromSeconds(10));
        }

        /// <summary>
        ///     StopTimerの処理を行う。
        /// </summary>
        private void StopTimer()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
        }

        /// <summary>
        ///     GetDisplayTextの処理を行う。
        /// </summary>
        private string GetDisplayText()
        {
            if (DataContext is not Models.Commit commit)
                return string.Empty;

            if (ShowAsDateTime)
                return UseAuthorTime ? commit.AuthorTimeStr : commit.CommitterTimeStr;

            var timestamp = UseAuthorTime ? commit.AuthorTime : commit.CommitterTime;
            var now = DateTime.Now;
            var localTime = DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime();
            var span = now - localTime;
            if (span.TotalMinutes < 1)
                return App.Text("Period.JustNow");

            if (span.TotalHours < 1)
                return App.Text("Period.MinutesAgo", (int)span.TotalMinutes);

            if (span.TotalDays < 1)
            {
                var hours = (int)span.TotalHours;
                return hours == 1 ? App.Text("Period.HourAgo") : App.Text("Period.HoursAgo", hours);
            }

            var lastDay = now.AddDays(-1).Date;
            if (localTime >= lastDay)
                return App.Text("Period.Yesterday");

            if ((localTime.Year == now.Year && localTime.Month == now.Month) || span.TotalDays < 28)
            {
                var diffDay = now.Date - localTime.Date;
                return App.Text("Period.DaysAgo", (int)diffDay.TotalDays);
            }

            var lastMonth = now.AddMonths(-1).Date;
            if (localTime.Year == lastMonth.Year && localTime.Month == lastMonth.Month)
                return App.Text("Period.LastMonth");

            if (localTime.Year == now.Year || localTime > now.AddMonths(-11))
            {
                var diffMonth = (12 + now.Month - localTime.Month) % 12;
                return App.Text("Period.MonthsAgo", diffMonth);
            }

            var diffYear = now.Year - localTime.Year;
            if (diffYear == 1)
                return App.Text("Period.LastYear");

            return App.Text("Period.YearsAgo", diffYear);
        }

        private IDisposable _refreshTimer = null;
    }
}
