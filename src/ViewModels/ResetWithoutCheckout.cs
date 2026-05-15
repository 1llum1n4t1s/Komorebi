using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// チェックアウトなしでブランチポインタを移動するダイアログのViewModel。
/// ワーキングディレクトリを変更せずにブランチの参照先を変更する。
/// ブランチまたはコミットをリセット先として指定できる。
/// </summary>
public class ResetWithoutCheckout : Popup
{
    /// <summary>
    /// リセット対象のブランチ。
    /// </summary>
    public Models.Branch Target
    {
        get;
    }

    /// <summary>
    /// リセット先のブランチまたはコミット。
    /// </summary>
    public object To
    {
        get;
    }

    /// <summary>
    /// ブランチをリセット先として指定してダイアログを初期化する。
    /// ブランチのHEADコミットをリビジョンとして使用する。
    /// </summary>
    public ResetWithoutCheckout(Repository repo, Models.Branch target, Models.Branch to)
    {
        _repo = repo;
        _revision = to.Head;
        Target = target;
        To = to;
    }

    /// <summary>
    /// コミットをリセット先として指定してダイアログを初期化する。
    /// コミットのSHAをリビジョンとして使用する。
    /// </summary>
    public ResetWithoutCheckout(Repository repo, Models.Branch target, Models.Commit to)
    {
        _repo = repo;
        _revision = to.SHA;
        Target = target;
        To = to;
    }

    /// <summary>
    /// チェックアウトなしのリセットを実行する。
    /// git branch -f コマンドでブランチポインタのみを移動し、
    /// ワーキングディレクトリは変更しない。
    /// </summary>
    public override async Task<bool> Sure()
    {
        ProgressDescription = App.Text("Progress.ResettingWithoutCheckout", Target.Name, _revision);

        var log = _repo.CreateLog($"Reset '{Target.Name}' to '{_revision}'");
        Use(log);

        // /rere 10 人分隊 P0#13: Watcher ロックは git コマンド実行範囲に限定し、MarkBranchesDirtyManually は
        // ロック解除後に呼ぶ。ロック中に呼ぶと、ロック解除後に届く FS イベントが Refresh をキャンセルする。
        bool succ;
        using (_repo.LockWatcher())
        {
            // ブランチポインタを強制的に移動（チェックアウトなし）
            succ = await new Commands.Branch(_repo.FullPath, Target.Name)
                .Use(log)
                .CreateAsync(_revision, true);
        }

        log.Complete();
        // ブランチ一覧の再読み込みをトリガー
        _repo.MarkBranchesDirtyManually();
        return succ;
    }

    /// <summary>対象リポジトリ</summary>
    private readonly Repository _repo = null;
    /// <summary>リセット先のリビジョン（SHA）</summary>
    private readonly string _revision;
}
