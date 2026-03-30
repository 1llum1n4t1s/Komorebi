using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Komorebi.Views;

/// <summary>
/// ファイル履歴ビューのコードビハインド。
/// </summary>
public partial class FileHistories : ChromelessWindow
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public FileHistories()
    {
        InitializeComponent();
    }

    /// <summary>
    /// リビジョンリストのItemsSourceが変更された際、最初のアイテムを自動選択する。
    /// </summary>
    private void OnRevisionsPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ListBox.ItemsSourceProperty &&
            sender is ListBox { Items: { Count: > 0 } } listBox)
            listBox.SelectedIndex = 0;
    }

    /// <summary>
    /// リビジョンリストの選択が変更された際のハンドラ。
    /// </summary>
    private void OnRevisionsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && DataContext is ViewModels.FileHistories vm)
        {
            vm.SelectedRevisions.Clear();
            if (listBox.SelectedItems is { } selected)
            {
                foreach (var item in selected)
                {
                    if (item is Models.FileVersion ver)
                        vm.SelectedRevisions.Add(ver);
                }
            }
        }
    }

    /// <summary>
    /// PressCommitSHAイベントのハンドラ。
    /// </summary>
    private void OnPressCommitSHA(object sender, PointerPressedEventArgs e)
    {
        if (sender is TextBlock { DataContext: Models.FileVersion ver } &&
            DataContext is ViewModels.FileHistories vm)
        {
            vm.NavigateToCommit(ver);
        }

        e.Handled = true;
    }

    /// <summary>
    /// ResetToSelectedRevisionイベントのハンドラ。
    /// </summary>
    private async void OnResetToSelectedRevision(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ViewModels.FileHistoriesSingleRevision single })
        {
            await single.ResetToSelectedRevisionAsync();
            NotifyDonePanel.IsVisible = true;
        }

        e.Handled = true;
    }

    /// <summary>
    /// CloseNotifyPanelイベントのハンドラ。
    /// </summary>
    private void OnCloseNotifyPanel(object _, PointerPressedEventArgs e)
    {
        NotifyDonePanel.IsVisible = false;
        e.Handled = true;
    }

    /// <summary>
    /// SaveAsPatchイベントのハンドラ。
    /// </summary>
    private async void OnSaveAsPatch(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ViewModels.FileHistoriesCompareRevisions compare })
        {
            var options = new FilePickerSaveOptions();
            options.Title = App.Text("FileCM.SaveAsPatch");
            options.DefaultExtension = ".patch";
            options.FileTypeChoices = [new FilePickerFileType("Patch File") { Patterns = ["*.patch"] }];

            try
            {
                var storageFile = await StorageProvider.SaveFilePickerAsync(options);
                if (storageFile is not null)
                    await compare.SaveAsPatch(storageFile.Path.LocalPath);

                NotifyDonePanel.IsVisible = true;
            }
            catch (Exception exception)
            {
                App.RaiseException(string.Empty, App.Text("Error.FailedToSaveAsPatch", exception.Message));
            }

            e.Handled = true;
        }
    }

    /// <summary>
    /// CommitSubjectDataContextChangedイベントのハンドラ。
    /// </summary>
    private void OnCommitSubjectDataContextChanged(object sender, EventArgs e)
    {
        if (sender is Border border)
            ToolTip.SetTip(border, null);
    }

    /// <summary>
    /// CommitSubjectPointerMovedイベントのハンドラ。
    /// </summary>
    private void OnCommitSubjectPointerMoved(object sender, PointerEventArgs e)
    {
        if (sender is Border { DataContext: Models.FileVersion ver } border &&
            DataContext is ViewModels.FileHistories vm)
        {
            var tooltip = ToolTip.GetTip(border);
            if (tooltip is null)
                ToolTip.SetTip(border, vm.GetCommitFullMessage(ver));
        }
    }

    /// <summary>
    /// OpenFileWithDefaultEditorイベントのハンドラ。
    /// </summary>
    private async void OnOpenFileWithDefaultEditor(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.FileHistories { ViewContent: ViewModels.FileHistoriesSingleRevision revision })
            await revision.OpenWithDefaultEditorAsync();

        e.Handled = true;
    }
}
