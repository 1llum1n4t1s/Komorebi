using System.Diagnostics;

using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// アプリケーション再起動確認ダイアログのコードビハインド。
/// </summary>
public partial class ConfirmRestart : ChromelessWindow
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public ConfirmRestart()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Restartの処理を行う。
    /// </summary>
    private void Restart(object _1, RoutedEventArgs _2)
    {
        var selfExecFile = Process.GetCurrentProcess().MainModule!.FileName;
        Process.Start(selfExecFile);
        App.Quit(-1);
    }
}
