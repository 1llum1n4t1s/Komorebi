using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Komorebi.Views
{
    /// <summary>
    ///     リビジョン移動セレクタのコードビハインド。
    /// </summary>
    public partial class GotoRevisionSelector : ChromelessWindow
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public GotoRevisionSelector()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        /// <summary>
        ///     コントロールが読み込まれた際の処理。
        /// </summary>
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            RevisionList.Focus();
        }

        /// <summary>
        ///     ListKeyDownイベントのハンドラ。
        /// </summary>
        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (e is not { Key: Key.Enter, KeyModifiers: KeyModifiers.None })
                return;

            if (sender is not ListBox { SelectedItem: Models.Commit commit })
                return;

            Close(commit);
            e.Handled = true;
        }

        /// <summary>
        ///     ListItemTappedイベントのハンドラ。
        /// </summary>
        private void OnListItemTapped(object sender, TappedEventArgs e)
        {
            if (sender is not Control { DataContext: Models.Commit commit })
                return;

            Close(commit);
            e.Handled = true;
        }
    }
}

