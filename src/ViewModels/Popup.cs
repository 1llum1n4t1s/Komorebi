using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// すべてのダイアログViewModelの基底クラス。
/// ObservableValidatorを継承しバリデーション機能を提供する。
/// 派生クラスはSure()をオーバーライドして確認アクションを実装する。
/// コマンドログの購読により進捗表示を行う。
/// </summary>
public class Popup : ObservableValidator, Models.ICommandLogReceiver
{
    /// <summary>
    /// 処理が進行中かどうか。trueの間はUIにプログレス表示される。
    /// </summary>
    public bool InProgress
    {
        get => _inProgress;
        set => SetProperty(ref _inProgress, value);
    }

    /// <summary>
    /// 進捗表示用の説明テキスト。コマンドログから自動更新される。
    /// </summary>
    public string ProgressDescription
    {
        get => _progressDescription;
        set => SetProperty(ref _progressDescription, value);
    }

    /// <summary>
    /// バリデーションを実行し、エラーがないことを確認する。
    /// すべてのプロパティのバリデーションを実行して結果を返す。
    /// </summary>
    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026:RequiresUnreferencedCode")]
    public bool Check()
    {
        if (HasErrors)
            return false;
        ValidateAllProperties();
        return !HasErrors;
    }

    /// <summary>
    /// コマンドログのデータを受信し、進捗説明を更新する。
    /// ICommandLogReceiverインターフェースの実装。
    /// </summary>
    public void OnReceiveCommandLog(string data)
    {
        var desc = data.Trim();
        if (!string.IsNullOrEmpty(desc))
            ProgressDescription = desc;
    }

    /// <summary>
    /// ダイアログ終了時のクリーンアップ処理。コマンドログの購読を解除する。
    /// </summary>
    public void Cleanup()
    {
        _log?.Unsubscribe(this);
    }

    /// <summary>
    /// ダイアログを確認ボタンなしで直接実行開始できるかどうか。
    /// デフォルトはtrue。派生クラスでオーバーライドして制御する。
    /// </summary>
    public virtual bool CanStartDirectly()
    {
        return true;
    }

    /// <summary>
    /// ダイアログの確認アクション。派生クラスでオーバーライドして
    /// git操作などの実際の処理を実装する。成功時にtrueを返す。
    /// オーバーライドを忘れた場合でも NullReferenceException を起こさないよう
    /// デフォルトで Task.FromResult(true) を返す（= ダイアログを即座に閉じる挙動）。
    /// </summary>
    public virtual Task<bool> Sure()
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// コマンドログを購読し、進捗表示に利用する。
    /// 派生クラスのSure()内で呼び出す。
    /// </summary>
    protected void Use(CommandLog log)
    {
        _log = log;
        _log.Subscribe(this);
    }

    /// <summary>処理進行中フラグ</summary>
    private bool _inProgress = false;
    /// <summary>進捗説明テキスト</summary>
    private string _progressDescription = string.Empty;
    /// <summary>購読中のコマンドログ</summary>
    private CommandLog _log = null;
}
