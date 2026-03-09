using Avalonia.Controls;
using Avalonia.Input;

namespace Komorebi.Views
{
    /// <summary>
    ///     Blameコマンドパレットのコードビハインド。
    /// </summary>
    public partial class BlameCommandPalette : UserControl
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public BlameCommandPalette()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     キーが押された際のイベント処理。
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.BlameCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                vm.Launch();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (FileListBox.IsKeyboardFocusWithin)
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
                    if (vm.VisibleFiles.Count > 0)
                        FileListBox.Focus(NavigationMethod.Directional);

                    e.Handled = true;
                    return;
                }

                if (FileListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
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
            if (DataContext is ViewModels.BlameCommandPalette vm)
            {
                vm.Launch();
                e.Handled = true;
            }
        }
    }
}
