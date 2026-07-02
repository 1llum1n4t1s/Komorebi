using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Komorebi.Views;

/// <summary>
/// コミットメッセージのトレーラー（Co-authored-by等）自動補完候補1件を表す。
/// </summary>
/// <param name="Target">補完対象の<see cref="TextBox"/>。</param>
/// <param name="Text">補完候補文字列。</param>
/// <param name="ReplaceStart">置換開始位置（文字インデックス）。</param>
/// <param name="ReplaceLen">置換対象の文字数。</param>
public record CommitMessageTextBoxSuggestion(TextBox Target, string Text, int ReplaceStart, int ReplaceLen)
{
    /// <summary>
    /// 補完候補を確定し、対象テキストボックスの入力中の語を置換する。
    /// </summary>
    public void Use()
    {
        var text = Target.Text ?? string.Empty;
        if (ReplaceStart + ReplaceLen > text.Length)
            return;

        var builder = new StringBuilder();
        builder
            .Append(text.Substring(0, ReplaceStart))
            .Append(Text)
            .Append(text.Substring(ReplaceStart + ReplaceLen));

        Target.Text = builder.ToString();
        Target.CaretIndex = ReplaceStart + Text.Length;
    }
}

/// <summary>
/// コミットメッセージ入力欄。標準<see cref="TextBox"/>を継承し、IME入力に完全対応しつつ
/// 件名（subject）文字数カウント・トレーラー自動補完・件名終端位置の算出を行う。
/// </summary>
/// <remarks>
/// upstream <c>3331766d</c> 由来。旧 <c>CommitMessageTextEditor</c>（<c>AvaloniaEdit.TextEditor</c> 継承）は
/// IME確定前入力がエディタに正しく反映されない不具合があったため、標準 <see cref="TextBox"/> ベースに全面移行した。
/// </remarks>
public class CommitMessageTextBox : TextBox
{
    /// <summary>キャレットの現在列番号を保持するダイレクトプロパティ。</summary>
    public static readonly DirectProperty<CommitMessageTextBox, int> ColumnProperty =
        AvaloniaProperty.RegisterDirect<CommitMessageTextBox, int>(
            nameof(Column),
            o => o.Column);

    /// <summary>キャレットの現在列番号。ステータスバー表示に使用。</summary>
    public int Column
    {
        get => _column;
        set => SetAndRaise(ColumnProperty, ref _column, value);
    }

    /// <summary>件名の文字数を保持するダイレクトプロパティ。</summary>
    public static readonly DirectProperty<CommitMessageTextBox, int> SubjectLengthProperty =
        AvaloniaProperty.RegisterDirect<CommitMessageTextBox, int>(
            nameof(SubjectLength),
            o => o.SubjectLength);

    /// <summary>コミットメッセージ件名の文字数。</summary>
    public int SubjectLength
    {
        get => _subjectLen;
        set => SetAndRaise(SubjectLengthProperty, ref _subjectLen, value);
    }

    /// <summary>件名のガイドライン文字数を保持するダイレクトプロパティ。</summary>
    public static readonly DirectProperty<CommitMessageTextBox, int> SubjectGuideLengthProperty =
        AvaloniaProperty.RegisterDirect<CommitMessageTextBox, int>(
            nameof(SubjectGuideLength),
            o => o.SubjectGuideLength,
            (o, v) => o.SubjectGuideLength = v);

    /// <summary>件名の推奨最大文字数。超過時に警告表示となる。</summary>
    public int SubjectGuideLength
    {
        get => _subjectGuideLen;
        set => SetAndRaise(SubjectGuideLengthProperty, ref _subjectGuideLen, value);
    }

    /// <summary>件名終端のY座標（コントロール内相対）を保持するダイレクトプロパティ。</summary>
    public static readonly DirectProperty<CommitMessageTextBox, double> SubjectEndYProperty =
        AvaloniaProperty.RegisterDirect<CommitMessageTextBox, double>(
            nameof(SubjectEndY),
            o => o.SubjectEndY);

    /// <summary>件名終端のY座標。<see cref="CommitMessageSubjectEndIndicator"/>の区切り線描画位置に使用。</summary>
    public double SubjectEndY
    {
        get => _subjectEndY;
        set => SetAndRaise(SubjectEndYProperty, ref _subjectEndY, value);
    }

    /// <summary>件名長警告状態を保持するダイレクトプロパティ。</summary>
    public static readonly DirectProperty<CommitMessageTextBox, bool> WarnSubjectLengthProperty =
        AvaloniaProperty.RegisterDirect<CommitMessageTextBox, bool>(
            nameof(WarnSubjectLength),
            o => o.WarnSubjectLength);

    /// <summary>件名がガイドライン文字数を超えている場合にtrueとなる。</summary>
    public bool WarnSubjectLength
    {
        get => _warnSubjectLen;
        set => SetAndRaise(WarnSubjectLengthProperty, ref _warnSubjectLen, value);
    }

    /// <summary>トレーラー自動補完候補一覧を保持するダイレクトプロパティ。</summary>
    public static readonly DirectProperty<CommitMessageTextBox, List<CommitMessageTextBoxSuggestion>> SuggestionsProperty =
        AvaloniaProperty.RegisterDirect<CommitMessageTextBox, List<CommitMessageTextBoxSuggestion>>(
            nameof(Suggestions),
            o => o.Suggestions);

    /// <summary>現在候補として表示中のトレーラー自動補完一覧。nullの場合は非表示。</summary>
    public List<CommitMessageTextBoxSuggestion> Suggestions
    {
        get => _suggestions;
        set => SetAndRaise(SuggestionsProperty, ref _suggestions, value);
    }

    /// <summary>選択中の補完候補インデックスを保持するダイレクトプロパティ。</summary>
    public static readonly DirectProperty<CommitMessageTextBox, int> SelectedSuggestionIndexProperty =
        AvaloniaProperty.RegisterDirect<CommitMessageTextBox, int>(
            nameof(SelectedSuggestionIndex),
            o => o.SelectedSuggestionIndex,
            (o, v) => o.SelectedSuggestionIndex = v);

    /// <summary>選択中の補完候補インデックス。</summary>
    public int SelectedSuggestionIndex
    {
        get => _selectedSuggestionIdx;
        set => SetAndRaise(SelectedSuggestionIndexProperty, ref _selectedSuggestionIdx, value);
    }

    /// <summary>補完候補ポップアップのY座標（コントロール内相対）を保持するダイレクトプロパティ。</summary>
    public static readonly DirectProperty<CommitMessageTextBox, double> SuggestionPopupYProperty =
        AvaloniaProperty.RegisterDirect<CommitMessageTextBox, double>(
            nameof(SuggestionPopupY),
            o => o.SuggestionPopupY);

    /// <summary>補完候補ポップアップのY座標。</summary>
    public double SuggestionPopupY
    {
        get => _suggestionPopupY;
        set => SetAndRaise(SuggestionPopupYProperty, ref _suggestionPopupY, value);
    }

    /// <summary>スタイルキーをTextBoxに設定。</summary>
    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public CommitMessageTextBox()
    {
        AcceptsReturn = true;
        AcceptsTab = true;
        TextWrapping = TextWrapping.Wrap;
        HorizontalContentAlignment = HorizontalAlignment.Left;
        VerticalContentAlignment = VerticalAlignment.Top;
        Padding = new Thickness(4);

        SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
    }

    /// <summary>
    /// テンプレート適用時の処理。内部パーツの参照を取得する。
    /// </summary>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _textPresenter = e.NameScope.Get<TextPresenter>("PART_TextPresenter");
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
    }

    /// <summary>
    /// コントロールが読み込まれた際の処理。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        LayoutUpdated += OnLayoutUpdated;
        OnLayoutUpdated(null, null);
    }

    /// <summary>
    /// コントロールがアンロードされた際の処理。
    /// </summary>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        base.OnUnloaded(e);
    }

    /// <summary>
    /// プロパティが変更された際の処理。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            var text = Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                _subjectEndCharIdx = -1;
                SubjectLength = 0;
                return;
            }

            var subjectLen = 0;
            var lastNonLineBreakCharIdx = 0;
            var lastLineStart = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch == '\n')
                {
                    var line = (i > lastLineStart) ? text.Substring(lastLineStart, i - lastLineStart) : string.Empty;
                    lastLineStart = i + 1;

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (subjectLen > 0)
                            break;

                        continue;
                    }

                    var validCharLen = line.TrimEnd().Length;
                    if (subjectLen > 0)
                        subjectLen += (validCharLen + 1);
                    else
                        subjectLen = validCharLen;
                }
                else if (ch != '\r')
                {
                    lastNonLineBreakCharIdx = i;
                }
            }

            if (lastLineStart < lastNonLineBreakCharIdx)
            {
                var validCharLen = text.Substring(lastLineStart).TrimEnd().Length;
                if (subjectLen > 0)
                    subjectLen += (validCharLen + 1);
                else
                    subjectLen = validCharLen;
            }

            SubjectLength = subjectLen;
            _subjectEndCharIdx = lastNonLineBreakCharIdx;
        }
        else if (change.Property == SubjectLengthProperty || change.Property == SubjectGuideLengthProperty)
        {
            WarnSubjectLength = _subjectLen > _subjectGuideLen;
        }
        else if (change.Property == CaretIndexProperty)
        {
            var text = Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                _suggestionMatchStartIdx = -1;
                Column = 0;
                Suggestions = null;
                return;
            }

            var caretIdx = CaretIndex;
            var startIdx = Math.Max(Math.Min(text.Length - 1, caretIdx - 1), 0);
            var hasWhitespace = false;
            for (var i = startIdx; i >= 0; i--)
            {
                if (i == 0)
                {
                    Column = startIdx + 2;
                    break;
                }

                var ch = text[i];
                if (ch == '\n')
                {
                    Column = startIdx - i + 1;
                    break;
                }

                if (!hasWhitespace)
                    hasWhitespace = char.IsWhiteSpace(ch);
            }

            var suggestionMatchStartIdx = Math.Max(caretIdx - _column + 1, 0);
            if (hasWhitespace || _column == 1 || suggestionMatchStartIdx < _subjectEndCharIdx)
            {
                _suggestionMatchStartIdx = -1;
                Suggestions = null;
                return;
            }

            var editLine = text.Substring(suggestionMatchStartIdx);
            var prefixEndIdx = editLine.IndexOfAny([' ', '\t', '\r', '\n']);
            var prefix = prefixEndIdx > 0 ? editLine.Substring(0, prefixEndIdx) : editLine;
            var matches = new List<CommitMessageTextBoxSuggestion>();
            if (prefix.Length >= 2)
            {
                foreach (var t in _trailers)
                {
                    if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        !editLine.StartsWith(t, StringComparison.Ordinal))
                        matches.Add(new(this, t, suggestionMatchStartIdx, prefix.Length));
                }
            }

            if (matches.Count > 0)
            {
                _suggestionMatchStartIdx = suggestionMatchStartIdx;
                Suggestions = matches;
                SelectedSuggestionIndex = 0;
            }
            else
            {
                _suggestionMatchStartIdx = -1;
                Suggestions = null;
            }
        }
    }

    /// <summary>
    /// フォーカスを失った際の処理。補完候補ポップアップを閉じる。
    /// </summary>
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Suggestions = null;
    }

    /// <summary>
    /// キー押下時の処理。補完候補表示中は上下キー・Enter/Tab・Escapeで候補を操作する。
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_suggestions != null)
        {
            if (e.Key == Key.Up)
            {
                if (_selectedSuggestionIdx > 0)
                    SelectedSuggestionIndex = _selectedSuggestionIdx - 1;
                else
                    SelectedSuggestionIndex = _suggestions.Count - 1;

                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (_selectedSuggestionIdx < _suggestions.Count - 1)
                    SelectedSuggestionIndex = _selectedSuggestionIdx + 1;
                else
                    SelectedSuggestionIndex = 0;

                e.Handled = true;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                var selected = _suggestions[_selectedSuggestionIdx];
                selected.Use();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Suggestions = null;
                e.Handled = true;
            }
        }

        if (!e.Handled)
            base.OnKeyDown(e);
    }

    /// <summary>
    /// LayoutUpdatedイベントのハンドラ。件名終端Y座標・補完候補ポップアップY座標を再計算する。
    /// </summary>
    private void OnLayoutUpdated(object sender, EventArgs e)
    {
        if (_subjectEndCharIdx < 0)
        {
            SubjectEndY = 0;
        }
        else
        {
            var y = _textPresenter?.TextLayout.HitTestTextPosition(_subjectEndCharIdx).Bottom ?? 0.0;
            var offset = _scrollViewer?.Offset.Y ?? 0;
            SubjectEndY = y - offset + 6;

            if (_suggestionMatchStartIdx >= 0)
            {
                var popupY = _textPresenter?.TextLayout.HitTestTextPosition(_suggestionMatchStartIdx).Bottom ?? 0;
                y = popupY - offset;
                if (y < 0.05 || y > Bounds.Height - 0.05)
                {
                    _suggestionMatchStartIdx = -1;
                    SuggestionPopupY = 0;
                    Suggestions = null;
                }
                else
                {
                    SuggestionPopupY = y;
                }
            }
        }
    }

    /// <summary>コミットメッセージのトレーラー（Co-authored-by等）自動補完候補一覧。</summary>
    private readonly List<string> _trailers =
    [
        "Acked-by: ",
        "Assisted-by: ",
        "BREAKING CHANGE: ",
        "Co-authored-by: ",
        "Fixes: ",
        "Helped-by: ",
        "Issue: ",
        "Milestone: ",
        "on-behalf-of: @",
        "Reference-to: ",
        "Refs: ",
        "Reviewed-by: ",
        "See-also: ",
        "Signed-off-by: ",
    ];

    /// <summary>テキスト描画に使うTextPresenterへの参照。件名終端・補完候補位置算出に使用。</summary>
    private TextPresenter _textPresenter = null;

    /// <summary>スクロールビューアへの参照。垂直スクロールオフセットの取得に使用。</summary>
    private ScrollViewer _scrollViewer = null;

    /// <summary>キャレットの現在列番号（内部保持値）。</summary>
    private int _column = 0;

    /// <summary>件名の文字数（内部保持値）。</summary>
    private int _subjectLen = 0;

    /// <summary>件名のガイドライン文字数（内部保持値）。</summary>
    private int _subjectGuideLen = 0;

    /// <summary>件名（subject）の終端文字インデックス。区切り線の描画位置算出に使用。</summary>
    private int _subjectEndCharIdx = -1;

    /// <summary>件名終端のY座標（内部保持値）。</summary>
    private double _subjectEndY = 0;

    /// <summary>件名長警告状態（内部保持値）。</summary>
    private bool _warnSubjectLen = false;

    /// <summary>補完候補マッチ開始文字インデックス。マッチなしの場合は-1。</summary>
    private int _suggestionMatchStartIdx = -1;

    /// <summary>現在の補完候補一覧（内部保持値）。</summary>
    private List<CommitMessageTextBoxSuggestion> _suggestions = null;

    /// <summary>選択中の補完候補インデックス（内部保持値）。</summary>
    private int _selectedSuggestionIdx = 0;

    /// <summary>補完候補ポップアップのY座標（内部保持値）。</summary>
    private double _suggestionPopupY = 0;
}

/// <summary>
/// コミットメッセージ入力欄の件名（subject）終端位置を示す区切り線インジケーター。
/// <see cref="CommitMessageTextBox"/>と重ねて配置し、入力の妨げにならないよう非ヒットテスト対象とする。
/// </summary>
public class CommitMessageSubjectEndIndicator : Control
{
    /// <summary>「SUBJECT END」表示に使うフォントファミリーを保持するスタイルプロパティ。</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<CommitMessageSubjectEndIndicator, FontFamily>(nameof(FontFamily));

    /// <summary>「SUBJECT END」表示に使うフォントファミリー。</summary>
    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>区切り線のブラシを保持するスタイルプロパティ。</summary>
    public static readonly StyledProperty<IBrush> LineBrushProperty =
        AvaloniaProperty.Register<CommitMessageSubjectEndIndicator, IBrush>(nameof(LineBrush), Brushes.Gray);

    /// <summary>件名と本文を区切る破線のブラシ。</summary>
    public IBrush LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    /// <summary>件名終端のY座標を保持するダイレクトプロパティ。</summary>
    public static readonly DirectProperty<CommitMessageSubjectEndIndicator, double> SubjectEndYProperty =
        AvaloniaProperty.RegisterDirect<CommitMessageSubjectEndIndicator, double>(
            nameof(SubjectEndY),
            o => o.SubjectEndY,
            (o, v) => o.SubjectEndY = v);

    /// <summary>件名終端のY座標。<see cref="CommitMessageTextBox.SubjectEndY"/>からバインドされる。</summary>
    public double SubjectEndY
    {
        get => _subjectEndY;
        set => SetAndRaise(SubjectEndYProperty, ref _subjectEndY, value);
    }

    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public CommitMessageSubjectEndIndicator()
    {
        IsHitTestVisible = false;
    }

    /// <summary>
    /// コントロールの描画処理を行う。件名終端に破線と「SUBJECT END」ラベルを描画する。
    /// </summary>
    public override void Render(DrawingContext context)
    {
        var y = SubjectEndY;
        if (y < 0.05 || y > Bounds.Height - 0.05)
            return;

        var font = FontFamily ?? FontFamily.Default;
        var pen = new Pen(LineBrush) { DashStyle = DashStyle.Dash };
        var w = Bounds.Width;

        var subjectEndTip = new FormattedText(
            "SUBJECT END",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(font, FontStyle.Italic),
            10,
            Brushes.Gray);
        context.DrawLine(pen, new Point(0, y), new Point(w, y));
        context.DrawText(subjectEndTip, new Point(w - subjectEndTip.WidthIncludingTrailingWhitespace - 18, y + 1));
    }

    /// <summary>
    /// プロパティが変更された際の処理。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SubjectEndYProperty)
            InvalidateVisual();
    }

    /// <summary>件名終端のY座標（内部保持値）。</summary>
    private double _subjectEndY = 0;
}

/// <summary>
/// コミットメッセージツールボックス（テンプレート等）のコードビハインド。
/// </summary>
public partial class CommitMessageToolBox : UserControl
{
    /// <summary>高度なオプション（テンプレート・AI・Conventional Commit）を表示するかのダイレクトプロパティ。</summary>
    public static readonly DirectProperty<CommitMessageToolBox, bool> ShowAdvancedOptionsProperty =
        AvaloniaProperty.RegisterDirect<CommitMessageToolBox, bool>(
            nameof(ShowAdvancedOptions),
            o => o.ShowAdvancedOptions,
            (o, v) => o.ShowAdvancedOptions = v);

    /// <summary>高度なオプション（テンプレート・AI・Conventional Commit）を表示するか。</summary>
    public bool ShowAdvancedOptions
    {
        get => _showAdvancedOptions;
        set => SetAndRaise(ShowAdvancedOptionsProperty, ref _showAdvancedOptions, value);
    }

    /// <summary>コミットメッセージを保持するダイレクトプロパティ。</summary>
    public static readonly DirectProperty<CommitMessageToolBox, string> CommitMessageProperty =
        AvaloniaProperty.RegisterDirect<CommitMessageToolBox, string>(
            nameof(CommitMessage),
            o => o.CommitMessage,
            (o, v) => o.CommitMessage = v);

    /// <summary>コミットメッセージ本文。テンプレートやAI生成時に設定される。</summary>
    public string CommitMessage
    {
        get => _commitMessage;
        set => SetAndRaise(CommitMessageProperty, ref _commitMessage, value);
    }

    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public CommitMessageToolBox()
    {
        InitializeComponent();
    }

    /// <summary>
    /// SuggestionTappedイベントのハンドラ。補完候補をタップで確定する。
    /// </summary>
    private void OnSuggestionTapped(object sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: CommitMessageTextBoxSuggestion suggestion })
            suggestion.Use();

        e.Handled = true;
    }

    /// <summary>
    /// OpenCommitMessagePickerイベントのハンドラ。
    /// </summary>
    private async void OnOpenCommitMessagePicker(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && DataContext is ViewModels.WorkingCopy vm && ShowAdvancedOptions)
        {
            var repo = vm.Repository;
            var foreground = this.FindResource("Brush.FG1") as IBrush;

            var menu = new ContextMenu();
            menu.MaxWidth = 480;

            var gitTemplate = await new Commands.Config(repo.FullPath).GetAsync("commit.template");
            var templateCount = repo.Settings.CommitTemplates.Count;
            if (templateCount == 0 && string.IsNullOrEmpty(gitTemplate))
            {
                menu.Items.Add(new MenuItem()
                {
                    Header = App.Text("WorkingCopy.NoCommitTemplates"),
                    Icon = App.CreateMenuIcon("Icons.Code"),
                    IsEnabled = false
                });
            }
            else
            {
                for (int i = 0; i < templateCount; i++)
                {
                    var icon = App.CreateMenuIcon("Icons.Code");
                    icon.Fill = foreground;

                    var template = repo.Settings.CommitTemplates[i];
                    var item = new MenuItem();
                    item.Header = App.Text("WorkingCopy.UseCommitTemplate", template.Name);
                    item.Icon = icon;
                    item.Click += (_, ev) =>
                    {
                        vm.ApplyCommitMessageTemplate(template);
                        ev.Handled = true;
                    };
                    menu.Items.Add(item);
                }

                if (!string.IsNullOrEmpty(gitTemplate))
                {
                    if (!Path.IsPathRooted(gitTemplate))
                        gitTemplate = Native.OS.GetAbsPath(repo.FullPath, gitTemplate);

                    var friendlyName = gitTemplate;
                    if (!OperatingSystem.IsWindows())
                    {
                        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        var prefixLen = home.EndsWith('/') ? home.Length - 1 : home.Length;
                        if (gitTemplate.StartsWith(home, StringComparison.Ordinal))
                            friendlyName = $"~{gitTemplate.AsSpan(prefixLen)}";
                    }

                    var icon = App.CreateMenuIcon("Icons.Code");
                    icon.Fill = foreground;

                    var gitTemplateItem = new MenuItem();
                    gitTemplateItem.Header = App.Text("WorkingCopy.UseCommitTemplate", friendlyName);
                    gitTemplateItem.Icon = icon;
                    gitTemplateItem.Click += (_, ev) =>
                    {
                        if (File.Exists(gitTemplate))
                            vm.CommitMessage = File.ReadAllText(gitTemplate);
                        ev.Handled = true;
                    };
                    menu.Items.Add(gitTemplateItem);
                }
            }

            menu.Items.Add(new MenuItem() { Header = "-" });

            var historiesCount = repo.Settings.CommitMessages.Count;
            if (historiesCount == 0)
            {
                menu.Items.Add(new MenuItem()
                {
                    Header = App.Text("WorkingCopy.NoCommitHistories"),
                    Icon = App.CreateMenuIcon("Icons.Histories"),
                    IsEnabled = false
                });
            }
            else
            {
                for (int i = 0; i < historiesCount; i++)
                {
                    var dup = repo.Settings.CommitMessages[i].Trim();
                    var header = new TextBlock()
                    {
                        Text = dup.ReplaceLineEndings(" "),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };

                    var icon = App.CreateMenuIcon("Icons.Histories");
                    icon.Fill = foreground;

                    var item = new MenuItem();
                    item.Header = header;
                    item.Icon = icon;
                    item.Click += (_, ev) =>
                    {
                        vm.CommitMessage = dup;
                        ev.Handled = true;
                    };

                    menu.Items.Add(item);
                }

                menu.Items.Add(new MenuItem() { Header = "-" });

                var clearIcon = App.CreateMenuIcon("Icons.Clear");
                clearIcon.Fill = foreground;

                var clearHistoryItem = new MenuItem();
                clearHistoryItem.Header = App.Text("WorkingCopy.ClearCommitHistories");
                clearHistoryItem.Icon = clearIcon;
                clearHistoryItem.Click += async (_, ev) =>
                {
                    await vm.ClearCommitMessageHistoryAsync();
                    ev.Handled = true;
                };

                menu.Items.Add(clearHistoryItem);
            }

            button.IsEnabled = false;
            menu.Placement = PlacementMode.TopEdgeAlignedLeft;
            menu.Closed += (_, _) => button.IsEnabled = true;
            menu.Open(button);
        }

        e.Handled = true;
    }

    /// <summary>
    /// OpenOpenAIHelperイベントのハンドラ。
    /// </summary>
    private void OnOpenOpenAIHelper(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.WorkingCopy vm && sender is Button button && ShowAdvancedOptions)
        {
            var repo = vm.Repository;

            if (vm.Staged is null || vm.Staged.Count == 0)
            {
                App.RaiseException(repo.FullPath, App.Text("Error.NoFilesForCommit"));
                e.Handled = true;
                return;
            }

            var services = repo.GetPreferredOpenAIServices();
            if (services.Count == 0)
            {
                App.RaiseException(repo.FullPath, App.Text("Error.BadOpenAIConfig"));
                e.Handled = true;
                return;
            }

            if (services.Count == 1)
            {
                DoOpenAIAssistant(repo, services[0], vm.Staged);
                e.Handled = true;
                return;
            }

            var menu = new ContextMenu();
            foreach (var service in services)
            {
                var dup = service;
                var item = new MenuItem();
                item.Header = service.Name;
                item.Click += (_, ev) =>
                {
                    DoOpenAIAssistant(repo, dup, vm.Staged);
                    ev.Handled = true;
                };

                menu.Items.Add(item);
            }

            button.IsEnabled = false;
            menu.Placement = PlacementMode.TopEdgeAlignedLeft;
            menu.Closed += (_, _) => button.IsEnabled = true;
            menu.Open(button);
        }

        e.Handled = true;
    }

    /// <summary>
    /// OpenConventionalCommitHelperイベントのハンドラ。
    /// </summary>
    private void OnOpenConventionalCommitHelper(object _, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;

        var conventionalTypesOverride = owner switch
        {
            Launcher { DataContext: ViewModels.Launcher { ActivePage: { Data: ViewModels.Repository repo } } } => repo.Settings.ConventionalTypesOverride,
            RepositoryConfigure { DataContext: ViewModels.RepositoryConfigure config } => config.ConventionalTypesOverride,
            CommitMessageEditor editor => editor.ConventionalTypesOverride,
            _ => string.Empty
        };

        var vm = new ViewModels.ConventionalCommitMessageBuilder(conventionalTypesOverride, text => CommitMessage = text);
        var builder = new ConventionalCommitMessageBuilder() { DataContext = vm };
        builder.Show(owner);

        e.Handled = true;
    }

    /// <summary>
    /// AIAssistant を non-modal（Show()）で開く共通ヘルパー。
    /// ShowDialog（modal）を廃止し、生成中もメインウィンドウを操作可能にする。
    /// </summary>
    private void DoOpenAIAssistant(ViewModels.Repository repo, AI.Service service, System.Collections.Generic.List<Models.Change> changes)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
            return;

        var assistant = new ViewModels.AIAssistant(repo, service, changes);
        var view = new AIAssistant() { DataContext = assistant };
        view.Show(owner);
    }

    /// <summary>コミットメッセージ本文（内部保持値）。</summary>
    private string _commitMessage = string.Empty;

    /// <summary>高度なオプション表示状態（内部保持値）。</summary>
    private bool _showAdvancedOptions = false;
}
