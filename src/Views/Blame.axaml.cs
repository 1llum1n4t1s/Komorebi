using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using AvaloniaEdit.Utils;

namespace Komorebi.Views;

/// <summary>
/// BlameTextEditorクラス。
/// </summary>
public class BlameTextEditor : TextEditor
{
    /// <summary>
    /// CommitInfoMarginクラス。
    /// </summary>
    public class CommitInfoMargin : AbstractMargin
    {
        /// <summary>
        /// コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public CommitInfoMargin(BlameTextEditor editor)
        {
            _editor = editor;
            ClipToBounds = true;
        }

        /// <summary>
        /// コントロールの描画処理を行う。
        /// </summary>
        public override void Render(DrawingContext context)
        {
            if (_editor.BlameData is null)
                return;

            var view = TextView;
            if (view is { VisualLinesValid: true })
            {
                var typeface = view.CreateTypeface();
                var underlinePen = new Pen(Brushes.DarkOrange);
                var width = Bounds.Width;
                var lineHeight = view.DefaultLineHeight;
                var pixelHeight = PixelSnapHelpers.GetPixelSize(view).Height;

                foreach (var line in view.VisualLines)
                {
                    if (line.IsDisposed || line.FirstDocumentLine is null || line.FirstDocumentLine.IsDeleted)
                        continue;

                    var lineNumber = line.FirstDocumentLine.LineNumber;
                    if (lineNumber > _editor.BlameData.LineInfos.Count)
                        break;

                    var info = _editor.BlameData.LineInfos[lineNumber - 1];
                    var x = 0.0;
                    var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineMiddle) - view.VerticalOffset;
                    if (!info.IsFirstInGroup && y > lineHeight)
                        continue;

                    var shaLink = new FormattedText(
                        info.CommitSHA,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        _editor.FontSize,
                        Brushes.DarkOrange);
                    var shaLinkTop = y - shaLink.Height * 0.5;
                    var underlineY = PixelSnapHelpers.PixelAlign(y + shaLink.Height * 0.5 + 0.5, pixelHeight);
                    context.DrawText(shaLink, new Point(x, shaLinkTop));
                    context.DrawLine(underlinePen, new Point(x, underlineY), new Point(x + shaLink.Width, underlineY));
                    x += shaLink.Width + 8;

                    var author = new FormattedText(
                        info.Author,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        _editor.FontSize,
                        _editor.Foreground);
                    var authorTop = y - author.Height * 0.5;
                    context.DrawText(author, new Point(x, authorTop));

                    var timeStr = Models.DateTimeFormat.Format(info.Timestamp, true);
                    var time = new FormattedText(
                        timeStr,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        _editor.FontSize,
                        _editor.Foreground);
                    var timeTop = y - time.Height * 0.5;
                    context.DrawText(time, new Point(width - time.Width, timeTop));
                }
            }
        }

        /// <summary>
        /// コントロールの測定処理をオーバーライドする。
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            var view = TextView;
            var maxWidth = 0.0;
            if (view is { VisualLinesValid: true } && _editor.BlameData is not null)
            {
                var typeface = view.CreateTypeface();
                var calculated = new HashSet<string>();
                foreach (var line in view.VisualLines)
                {
                    var lineNumber = line.FirstDocumentLine.LineNumber;
                    if (lineNumber > _editor.BlameData.LineInfos.Count)
                        break;

                    var info = _editor.BlameData.LineInfos[lineNumber - 1];

                    if (!calculated.Add(info.CommitSHA))
                        continue;

                    var x = 0.0;
                    var shaLink = new FormattedText(
                        info.CommitSHA,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        _editor.FontSize,
                        Brushes.DarkOrange);
                    x += shaLink.Width + 8;

                    var author = new FormattedText(
                        info.Author,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        _editor.FontSize,
                        _editor.Foreground);
                    x += author.Width + 8;

                    var timeStr = Models.DateTimeFormat.Format(info.Timestamp, true);
                    var time = new FormattedText(
                        timeStr,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        _editor.FontSize,
                        _editor.Foreground);
                    x += time.Width;

                    if (maxWidth < x)
                        maxWidth = x;
                }
            }

            return new Size(maxWidth, 0);
        }

        /// <summary>
        /// ポインターが移動した際のイベント処理。
        /// </summary>
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            var view = TextView;
            if (!e.Handled && view is { VisualLinesValid: true })
            {
                var pos = e.GetPosition(this);
                var typeface = view.CreateTypeface();
                var lineHeight = view.DefaultLineHeight;

                foreach (var line in view.VisualLines)
                {
                    if (line.IsDisposed || line.FirstDocumentLine is null || line.FirstDocumentLine.IsDeleted)
                        continue;

                    var lineNumber = line.FirstDocumentLine.LineNumber;
                    if (lineNumber > _editor.BlameData.LineInfos.Count)
                        break;

                    var info = _editor.BlameData.LineInfos[lineNumber - 1];
                    var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - view.VerticalOffset;
                    var shaLink = new FormattedText(
                        info.CommitSHA,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        _editor.FontSize,
                        Brushes.DarkOrange);

                    var rect = new Rect(0, y, shaLink.Width, lineHeight);
                    if (rect.Contains(pos))
                    {
                        Cursor = Cursor.Parse("Hand");

                        if (DataContext is ViewModels.Blame blame)
                        {
                            var msg = blame.GetCommitMessage(info.CommitSHA);
                            ToolTip.SetTip(this, msg);
                        }

                        return;
                    }
                }

                Cursor = Cursor.Default;
                ToolTip.SetTip(this, null);
            }
        }

        /// <summary>
        /// ポインターが押された際のイベント処理。
        /// </summary>
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            var view = TextView;
            if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && view is { VisualLinesValid: true })
            {
                var pos = e.GetPosition(this);
                var typeface = view.CreateTypeface();

                foreach (var line in view.VisualLines)
                {
                    if (line.IsDisposed || line.FirstDocumentLine is null || line.FirstDocumentLine.IsDeleted)
                        continue;

                    var lineNumber = line.FirstDocumentLine.LineNumber;
                    if (lineNumber > _editor.BlameData.LineInfos.Count)
                        break;

                    var info = _editor.BlameData.LineInfos[lineNumber - 1];
                    var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset;
                    var shaLink = new FormattedText(
                        info.CommitSHA,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        _editor.FontSize,
                        Brushes.DarkOrange);

                    var rect = new Rect(0, y, shaLink.Width, shaLink.Height);
                    if (rect.Contains(pos))
                    {
                        if (DataContext is ViewModels.Blame blame)
                            blame.NavigateToCommit(info.File, info.CommitSHA);

                        e.Handled = true;
                        break;
                    }
                }
            }
        }

        /// <summary>親のBlameTextEditorへの参照。</summary>
        private readonly BlameTextEditor _editor = null;
    }

    /// <summary>
    /// VerticalSeparatorMarginクラス。
    /// </summary>
    public class VerticalSeparatorMargin : AbstractMargin
    {
        /// <summary>
        /// コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public VerticalSeparatorMargin(BlameTextEditor editor)
        {
            _editor = editor;
        }

        /// <summary>
        /// コントロールの描画処理を行う。
        /// </summary>
        public override void Render(DrawingContext context)
        {
            var pen = new Pen(_editor.BorderBrush);
            context.DrawLine(pen, new Point(0.5, 0), new Point(0.5, Bounds.Height));
        }

        /// <summary>
        /// コントロールの測定処理をオーバーライドする。
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(1, 0);
        }

        /// <summary>親のBlameTextEditorへの参照。</summary>
        private readonly BlameTextEditor _editor = null;
    }

    /// <summary>
    /// LineBackgroundRendererクラス。
    /// </summary>
    public class LineBackgroundRenderer : IBackgroundRenderer
    {
        /// <summary>背景レイヤーに描画する。</summary>
        public KnownLayer Layer => KnownLayer.Background;

        /// <summary>
        /// コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public LineBackgroundRenderer(BlameTextEditor owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// Drawの処理を行う。
        /// </summary>
        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (!textView.VisualLinesValid)
                return;

            var w = textView.Bounds.Width;
            if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0)
                return;

            var highlight = _owner._highlight;
            if (string.IsNullOrEmpty(highlight))
                return;

            var color = (Color)_owner.FindResource("SystemAccentColor")!;
            var brush = new SolidColorBrush(color, 0.2);
            var lines = _owner.BlameData.LineInfos;

            foreach (var line in textView.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine is null || line.FirstDocumentLine.IsDeleted)
                    continue;

                var lineNumber = line.FirstDocumentLine.LineNumber;
                if (lineNumber > lines.Count)
                    break;

                var info = lines[lineNumber - 1];
                if (!info.CommitSHA.Equals(highlight, StringComparison.Ordinal))
                    continue;

                var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - textView.VerticalOffset;
                var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - textView.VerticalOffset;
                drawingContext.FillRectangle(brush, new Rect(0, startY, w, endY - startY));
            }
        }

        /// <summary>親のBlameTextEditorへの参照。</summary>
        private readonly BlameTextEditor _owner;
    }

    /// <summary>表示対象のファイルパスを保持するスタイルプロパティ。</summary>
    public static readonly StyledProperty<string> FileProperty =
        AvaloniaProperty.Register<BlameTextEditor, string>(nameof(File));

    /// <summary>表示対象のファイルパス。シンタックスハイライトの文法選択に使用。</summary>
    public string File
    {
        get => GetValue(FileProperty);
        set => SetValue(FileProperty, value);
    }

    /// <summary>Blameデータを保持するスタイル���ロパティ。</summary>
    public static readonly StyledProperty<Models.BlameData> BlameDataProperty =
        AvaloniaProperty.Register<BlameTextEditor, Models.BlameData>(nameof(BlameData));

    /// <summary>各行のblame情報（コミットSHA・著者・日時など）を含むデータ。</summary>
    public Models.BlameData BlameData
    {
        get => GetValue(BlameDataProperty);
        set => SetValue(BlameDataProperty, value);
    }

    /// <summary>タブ幅を保持するスタイ��プロパティ。</summary>
    public static readonly StyledProperty<int> TabWidthProperty =
        AvaloniaProperty.Register<BlameTextEditor, int>(nameof(TabWidth), 4);

    /// <summary>タブ文字のインデント幅。</summary>
    public int TabWidth
    {
        get => GetValue(TabWidthProperty);
        set => SetValue(TabWidthProperty, value);
    }

    /// <summary>スタイルキーをTextEditorに設定。</summary>
    protected override Type StyleKeyOverride => typeof(TextEditor);

    /// <summary>
    /// BlameTextEditorの処理を行う。
    /// </summary>
    public BlameTextEditor() : base(new TextArea(), new TextDocument())
    {
        IsReadOnly = true;
        ShowLineNumbers = false;
        WordWrap = false;

        Options.IndentationSize = TabWidth;
        Options.EnableHyperlinks = false;
        Options.EnableEmailHyperlinks = false;

        _textMate = Models.TextMateHelper.CreateForEditor(this);

        TextArea.LeftMargins.Add(new CommitInfoMargin(this) { Margin = new Thickness(8, 0) });
        TextArea.LeftMargins.Add(new VerticalSeparatorMargin(this));
        TextArea.LeftMargins.Add(new LineNumberMargin() { Margin = new Thickness(8, 0) });
        TextArea.LeftMargins.Add(new VerticalSeparatorMargin(this));
        TextArea.Caret.PositionChanged += OnTextAreaCaretPositionChanged;
        TextArea.TextView.BackgroundRenderers.Add(new LineBackgroundRenderer(this));
        TextArea.TextView.ContextRequested += OnTextViewContextRequested;
        TextArea.TextView.VisualLinesChanged += OnTextViewVisualLinesChanged;
        TextArea.TextView.Margin = new Thickness(4, 0);
    }

    /// <summary>
    /// コントロールがアンロードされた際の処理。
    /// </summary>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        TextArea.LeftMargins.Clear();
        TextArea.Caret.PositionChanged -= OnTextAreaCaretPositionChanged;
        TextArea.TextView.ContextRequested -= OnTextViewContextRequested;
        TextArea.TextView.VisualLinesChanged -= OnTextViewVisualLinesChanged;

        if (_textMate is not null)
        {
            _textMate.Dispose();
            _textMate = null;
        }
    }

    /// <summary>
    /// プロパティが変更された際の処理。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FileProperty)
        {
            if (File is { Length: > 0 })
                Models.TextMateHelper.SetGrammarByFileName(_textMate, File);
        }
        if (change.Property == BlameDataProperty)
        {
            if (BlameData is { IsBinary: false } blame)
                Text = blame.Content;
            else
                Text = string.Empty;
        }
        else if (change.Property == TabWidthProperty)
        {
            Options.IndentationSize = TabWidth;
        }
        else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue is not null)
        {
            Models.TextMateHelper.SetThemeByApp(_textMate);
        }
    }

    /// <summary>
    /// TextAreaCaretPositionChangedイベントのハンドラ。
    /// </summary>
    private void OnTextAreaCaretPositionChanged(object sender, EventArgs e)
    {
        if (!TextArea.IsFocused)
            return;

        var caret = TextArea.Caret;
        if (caret is null || caret.Line > BlameData.LineInfos.Count)
            return;

        _highlight = BlameData.LineInfos[caret.Line - 1].CommitSHA;
    }

    /// <summary>
    /// TextViewContextRequestedイベントのハンドラ。
    /// </summary>
    private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
    {
        var selected = SelectedText;
        if (string.IsNullOrEmpty(selected))
            return;

        var copy = new MenuItem();
        copy.Header = App.Text("Copy");
        copy.Icon = App.CreateMenuIcon("Icons.Copy");
        copy.Click += async (_, ev) =>
        {
            await App.CopyTextAsync(selected);
            ev.Handled = true;
        };

        var menu = new ContextMenu();
        menu.Items.Add(copy);
        menu.Open(TextArea.TextView);

        e.Handled = true;
    }

    /// <summary>
    /// TextViewVisualLinesChangedイベントのハンドラ。
    /// </summary>
    private void OnTextViewVisualLinesChanged(object sender, EventArgs e)
    {
        foreach (var margin in TextArea.LeftMargins)
        {
            if (margin is CommitInfoMargin commitInfo)
            {
                commitInfo.InvalidateMeasure();
                break;
            }
        }
    }

    /// <summary>TextMateによるシンタックスハイライト設定。</summary>
    private TextMate.Installation _textMate = null;

    /// <summary>現在ハイライト中のコミットSHA。キャレット行のblame情報から取得。</summary>
    private string _highlight = string.Empty;
}

/// <summary>
/// Blame（各行の変更者表示）ビューのコードビハインド。
/// </summary>
public partial class Blame : ChromelessWindow
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public Blame()
    {
        InitializeComponent();
    }

    /// <summary>
    /// ウィンドウが閉じられた後の処理。
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        GC.Collect();
    }

    /// <summary>
    /// ポインターが離された際のイベント処理。
    /// </summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!e.Handled && DataContext is ViewModels.Blame blame)
        {
            if (e.InitialPressMouseButton == MouseButton.XButton1)
            {
                blame.Back();
                e.Handled = true;
            }
            else if (e.InitialPressMouseButton == MouseButton.XButton2)
            {
                blame.Forward();
                e.Handled = true;
            }
        }
    }
}
