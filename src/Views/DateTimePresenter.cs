using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Komorebi.Views;

public class DateTimePresenter : TextBlock
{
    public static readonly StyledProperty<bool> ShowDateOnlyProperty =
        AvaloniaProperty.Register<DateTimePresenter, bool>(nameof(ShowDateOnly), false);

    public bool ShowDateOnly
    {
        get => GetValue(ShowDateOnlyProperty);
        set => SetValue(ShowDateOnlyProperty, value);
    }

    public static readonly StyledProperty<bool> Use24HoursProperty =
        AvaloniaProperty.Register<DateTimePresenter, bool>(nameof(Use24Hours), true);

    public bool Use24Hours
    {
        get => GetValue(Use24HoursProperty);
        set => SetValue(Use24HoursProperty, value);
    }

    public static readonly StyledProperty<int> DateTimeFormatProperty =
        AvaloniaProperty.Register<DateTimePresenter, int>(nameof(DateTimeFormat));

    public int DateTimeFormat
    {
        get => GetValue(DateTimeFormatProperty);
        set => SetValue(DateTimeFormatProperty, value);
    }

    public static readonly StyledProperty<ulong> TimestampProperty =
        AvaloniaProperty.Register<DateTimePresenter, ulong>(nameof(Timestamp), 0);

    public ulong Timestamp
    {
        get => GetValue(TimestampProperty);
        set => SetValue(TimestampProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>
    /// コントロールが読み込まれた際の処理。設定値の同期を開始する。
    /// </summary>
    /// <remarks>
    /// upstream は ctor で Binding(Path="...") を張るが、Native AOT で IL3050 になるため
    /// 文字列パス解決を使わない購読へ置き換えている (PropertySync 参照)。
    /// 購読解除点を持つため確立は Loaded、解除は Unloaded で行う。
    /// </remarks>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _use24HoursSync = PropertySync.OneWay(
            this, Use24HoursProperty,
            ViewModels.Preferences.Instance,
            nameof(ViewModels.Preferences.Use24Hours),
            static p => p.Use24Hours);

        _dateTimeFormatSync = PropertySync.OneWay(
            this, DateTimeFormatProperty,
            ViewModels.Preferences.Instance,
            nameof(ViewModels.Preferences.DateTimeFormat),
            static p => p.DateTimeFormat);
    }

    /// <summary>
    /// コントロールがアンロードされた際の処理。設定値の同期を解除する。
    /// </summary>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        _use24HoursSync?.Dispose();
        _use24HoursSync = null;
        _dateTimeFormatSync?.Dispose();
        _dateTimeFormatSync = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ShowDateOnlyProperty ||
            change.Property == Use24HoursProperty ||
            change.Property == DateTimeFormatProperty ||
            change.Property == TimestampProperty)
        {
            var text = Models.DateTimeFormat.Format(Timestamp, ShowDateOnly);
            SetCurrentValue(TextProperty, text);
        }
    }

    /// <summary>24 時間表記設定の同期ハンドル。</summary>
    private IDisposable? _use24HoursSync;

    /// <summary>日時フォーマット設定の同期ハンドル。</summary>
    private IDisposable? _dateTimeFormatSync;
}
