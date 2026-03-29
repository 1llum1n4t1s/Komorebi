using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace Komorebi.Views;

/// <summary>
/// ディレクトリ履歴ビューのコードビハインド。
/// </summary>
public partial class DirHistories : ChromelessWindow
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public DirHistories()
    {
        InitializeComponent();
    }

    /// <summary>
    /// PressCommitSHAイベントのハンドラ。
    /// </summary>
    private void OnPressCommitSHA(object sender, PointerPressedEventArgs e)
    {
        if (sender is TextBlock { DataContext: Models.Commit commit } &&
            DataContext is ViewModels.DirHistories vm)
        {
            vm.NavigateToCommit(commit);
        }

        e.Handled = true;
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
        if (sender is Border { DataContext: Models.Commit commit } border &&
            DataContext is ViewModels.DirHistories vm)
        {
            var tooltip = ToolTip.GetTip(border);
            if (tooltip is null)
                ToolTip.SetTip(border, vm.GetCommitFullMessage(commit));
        }
    }
}
