using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Komorebi.Views;

/// <summary>
/// ワークスペース設定ダイアログのコードビハインド。
/// </summary>
public partial class ConfigureWorkspace : ChromelessWindow
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public ConfigureWorkspace()
    {
        CloseOnESC = true;
        InitializeComponent();
    }

    /// <summary>
    /// ウィンドウが閉じられる際の処理。
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (!Design.IsDesignMode)
            ViewModels.Preferences.Instance.Save();
    }

    /// <summary>
    /// DefaultCloneDirの選択処理を行う。
    /// </summary>
    private async void SelectDefaultCloneDir(object _, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.ConfigureWorkspace workspace || workspace.Selected is null)
            return;

        var options = new FolderPickerOpenOptions() { AllowMultiple = false };
        try
        {
            var selected = await StorageProvider.OpenFolderPickerAsync(options);
            if (selected.Count == 1)
            {
                var folder = selected[0];
                var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                workspace.Selected.DefaultCloneDir = folderPath;
            }
        }
        catch (Exception ex)
        {
            App.RaiseException(string.Empty, App.Text("Error.FailedToSelectCloneDir", ex.Message));
        }

        e.Handled = true;
    }
}
