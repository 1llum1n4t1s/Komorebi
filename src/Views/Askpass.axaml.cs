using System;

using Avalonia.Input;
using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// SSH鍵のパスフレーズ入力ダイアログのコードビハインド。
/// gitがSSH認証時にパスフレーズを要求した際に表示される。
/// </summary>
public partial class Askpass : ChromelessWindow
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public Askpass()
    {
        InitializeComponent();
    }

    /// <summary>
    /// コントロールが読み込まれた際の処理。パスフレーズ入力欄にフォーカスを設定する。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        TxtPassphrase.Focus(NavigationMethod.Directional);
    }

    /// <summary>
    /// ウィンドウを閉じてパスフレーズ未入力として終了する。
    /// </summary>
    private void CloseWindow(object _1, RoutedEventArgs _2)
    {
        // 標準出力にメッセージを出力してgitプロセスに通知する
        Console.Out.WriteLine("No passphrase entered.");
        App.Quit(-1);
    }

    /// <summary>
    /// 入力されたパスフレーズを標準出力に書き出してgitプロセスに渡す。
    /// </summary>
    private void EnterPassword(object _1, RoutedEventArgs _2)
    {
        var passphrase = TxtPassphrase.Text ?? string.Empty;
        // gitはstdoutからパスフレーズを読み取る
        Console.Out.WriteLine(passphrase);
        App.Quit(0);
    }
}
