using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Komorebi.Views
{
    public partial class SelfUpdate : ChromelessWindow
    {
        public SelfUpdate()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (DataContext is ViewModels.SelfUpdate vm)
                vm.CancelDownload();
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close();
        }

        private void DownloadAndInstall(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.VelopackUpdate update } &&
                DataContext is ViewModels.SelfUpdate vm)
            {
                vm.DownloadAndApplyUpdate(update);
            }

            e.Handled = true;
        }

        private void IgnoreThisVelopackVersion(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.VelopackUpdate update })
                ViewModels.Preferences.Instance.IgnoreUpdateTag = update.TagName;

            Close();
            e.Handled = true;
        }
    }
}
