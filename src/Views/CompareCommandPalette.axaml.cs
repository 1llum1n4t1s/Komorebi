using Avalonia.Controls;
using Avalonia.Input;

namespace Komorebi.Views;

/// <summary>
/// 比較コマンドパレットのコードビハインド。
/// </summary>
public partial class CompareCommandPalette : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public CompareCommandPalette()
    {
        InitializeComponent();
    }

    /// <summary>
    /// キーが押された際のイベント処理。
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not ViewModels.CompareCommandPalette vm)
            return;

        if (e.Key == Key.Enter)
        {
            vm.Launch();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (RefsListBox.IsKeyboardFocusWithin)
            {
                FilterTextBox.Focus(NavigationMethod.Directional);
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Key.Down || e.Key == Key.Tab)
        {
            if (FilterTextBox.IsKeyboardFocusWithin)
            {
                if (vm.Refs.Count > 0)
                    RefsListBox.Focus(NavigationMethod.Directional);

                e.Handled = true;
                return;
            }

            if (RefsListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
            {
                FilterTextBox.Focus(NavigationMethod.Directional);
                e.Handled = true;
                return;
            }
        }
    }

    /// <summary>
    /// ItemTappedイベントのハンドラ。
    /// </summary>
    private void OnItemTapped(object sender, TappedEventArgs e)
    {
        if (DataContext is ViewModels.CompareCommandPalette vm)
        {
            vm.Launch();
            e.Handled = true;
        }
    }
}
