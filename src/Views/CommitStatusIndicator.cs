using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Komorebi.Views;

/// <summary>
///     コミット操作の進行状態（マージ中・リベース中等）を示すインジケータ。
/// </summary>
public class CommitStatusIndicator : Control
{
    public static readonly StyledProperty<Models.Branch> CurrentBranchProperty =
        AvaloniaProperty.Register<CommitStatusIndicator, Models.Branch>(nameof(CurrentBranch));

    public Models.Branch CurrentBranch
    {
        get => GetValue(CurrentBranchProperty);
        set => SetValue(CurrentBranchProperty, value);
    }

    public static readonly StyledProperty<IBrush> AheadBrushProperty =
        AvaloniaProperty.Register<CommitStatusIndicator, IBrush>(nameof(AheadBrush));

    public IBrush AheadBrush
    {
        get => GetValue(AheadBrushProperty);
        set => SetValue(AheadBrushProperty, value);
    }

    public static readonly StyledProperty<IBrush> BehindBrushProperty =
        AvaloniaProperty.Register<CommitStatusIndicator, IBrush>(nameof(BehindBrush));

    public IBrush BehindBrush
    {
        get => GetValue(BehindBrushProperty);
        set => SetValue(BehindBrushProperty, value);
    }

    private enum Status
    {
        Normal,
        Ahead,
        Behind,
    }

    /// <summary>
    ///     コントロールの描画処理を行う。
    /// </summary>
    public override void Render(DrawingContext context)
    {
        if (_status == Status.Normal)
            return;

        context.DrawEllipse(_status == Status.Ahead ? AheadBrush : BehindBrush, null, new Rect(0, 0, 5, 5));
    }

    /// <summary>
    ///     コントロールの測定処理をオーバーライドする。
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (DataContext is Models.Commit commit && CurrentBranch is { } b)
        {
            var sha = commit.SHA;

            if (b.Ahead.Contains(sha))
                _status = Status.Ahead;
            else if (b.Behind.Contains(sha))
                _status = Status.Behind;
            else
                _status = Status.Normal;
        }
        else
        {
            _status = Status.Normal;
        }

        return _status == Status.Normal ? new Size(0, 0) : new Size(9, 5);
    }

    /// <summary>
    ///     データコンテキストが変更された際の処理。
    /// </summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        InvalidateMeasure();
    }

    /// <summary>
    ///     プロパティが変更された際の処理。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurrentBranchProperty)
            InvalidateMeasure();
    }

    private Status _status = Status.Normal;
}
