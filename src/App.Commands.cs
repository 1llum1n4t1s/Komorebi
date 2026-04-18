using System;
using System.Windows.Input;

using Avalonia.Controls;

namespace Komorebi;

/// <summary>
/// アプリケーション全体で使用するコマンド定義を含むAppクラスのpartial部分。
/// XAMLバインディングやメニューアクションから呼び出される静的コマンドを集約する。
/// </summary>
public partial class App
{
    /// <summary>
    /// <see cref="ICommand"/>の簡易実装。Action&lt;object&gt;デリゲートをラップし、
    /// XAMLバインディングからの実行を可能にする。
    /// </summary>
    /// <param name="action">コマンド実行時に呼び出されるデリゲート</param>
    public class Command(Action<object> action) : ICommand
    {
        /// <summary>
        /// コマンドの実行可否が変化した際に発火するイベント。
        /// このコマンドは常に実行可能なため、イベント登録・解除は何もしない。
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        /// <summary>
        /// コマンドが実行可能かどうかを返す。actionがnullでなければ常にtrue。
        /// </summary>
        public bool CanExecute(object parameter) => action is not null;

        /// <summary>
        /// デリゲートを呼び出してコマンドを実行する。
        /// </summary>
        public void Execute(object parameter) => action?.Invoke(parameter);
    }

    /// <summary>
    /// アップデート確認コマンドをUIに表示するかどうか。
    /// コンパイル定数DISABLE_UPDATE_DETECTIONが定義されている場合はfalseを返す。
    /// </summary>
    public static bool IsCheckForUpdateCommandVisible
    {
        get
        {
#if DISABLE_UPDATE_DETECTION
            return false;
#else
            return true;
#endif
        }
    }

    /// <summary>
    /// 設定ダイアログを開くコマンド。
    /// </summary>
    public static readonly Command OpenPreferencesCommand = new(async _ => await ShowDialog(new Views.Preferences()));

    /// <summary>
    /// ホットキー一覧ダイアログを開くコマンド。
    /// </summary>
    public static readonly Command OpenHotkeysCommand = new(async _ => await ShowDialog(new Views.Hotkeys()));

    /// <summary>
    /// アプリケーションデータディレクトリをOSのファイルマネージャーで開くコマンド。
    /// </summary>
    public static readonly Command OpenAppDataDirCommand = new(_ => Native.OS.OpenInFileManager(Native.OS.DataDir));

    /// <summary>
    /// アプリケーション情報（About）ダイアログを開くコマンド。
    /// </summary>
    public static readonly Command OpenAboutCommand = new(async _ => await ShowDialog(new Views.About()));

    /// <summary>
    /// アップデートの有無を手動で確認するコマンド。引数trueはユーザー操作起因であることを示す。
    /// </summary>
    public static readonly Command CheckForUpdateCommand = new(_ => Check4Update(true));

    /// <summary>
    /// アプリケーションを終了するコマンド。終了コード0（正常終了）で終了する。
    /// </summary>
    public static readonly Command QuitCommand = new(_ => Quit(0));

    /// <summary>
    /// TextBlockのテキストをクリップボードにコピーするコマンド。
    /// InlineCollectionがある場合はその結合テキスト、なければTextプロパティをコピーする。
    /// </summary>
    public static readonly Command CopyTextBlockCommand = new(async p =>
    {
        // パラメータがTextBlockでなければ何もしない
        if (p is not TextBlock textBlock)
            return;

        // Inlines（装飾テキスト等）が存在する場合はその結合テキストをコピー
        if (textBlock.Inlines is { Count: > 0 } inlines)
            await CopyTextAsync(inlines.Text);
        // それ以外で通常のTextが設定されている場合はそれをコピー
        else if (!string.IsNullOrEmpty(textBlock.Text))
            await CopyTextAsync(textBlock.Text);
    });

    /// <summary>
    /// 自アプリケーションを隠す macOS 用コマンド（⌘+H 対応）。
    /// upstream 29cf5fc5 で IActivatableLifetime ベースから Native.OS.HideSelf() 直呼びに変更。
    /// 非 macOS プラットフォームでは何もしない。
    /// </summary>
    public static readonly Command HideAppCommand = new(_ => Native.OS.HideSelf());

    /// <summary>
    /// Komorebi 以外のアプリケーションを全て隠す macOS 用コマンド（⌘+Alt+H、upstream 29cf5fc5）。
    /// 非 macOS プラットフォームでは何もしない。
    /// </summary>
    public static readonly Command HideOtherApplicationsCommand = new(_ => Native.OS.HideOtherApplications());

    /// <summary>
    /// 隠されている全アプリケーションを再表示する macOS 用コマンド（upstream 29cf5fc5）。
    /// 非 macOS プラットフォームでは何もしない。
    /// </summary>
    public static readonly Command ShowAllApplicationsCommand = new(_ => Native.OS.ShowAllApplications());
}
