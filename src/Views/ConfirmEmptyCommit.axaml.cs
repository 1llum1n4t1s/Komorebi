using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// ステージ済みファイルが無い状態でのコミット確認ダイアログのコードビハインド。
/// 選択ファイルのステージ、全ファイルのステージ、空コミット作成、キャンセルの4択を提示する。
/// </summary>
public partial class ConfirmEmptyCommit : ChromelessWindow
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public ConfirmEmptyCommit()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 選択中のファイルをステージしてからコミットする。
    /// </summary>
    private void StageSelectedThenCommit(object _1, RoutedEventArgs _2)
    {
        Close(Models.ConfirmEmptyCommitResult.StageSelectedAndCommit);
    }

    /// <summary>
    /// 全ファイルをステージしてからコミットする。
    /// </summary>
    private void StageAllThenCommit(object _1, RoutedEventArgs _2)
    {
        Close(Models.ConfirmEmptyCommitResult.StageAllAndCommit);
    }

    /// <summary>
    /// 空コミットを作成する。
    /// </summary>
    private void Continue(object _1, RoutedEventArgs _2)
    {
        Close(Models.ConfirmEmptyCommitResult.CreateEmptyCommit);
    }

    /// <summary>
    /// ダイアログを閉じてコミットをキャンセルする。
    /// </summary>
    private void CloseWindow(object _1, RoutedEventArgs _2)
    {
        Close(Models.ConfirmEmptyCommitResult.Cancel);
    }
}
