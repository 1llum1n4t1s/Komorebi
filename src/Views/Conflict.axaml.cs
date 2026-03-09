using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Komorebi.Views
{
    /// <summary>
    ///     コンフリクト解決ダイアログのコードビハインド。
    /// </summary>
    public partial class Conflict : UserControl
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public Conflict()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     PressedSHAイベントのハンドラ。
        /// </summary>
        private void OnPressedSHA(object sender, PointerPressedEventArgs e)
        {
            var repoView = this.FindAncestorOfType<Repository>();
            if (repoView is { DataContext: ViewModels.Repository repo } && sender is TextBlock text)
                repo.NavigateToCommit(text.Text);

            e.Handled = true;
        }

        /// <summary>
        ///     UseTheirsイベントのハンドラ。
        /// </summary>
        private async void OnUseTheirs(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Conflict vm)
                await vm.UseTheirsAsync();

            e.Handled = true;
        }

        /// <summary>
        ///     UseMineイベントのハンドラ。
        /// </summary>
        private async void OnUseMine(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Conflict vm)
                await vm.UseMineAsync();

            e.Handled = true;
        }

        /// <summary>
        ///     Mergeイベントのハンドラ。
        /// </summary>
        private async void OnMerge(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Conflict vm)
                await vm.MergeAsync();

            e.Handled = true;
        }

        /// <summary>
        ///     MergeExternalイベントのハンドラ。
        /// </summary>
        private async void OnMergeExternal(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Conflict vm)
                await vm.MergeExternalAsync();

            e.Handled = true;
        }
    }
}
