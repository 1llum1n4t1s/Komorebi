using System;
using System.IO;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Komorebi.Views;

/// <summary>
/// ローカルリポジトリを開くダイアログのコードビハインド。
/// フォルダピッカーでフォルダを選択し、ViewModel の RepoPath に反映する。
/// </summary>
public partial class OpenLocalRepository : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public OpenLocalRepository()
    {
        InitializeComponent();
    }

    /// <summary>
    /// フォルダ選択ボタン押下時のハンドラ。
    /// ワークスペースの既定クローンディレクトリを初期位置としてフォルダピッカーを表示する。
    /// </summary>
    private async void OnSelectRepositoryFolder(object _1, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.OpenLocalRepository vm)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        // アクティブワークスペースの既定クローンディレクトリを初期位置にする
        // GetActiveWorkspace() が null を返しうるため防御的に null-conditional アクセスを使用（Clone.cs と同様）
        var preference = ViewModels.Preferences.Instance;
        var workspace = preference.GetActiveWorkspace();
        var initDir = workspace?.DefaultCloneDir;
        if (string.IsNullOrEmpty(initDir) || !Directory.Exists(initDir))
            initDir = preference.GitDefaultCloneDir;

        var options = new FolderPickerOpenOptions() { AllowMultiple = false };
        if (Directory.Exists(initDir))
        {
            var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initDir);
            options.SuggestedStartLocation = folder;
        }

        try
        {
            var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (selected.Count == 1)
            {
                var folder = selected[0];
                vm.RepoPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
            }
        }
        catch (Exception exception)
        {
            App.RaiseException(string.Empty, App.Text("Error.FailedToOpenRepository", exception.Message));
        }

        e.Handled = true;
    }
}
