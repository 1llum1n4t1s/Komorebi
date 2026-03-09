using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace Komorebi.Views
{
    /// <summary>
    ///     カスタムアクション実行ダイアログのコードビハインド。
    /// </summary>
    public partial class ExecuteCustomAction : UserControl
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public ExecuteCustomAction()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     コントロールが読み込まれた際の処理。
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var inputs = this.GetVisualDescendants();
            foreach (var input in inputs)
            {
                if (input is InputElement { Focusable: true, IsTabStop: true } focusable)
                {
                    focusable.Focus();
                    return;
                }
            }
        }

        /// <summary>
        ///     Pathの選択処理を行う。
        /// </summary>
        private async void SelectPath(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var control = sender as Control;

            if (control?.DataContext is not ViewModels.CustomActionControlPathSelector selector)
                return;

            if (selector.IsFolder)
            {
                try
                {
                    var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                    var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                    if (selected.Count == 1)
                    {
                        var folder = selected[0];
                        var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                        selector.Path = folderPath;
                    }
                }
                catch (Exception exception)
                {
                    App.RaiseException(string.Empty, $"Failed to select parent folder: {exception.Message}");
                }
            }
            else
            {
                var options = new FilePickerOpenOptions()
                {
                    AllowMultiple = false,
                    FileTypeFilter = [new FilePickerFileType("File") { Patterns = ["*.*"] }]
                };

                var selected = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                if (selected.Count == 1)
                    selector.Path = selected[0].Path.LocalPath;
            }

            e.Handled = true;
        }
    }
}
