using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Komorebi.Views
{
    /// <summary>
    ///     リポジトリスキャン（ディスク検索）ダイアログのコードビハインド。
    /// </summary>
    public partial class ScanRepositories : UserControl
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public ScanRepositories()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     SelectRootDirイベントのハンドラ。
        /// </summary>
        private async void OnSelectRootDir(object _, RoutedEventArgs e)
        {
            var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (provider == null)
                return;

            if (DataContext is not ViewModels.ScanRepositories vm)
                return;

            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            try
            {
                var selected = await provider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                {
                    var folder = selected[0];
                    var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                    vm.CustomDir = folderPath;
                }
            }
            catch (Exception ex)
            {
                App.RaiseException(string.Empty, App.Text("Error.FailedToSelectScanDir", ex.Message));
            }

            e.Handled = true;
        }
    }
}
