using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// モーダルダイアログ内で発生したエラー／情報を、そのダイアログの上にさらに別モーダルで表示する小ウィンドウ。
/// 通常の通知バナー（<see cref="App.RaiseException(string, string)"/>）は親ウィンドウに表示されるため、
/// 既に開いている Preferences 等のモーダルダイアログの裏側に隠れてしまう問題を解消する。
/// </summary>
public partial class Alert : ChromelessWindow
{
    /// <summary>
    /// コンストラクタ。ESC キーで閉じられるように構成する。
    /// </summary>
    public Alert()
    {
        CloseOnESC = true;
        InitializeComponent();
    }

    /// <summary>
    /// 指定したオーナーウィンドウの上にモーダルで表示する。
    /// </summary>
    /// <param name="owner">オーナーとなる親ウィンドウ（通常は呼び出し元のモーダルダイアログ）</param>
    /// <param name="message">表示するメッセージ本文</param>
    /// <param name="isError">true ならエラー表示（タイトル: ERROR）、false なら情報表示（タイトル: NOTICE）</param>
    public async Task ShowAsync(Window owner, string message, bool isError)
    {
        var title = isError ? App.Text("Launcher.Error") : App.Text("Launcher.Info");
        Title = title;
        TxtTitle.Text = title;
        Message.Text = message;
        await ShowDialog(owner);
    }

    /// <summary>
    /// OK ボタン押下時。ウィンドウを閉じる。
    /// </summary>
    private void OnOk(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
