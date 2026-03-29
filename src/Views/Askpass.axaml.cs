using System;

using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// SSH認証ダイアログのコードビハインド。
/// ホスト鍵確認（yes/no応答）とパスフレーズ/パスワード入力の両方に対応する。
/// </summary>
public partial class Askpass : ChromelessWindow
{
    private bool _isHostKeyPrompt;
    private bool _isPreview;

    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public Askpass()
    {
        InitializeComponent();
    }

    /// <summary>
    /// プレビューモードかどうか。trueの場合、OK/キャンセルでアプリを終了せずウィンドウを閉じるだけにする。
    /// </summary>
    public bool IsPreview
    {
        get => _isPreview;
        set => _isPreview = value;
    }

    /// <summary>
    /// コントロールが読み込まれた際の処理。パスフレーズ入力欄にフォーカスを設定する。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (!_isHostKeyPrompt)
            TxtPassphrase.Focus(NavigationMethod.Directional);
    }

    /// <summary>
    /// SSHメッセージを解析し、対応するローカライズ済みヒントを表示する。
    /// ホスト鍵確認の場合はパスワード入力欄を非表示にする。
    /// </summary>
    public void SetHint(string sshMessage)
    {
        var hintKey = Models.GitErrorHelper.GetAskpassHintKey(sshMessage);

        // ホスト鍵プロンプトの場合、入力欄を隠す
        _isHostKeyPrompt = hintKey is "Text.Askpass.Hint.HostKeyNew" or "Text.Askpass.Hint.HostKeyChanged";
        if (_isHostKeyPrompt)
            TxtPassphrase.IsVisible = false;

        // ホスト鍵変更警告の場合、接続不可なのでOKボタンも隠す
        if (hintKey is "Text.Askpass.Hint.HostKeyChanged")
            BtnOk.IsVisible = false;

        if (string.IsNullOrEmpty(hintKey))
            return;

        if (Application.Current?.TryGetResource(hintKey, null, out var value) is true && value is string hint)
        {
            TxtHint.Text = hint;
            HintBorder.IsVisible = true;
        }
    }

    /// <summary>
    /// OKボタン押下時の処理。
    /// ホスト鍵確認の場合は"yes"を、それ以外は入力されたパスフレーズを標準出力に書き出す。
    /// </summary>
    private void EnterPassword(object _1, RoutedEventArgs _2)
    {
        if (_isHostKeyPrompt)
        {
            Console.Out.WriteLine("yes");
        }
        else
        {
            var passphrase = TxtPassphrase.Text ?? string.Empty;
            Console.Out.WriteLine(passphrase);
        }

        // App.Quit()でプロセスが終了する前にバッファをフラッシュする。
        // SSHは子プロセス（Askpass）のstdoutから応答を読み取るため、
        // フラッシュしないとシャットダウン時にバッファが失われ、
        // 空の応答として処理されてHost key verification failedになる。
        Console.Out.Flush();

        if (_isPreview)
            Close();
        else
            App.Quit(0);
    }

    /// <summary>
    /// キャンセルボタン押下時の処理。
    /// ホスト鍵確認の場合は"no"を、それ以外は空メッセージを標準出力に書き出す。
    /// </summary>
    private void CloseWindow(object _1, RoutedEventArgs _2)
    {
        if (!_isPreview)
        {
            if (_isHostKeyPrompt)
                Console.Out.WriteLine("no");
            else
                Console.Out.WriteLine("No passphrase entered.");

            Console.Out.Flush();
        }

        if (_isPreview)
            Close();
        else
            App.Quit(-1);
    }
}
