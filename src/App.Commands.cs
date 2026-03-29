using System;
using System.Windows.Input;

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

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
    /// アプリケーションをバックグラウンドに隠すコマンド（トレイ最小化等）。
    /// IActivatableLifetimeがサポートされているプラットフォームでのみ動作する。
    /// </summary>
    public static readonly Command HideAppCommand = new(_ =>
    {
        // IActivatableLifetime機能を取得し、バックグラウンド状態へ遷移させる
        if (Current is App app && app.TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime lifetime)
            lifetime.TryEnterBackground();
    });

    /// <summary>
    /// バックグラウンドからアプリケーションを前面に復帰させるコマンド。
    /// IActivatableLifetimeがサポートされているプラットフォームでのみ動作する。
    /// </summary>
    public static readonly Command ShowAppCommand = new(_ =>
    {
        // IActivatableLifetime機能を取得し、フォアグラウンド状態へ復帰させる
        if (Current is App app && app.TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime lifetime)
            lifetime.TryLeaveBackground();
    });
}
