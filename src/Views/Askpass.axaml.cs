using System;
using System.Security.Cryptography;
using System.Text;

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
    private bool _isHostKeyChangedWarning;
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
        // ホスト鍵「変更」警告は MITM の可能性があるため、いかなる経路でも yes を送ってはならない
        _isHostKeyChangedWarning = hintKey is "Text.Askpass.Hint.HostKeyChanged";
        if (_isHostKeyPrompt)
            TxtPassphrase.IsVisible = false;

        // ホスト鍵変更警告の場合、接続不可なのでOKボタンも隠す
        if (_isHostKeyChangedWarning)
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
        // 防御: HostKeyChanged 警告中は HotKey="Enter" 経由などで意図せず到達しても
        // "yes" を絶対に送らない。MITM 攻撃を受けたホストへの自動承認を防ぐ。
        if (_isHostKeyChangedWarning)
        {
            Console.Out.WriteLine("no");
            Console.Out.Flush();
            if (_isPreview)
                Close();
            else
                App.Quit(-1);
            return;
        }

        if (_isHostKeyPrompt)
        {
            Console.Out.WriteLine("yes");
            // App.Quit()でプロセスが終了する前にバッファをフラッシュする。
            // SSHは子プロセス（Askpass）のstdoutから応答を読み取るため、
            // フラッシュしないとシャットダウン時にバッファが失われ、
            // 空の応答として処理されてHost key verification failedになる。
            Console.Out.Flush();
        }
        else
        {
            // upstream 6feae0bd: UTF-8 パスフレーズを直接バイト列で書き出し、終了前に passBytes を ZeroMemory でゼロ化。
            // Console.Out.WriteLine() は CodePage 依存で非 ASCII passphrase を破損させる恐れがあったため
            // OpenStandardOutput() で UTF-8 バイト列をそのまま書く。TxtPassphrase.Text も string.Empty で参照を切る。
            //
            // /rere P2#19 注記: C# string は immutable + GC 管理のため、TxtPassphrase.Text 内部の
            // 旧 string オブジェクト自体は GC が回収するまでヒープに残る。完全な秘密保護は SecureString
            // または char[] ベースのカスタムコントロールが必要だが、.NET 5+ では SecureString が非推奨扱いで
            // ベストエフォートに留まる (Askpass プロセスは短命なので現実的リスクは限定)。
            var passphrase = TxtPassphrase.Text ?? string.Empty;
            byte[] passBytes = Encoding.UTF8.GetBytes(passphrase);
            try
            {
                var outStream = Console.OpenStandardOutput();
                outStream.Write(passBytes, 0, passBytes.Length);
                outStream.WriteByte((byte)'\n');
                outStream.Flush();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passBytes);
                TxtPassphrase.Text = string.Empty;
            }
        }

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
