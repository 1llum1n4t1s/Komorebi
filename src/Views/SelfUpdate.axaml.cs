using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Komorebi.Views
{
    /// <summary>
    ///     自動更新（Velopack）ダイアログのコードビハインド。
    /// </summary>
    public partial class SelfUpdate : ChromelessWindow
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public SelfUpdate()
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

            if (DataContext is ViewModels.SelfUpdate vm)
                vm.CancelDownload();
        }

        /// <summary>
        ///     CloseWindowの処理を行う。
        /// </summary>
        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close();
        }

        /// <summary>
        ///     DownloadAndInstallの処理を行う。
        /// </summary>
        private void DownloadAndInstall(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.VelopackUpdate update } &&
                DataContext is ViewModels.SelfUpdate vm)
            {
                vm.DownloadAndApplyUpdate(update);
            }

            e.Handled = true;
        }

        /// <summary>
        ///     IgnoreThisVelopackVersionの処理を行う。
        /// </summary>
        private void IgnoreThisVelopackVersion(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.VelopackUpdate update })
                ViewModels.Preferences.Instance.IgnoreUpdateTag = update.TagName;

            Close();
            e.Handled = true;
        }
    }
}
