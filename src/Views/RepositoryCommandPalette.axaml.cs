using Avalonia.Controls;
using Avalonia.Input;

namespace Komorebi.Views
{
    /// <summary>
    ///     リポジトリ操作コマンドパレットのコードビハインド。
    /// </summary>
    public partial class RepositoryCommandPalette : UserControl
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public RepositoryCommandPalette()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     キーが押された際のイベント処理。
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.RepositoryCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                vm.Exec();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (CmdListBox.IsKeyboardFocusWithin)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.Down || e.Key == Key.Tab)
            {
                if (FilterTextBox.IsKeyboardFocusWithin)
                {
                    if (vm.VisibleCmds.Count > 0)
                        CmdListBox.Focus(NavigationMethod.Directional);

                    e.Handled = true;
                    return;
                }

                if (CmdListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
        }

        /// <summary>
        ///     ItemTappedイベントのハンドラ。
        /// </summary>
        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.RepositoryCommandPalette vm)
            {
                vm.Exec();
                e.Handled = true;
            }
        }
    }
}
