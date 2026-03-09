using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;

namespace Komorebi.Views
{
    /// <summary>
    ///     AIResponseViewクラス。
    /// </summary>
    public class AIResponseView : TextEditor
    {
        public static readonly StyledProperty<string> ContentProperty =
            AvaloniaProperty.Register<AIResponseView, string>(nameof(Content), string.Empty);

        public string Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public AIResponseView() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = true;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
        }

        /// <summary>
        ///     コントロールが読み込まれた際の処理。
        /// </summary>
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            TextArea.TextView.ContextRequested += OnTextViewContextRequested;

            if (_textMate == null)
            {
                _textMate = Models.TextMateHelper.CreateForEditor(this);
                Models.TextMateHelper.SetGrammarByFileName(_textMate, "README.md");
            }
        }

        /// <summary>
        ///     コントロールがアンロードされた際の処理。
        /// </summary>
        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
            }

            GC.Collect();
        }

        /// <summary>
        ///     プロパティが変更された際の処理。
        /// </summary>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ContentProperty)
                Text = Content;
        }

        /// <summary>
        ///     TextViewContextRequestedイベントのハンドラ。
        /// </summary>
        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected))
                return;

            var copy = new MenuItem() { Header = App.Text("Copy") };
            copy.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(selected);
                ev.Handled = true;
            };

            if (this.FindResource("Icons.Copy") is Geometry geo)
            {
                copy.Icon = new Avalonia.Controls.Shapes.Path()
                {
                    Width = 10,
                    Height = 10,
                    Stretch = Stretch.Uniform,
                    Data = geo,
                };
            }

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);

            e.Handled = true;
        }

        private TextMate.Installation _textMate = null;
    }

    /// <summary>
    ///     AIアシスタント（コミットメッセージ生成等）ダイアログのコードビハインド。
    /// </summary>
    public partial class AIAssistant : ChromelessWindow
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public AIAssistant()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        /// <summary>
        ///     ウィンドウが閉じられる際の処理。
        /// </summary>
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            (DataContext as ViewModels.AIAssistant)?.Cancel();
        }

        /// <summary>
        ///     Applyイベントのハンドラ。
        /// </summary>
        private void OnApply(object sender, RoutedEventArgs e)
        {
            (DataContext as ViewModels.AIAssistant)?.Apply();
            Close();
        }
    }
}
