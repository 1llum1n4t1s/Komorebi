using Avalonia.Controls;
using Avalonia.Input;

namespace Komorebi.Views;

/// <summary>
///     ファイルを開くコマンドパレットのコードビハインド。
/// </summary>
public partial class OpenFileCommandPalette : UserControl
{
    /// <summary>
    ///     コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public OpenFileCommandPalette()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     キーが押された際のイベント処理。
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not ViewModels.OpenFileCommandPalette vm)
            return;

        if (e.Key == Key.Enter)
        {
            vm.Launch();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (FileListBox.IsKeyboardFocusWithin)
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
                if (vm.VisibleFiles.Count > 0)
                    FileListBox.Focus(NavigationMethod.Directional);

                e.Handled = true;
                return;
            }

            if (FileListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
            {
                FilterTextBox.Focus(NavigationMethod.Directional);
                e.Handled = true;
                return;
            }
        }
    }

    /// <summary>
    ///     ItemTappedイベントのハンドラ。
    /// </summary>
    private void OnItemTapped(object sender, TappedEventArgs e)
    {
        if (DataContext is ViewModels.OpenFileCommandPalette vm)
        {
            vm.Launch();
            e.Handled = true;
        }
    }
}
