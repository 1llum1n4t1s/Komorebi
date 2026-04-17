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
    /// 前回終了時のサイズと位置を復元する（upstream issue #2100 対応）。
    /// </summary>
    public FileHistories()
    {
        InitializeComponent();
        PositionChanged += OnPositionChanged;

        // 前回のウィンドウサイズを復元する
        var layout = ViewModels.Preferences.Instance.Layout;
        Width = layout.FileHistoriesWidth;
        Height = layout.FileHistoriesHeight;

        // 前回のウィンドウ位置がスクリーン内に収まる場合のみ復元する（複数モニタ対応）
        var x = layout.FileHistoriesPositionX;
        var y = layout.FileHistoriesPositionY;
        if (x != int.MinValue && y != int.MinValue && Screens is { } screens)
        {
            var position = new PixelPoint(x, y);
            var size = new PixelSize((int)layout.FileHistoriesWidth, (int)layout.FileHistoriesHeight);
            var desiredRect = new PixelRect(position, size);
            for (var i = 0; i < screens.ScreenCount; i++)
            {
                var screen = screens.All[i];
                if (screen.WorkingArea.Contains(desiredRect))
                {
                    Position = position;
                    return;
                }
            }
        }

        // スクリーン外の場合はオーナー中央に表示する
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // 前回の最大化状態を復元する
        var state = ViewModels.Preferences.Instance.Layout.FileHistoriesWindowState;
        if (state == WindowState.Maximized || state == WindowState.FullScreen)
            WindowState = WindowState.Maximized;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            var state = (WindowState)change.NewValue!;
            ViewModels.Preferences.Instance.Layout.FileHistoriesWindowState = state;
        }
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        // 通常ウィンドウ状態の場合のみサイズを保存する
        if (WindowState == WindowState.Normal)
        {
            var layout = ViewModels.Preferences.Instance.Layout;
            layout.FileHistoriesWidth = Width;
            layout.FileHistoriesHeight = Height;
        }
    }

    /// <summary>
    /// ウィンドウ位置変更時のハンドラ。通常状態の場合のみ位置を保存する。
    /// </summary>
    private void OnPositionChanged(object sender, PixelPointEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            var layout = ViewModels.Preferences.Instance.Layout;
            layout.FileHistoriesPositionX = Position.X;
            layout.FileHistoriesPositionY = Position.Y;
        }
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
