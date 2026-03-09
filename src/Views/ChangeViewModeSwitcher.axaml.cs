using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Komorebi.Views
{
    /// <summary>
    ///     変更ビューモード切替コントロールのコードビハインド。
    /// </summary>
    public partial class ChangeViewModeSwitcher : UserControl
    {
        public static readonly StyledProperty<Models.ChangeViewMode> ViewModeProperty =
            AvaloniaProperty.Register<ChangeViewModeSwitcher, Models.ChangeViewMode>(nameof(ViewMode));

        public Models.ChangeViewMode ViewMode
        {
            get => GetValue(ViewModeProperty);
            set => SetValue(ViewModeProperty, value);
        }

        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public ChangeViewModeSwitcher()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     SwitchToListの処理を行う。
        /// </summary>
        private void SwitchToList(object sender, RoutedEventArgs e)
        {
            ViewMode = Models.ChangeViewMode.List;
            e.Handled = true;
        }

        /// <summary>
        ///     SwitchToGridの処理を行う。
        /// </summary>
        private void SwitchToGrid(object sender, RoutedEventArgs e)
        {
            ViewMode = Models.ChangeViewMode.Grid;
            e.Handled = true;
        }

        /// <summary>
        ///     SwitchToTreeの処理を行う。
        /// </summary>
        private void SwitchToTree(object sender, RoutedEventArgs e)
        {
            ViewMode = Models.ChangeViewMode.Tree;
            e.Handled = true;
        }
    }
}
