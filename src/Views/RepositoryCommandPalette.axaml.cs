using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// リポジトリコマンドパレットのコードビハインド。キーボード操作でコマンドを検索・実行する。
/// </summary>
public partial class RepositoryCommandPalette : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public RepositoryCommandPalette()
    {
        InitializeComponent();
    }

    /// <summary>
    /// コントロール読み込み時にフィルタ入力欄へフォーカスを設定する。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        FilterTextBox.Focus(NavigationMethod.Directional);
    }

    /// <summary>
    /// キー押下時のナビゲーション処理。Enterで実行、Up/Down/Tab でフォーカス移動する。
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not ViewModels.RepositoryCommandPalette vm)
            return;

        if (e.Key == Key.Enter)
        {
            vm.Exec();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            // コマンドリストからフィルタ入力欄へフォーカスを戻す
            if (CmdListBox.IsKeyboardFocusWithin)
            {
                FilterTextBox.Focus(NavigationMethod.Directional);
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Key.Down || e.Key == Key.Tab)
        {
            // フィルタ入力欄からコマンドリストへフォーカスを移動
            if (FilterTextBox.IsKeyboardFocusWithin)
            {
                if (vm.VisibleCmds.Count > 0)
                    CmdListBox.Focus(NavigationMethod.Directional);

                e.Handled = true;
                return;
            }

            // Tab キーでコマンドリストからフィルタ入力欄へ戻る
            if (CmdListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
            {
                FilterTextBox.Focus(NavigationMethod.Directional);
                e.Handled = true;
                return;
            }
        }
    }

    /// <summary>
    /// コマンドリスト項目タップ時に選択中のコマンドを実行する。
    /// </summary>
    private void OnItemTapped(object sender, TappedEventArgs e)
    {
        if (DataContext is ViewModels.RepositoryCommandPalette vm)
        {
            vm.Exec();
            e.Handled = true;
        }
    }
}
