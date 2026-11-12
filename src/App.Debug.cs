#if DEBUG
// nullable 移行未実施。App.axaml.cs と同じく null 注釈の導入まで警告を抑止する。
#nullable disable warnings
using Avalonia.Controls;

namespace Komorebi;

/// <summary>
/// Debug ビルド専用の CRDebugger 初期化とテスト用コンテナ。
/// App.axaml.cs の本体から分離し、ファイル全体を #if DEBUG で囲むことで Release ビルドの
/// コンパイル対象から外す（CRDebugger パッケージも Debug 構成のみ参照する）。
/// 呼び出し側（App.TryLaunchAsNormal 内の #if DEBUG ブロック）は App.axaml.cs に残している。
/// </summary>
public partial class App
{
    /// <summary>
    /// CRDebuggerを初期化し、メインウィンドウの四隅ダブルクリックでトグルするハンドラを登録する。
    /// </summary>
    private static void InitCRDebugger(Window mainWindow)
    {
        if (CRDebugger.Core.CRDebugger.IsInitialized)
            return;

        var options = new CRDebugger.Core.CRDebuggerOptions
        {
            Theme = CRDebugger.Core.Theming.CRTheme.Dark,
            DefaultTab = CRDebugger.Core.CRTab.Console,
            CaptureUnhandledExceptions = true,
        };
        CRDebugger.Avalonia.CRDebuggerAvaloniaExtensions.UseAvalonia(options);
        CRDebugger.Core.CRDebugger.Initialize(options);

        // Optionsタブにテスト用コンテナを登録する
        CRDebugger.Core.CRDebugger.AddOptionContainer(new DebugNotificationTester());
        CRDebugger.Core.CRDebugger.AddOptionContainer(new DebugAskpassTester());

        // 画面の四隅（40x40px）のダブルクリックでデバッガーをトグルする
        const double cornerSize = 40;
        mainWindow.DoubleTapped += (_, e) =>
        {
            if (e.Source is not Avalonia.Visual visual)
                return;

            var pos = e.GetPosition(mainWindow);
            var bounds = mainWindow.Bounds;

            var isLeft = pos.X < cornerSize;
            var isRight = pos.X > bounds.Width - cornerSize;
            var isTop = pos.Y < cornerSize;
            var isBottom = pos.Y > bounds.Height - cornerSize;

            if ((isLeft || isRight) && (isTop || isBottom))
                CRDebugger.Core.CRDebugger.Toggle();
        };
    }

    /// <summary>
    /// CRDebugger Optionsタブに表示される通知テスト用コンテナ。
    /// 各シナリオをボタンから直接発火できる。
    /// CRDebuggerはリフレクションでインスタンスメソッドを検出するためstaticにできない。
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822")]
    private sealed class DebugNotificationTester
    {
        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "📢 情報通知")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(1)]
        public void SendInfo()
        {
            Dispatch("テスト情報通知メッセージ", false);
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "❌ エラー通知")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(2)]
        public void SendError()
        {
            Dispatch("テストエラー通知メッセージ", true);
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "📢 情報 + ヒント")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(3)]
        public void SendInfoWithHint()
        {
            Dispatch("操作が完了しました", false, "ヒント: これはテスト用のヒントメッセージです");
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "❌ エラー + ヒント")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(4)]
        public void SendErrorWithHint()
        {
            Dispatch("fatal: authentication failed for 'https://github.com/user/repo.git'", true, "認証に失敗しました。パスワードまたはトークンを確認してください。");
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "📢 長文通知")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(5)]
        public void SendLongMessage()
        {
            Dispatch("これは非常に長い通知メッセージのテストです。実際の運用で発生しうる長文のgitエラーメッセージや、複数行にわたる詳細情報が正しく表示されるかを確認するためのものです。", false);
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "📢 長文ヒント（折り返し確認）")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(6)]
        public void SendLongHint()
        {
            Dispatch(
                "fatal: Could not read from remote repository.",
                true,
                "SSH秘密鍵ファイルのパーミッションが緩すぎます。鍵ファイルは所有者のみアクセス可能（chmod 600）である必要があります。この問題はmacOSやLinuxで発生し、秘密鍵ファイルのパーミッションが644や755など他のユーザーからも読み取り可能な状態になっている場合に発生します。");
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "🔑 鍵パーミッションエラー（アクションボタン付き）")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(7)]
        public void SendKeyPermissionError()
        {
            var launcher = GetLauncher();
            if (launcher is null)
                return;

            var errorMsg =
                "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n" +
                "@         WARNING: UNPROTECTED PRIVATE KEY FILE!          @\n" +
                "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n" +
                "Permissions 0755 for '/Users/user/.ssh/id_rsa' are too open.\n" +
                "It is required that your private key files are NOT accessible by others.\n" +
                "This private key will be ignored.\n" +
                "Load key \"/Users/user/.ssh/id_rsa\": bad permissions\n" +
                "git@github.com: Permission denied (publickey).\n" +
                "fatal: Could not read from remote repository.";

            var hintKey = Models.GitErrorHelper.GetHintKey(errorMsg);
            var hint = string.IsNullOrEmpty(hintKey) ? "" : Text(hintKey.Replace("Text.", ""));
            var keyPath = Models.GitErrorHelper.ExtractKeyPathFromPermissionError(errorMsg);
            var fixLabel = Text("GitError.KeyPermissionTooOpen.Fix");

            // デバッグ用: Windows制限を外してアクションボタンを直接構築
            launcher.DispatchNotification(
                launcher.ActivePage?.Node.Id ?? "",
                errorMsg,
                true,
                hint,
                fixLabel,
                () => launcher.DispatchNotification(
                    launcher.ActivePage?.Node.Id ?? "",
                    $"[デバッグ] chmod 600 \"{keyPath}\" を実行します（テスト環境のため実際には実行しません）",
                    false));
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "🗑️ 通知クリア")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(8)]
        public void ClearNotifications()
        {
            GetLauncher()?.ActivePage?.ClearNotifications();
        }

        private static void Dispatch(string message, bool isError, string hint = "")
        {
            var launcher = GetLauncher();
            if (launcher is null)
                return;

            launcher.DispatchNotification(
                launcher.ActivePage?.Node.Id ?? "",
                message,
                isError,
                hint);
        }
    }

    /// <summary>
    /// Askpassダイアログの表示確認用テスター。
    /// 各種SSHプロンプトパターン（ホスト鍵確認、パスフレーズ、パスワード）を
    /// Askpassダイアログとしてプレビュー表示する。
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822")]
    private sealed class DebugAskpassTester
    {
        [CRDebugger.Core.Options.Attributes.CRCategory("Askpassテスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "🔑 初回ホスト鍵確認")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(1)]
        public void ShowHostKeyNew()
        {
            ShowAskpass(
                "The authenticity of host 'github.com (64:ff9b::141b:b171)' can't be established.\n" +
                "ED25519 key fingerprint is SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU\n" +
                "This key is not known by any other names.\n" +
                "Are you sure you want to continue connecting (yes/no/[fingerprint])?");
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("Askpassテスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "⚠️ ホスト鍵変更警告")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(2)]
        public void ShowHostKeyChanged()
        {
            ShowAskpass(
                "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n" +
                "@    WARNING: REMOTE HOST IDENTIFICATION HAS CHANGED!     @\n" +
                "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n" +
                "IT IS POSSIBLE THAT SOMEONE IS DOING SOMETHING NASTY!\n" +
                "Host key for github.com has changed and you have requested strict checking.\n" +
                "Host key verification failed.");
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("Askpassテスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "🔐 パスフレーズ入力")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(3)]
        public void ShowPassphrase()
        {
            ShowAskpass("Enter passphrase for key '/Users/user/.ssh/id_ed25519': ");
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("Askpassテスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "🔒 パスワード入力")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(4)]
        public void ShowPassword()
        {
            ShowAskpass("Password for 'https://user@github.com': ");
        }

        private static void ShowAskpass(string message)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var askpass = new Views.Askpass { IsPreview = true };
                askpass.TxtDescription.Text = message;
                askpass.SetHint(message);
                askpass.Show();
            });
        }
    }
}
#endif
