using Avalonia.Input;
using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// 新しいリモートブランチへのプッシュ先を指定するダイアログ。
/// </summary>
public partial class PushToNewBranch : ChromelessWindow
{
    /// <summary>
    /// コンストラクタ。ESCキーで閉じる設定を有効化し、コンポーネントを初期化する。
    /// </summary>
    public PushToNewBranch()
    {
        CloseOnESC = true;
        InitializeComponent();
    }

    /// <summary>
    /// ウィンドウ読み込み完了時にブランチ名入力欄へフォーカスを設定する。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        TxtName.Focus(NavigationMethod.Directional);
    }

    /// <summary>
    /// 確定ボタン押下時に入力されたブランチ名を結果として返してダイアログを閉じる。
    /// </summary>
    private void OnSure(object _1, RoutedEventArgs _2)
    {
        if (DataContext is ViewModels.PushToNewBranch vm && vm.Check())
            Close(vm.BranchName);
    }

    /// <summary>
    /// キャンセルボタン押下時に空文字を結果として返してダイアログを閉じる。
    /// </summary>
    private void OnCancel(object _1, RoutedEventArgs _2)
    {
        Close(string.Empty);
    }
}
