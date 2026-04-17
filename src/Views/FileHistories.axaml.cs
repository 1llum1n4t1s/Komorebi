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
    /// 前回終了時のサイズは constructor で、位置は <see cref="OnOpened"/> で復元する
    /// （upstream issue #2100 対応）。
    /// </summary>
    public FileHistories()
    {
        InitializeComponent();
        PositionChanged += OnPositionChanged;

        // このウィンドウは OnOpened 内で自前の位置復元を行うため、App.ShowWindow の
        // 「アクティブスクリーン中央に配置」処理を抑止する（上書き競合回避）。
        SuppressShowWindowCentering = true;

        // サイズは constructor 段階で設定して構わない（Screens を参照しないため）
        var layout = ViewModels.Preferences.Instance.Layout;
        Width = layout.FileHistoriesWidth;
        Height = layout.FileHistoriesHeight;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Avalonia 11 では <see cref="WindowBase.Screens"/> が constructor 時点で null の場合があるため、
    /// ウィンドウ位置の復元は OnOpened で実施する（coderabbit PR #17 レビュー対応）。
    /// </remarks>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        var layout = ViewModels.Preferences.Instance.Layout;

        // 前回の最大化状態を復元する
        var state = layout.FileHistoriesWindowState;
        if (state == WindowState.Maximized || state == WindowState.FullScreen)
            WindowState = WindowState.Maximized;

        // 前回のウィンドウ位置がスクリーン内に収まる場合のみ復元する（複数モニタ対応）
        if (!TryRestoreWindowPosition(
                layout.FileHistoriesPositionX,
                layout.FileHistoriesPositionY,
                layout.FileHistoriesWidth,
                layout.FileHistoriesHeight))
        {
            // 復元できない場合はオーナー中央に配置する（App.ShowWindow の中央寄せは抑止されているため、ここで補完）
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            var state = (WindowState)change.NewValue!;
            // Minimized を保存してしまうと次回起動時に最小化で復元されて困るのでフィルタする
            // （ユーザーがタスクバーから最小化しただけで Maximized が失われないように）
            if (state != WindowState.Minimized)
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
