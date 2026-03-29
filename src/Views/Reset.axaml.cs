using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// リセットダイアログのコードビハインド。
/// </summary>
public partial class Reset : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public Reset()
    {
        InitializeComponent();
    }

    /// <summary>
    /// コントロールが読み込まれた際の処理。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        ResetMode.Focus();
    }

    /// <summary>
    /// ResetModeKeyDownイベントのハンドラ。
    /// </summary>
    private void OnResetModeKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            var key = e.Key.ToString();
            for (int i = 0; i < Models.ResetMode.Supported.Length; i++)
            {
                if (key.Equals(Models.ResetMode.Supported[i].Key, System.StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = i;
                    e.Handled = true;
                    return;
                }
            }
        }
    }
}
