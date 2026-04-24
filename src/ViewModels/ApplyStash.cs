using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// スタッシュ適用ダイアログのViewModel。
/// 保存されたスタッシュをワーキングコピーに適用する。
/// </summary>
public class ApplyStash : Popup
{
    /// <summary>
    /// 適用対象のスタッシュオブジェクト。
    /// </summary>
    public Models.Stash Stash
    {
        get;
        private set;
    }

    /// <summary>
    /// インデックスの状態も復元するかどうかのフラグ。デフォルトはtrue。
    /// </summary>
    public bool RestoreIndex
    {
        get;
        set;
    } = true;

    /// <summary>
    /// 適用後にスタッシュを削除するかどうかのフラグ。デフォルトはfalse。
    /// </summary>
    public bool DropAfterApply
    {
        get;
        set;
    } = false;

    /// <summary>
    /// コンストラクタ。リポジトリと対象スタッシュを受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    /// <param name="stash">適用するスタッシュ</param>
    public ApplyStash(Repository repo, Models.Stash stash)
    {
        _repo = repo;
        Stash = stash;
    }

    /// <summary>
    /// 確定処理。git stash applyを実行し、オプションで適用後に削除も行う。
    /// </summary>
    /// <returns>常にtrue</returns>
    public override async Task<bool> Sure()
    {
        ProgressDescription = App.Text("Progress.ApplyingStash", Stash.Name);

        // コマンドログを作成する
        var log = _repo.CreateLog("Apply Stash");
        Use(log);

        // git stash applyコマンドを実行する。LockWatcher はコマンド実行中だけ保持する（ブロック構文）。
        // MarkWorkingCopyDirtyManually はロック解除後に呼ぶ（Discard.cs パターン準拠）。
        bool succ;
        using (_repo.LockWatcher())
        {
            succ = await new Commands.Stash(_repo.FullPath)
                .Use(log)
                .ApplyAsync(Stash.Name, RestoreIndex);
        }

        if (succ)
        {
            // ワーキングコピーの状態を更新する（ロック解除後）
            _repo.MarkWorkingCopyDirtyManually();

            // 適用後削除オプションが有効な場合、スタッシュを削除する
            if (DropAfterApply)
            {
                await new Commands.Stash(_repo.FullPath)
                    .Use(log)
                    .DropAsync(Stash.Name);

                // スタッシュ一覧を更新する
                _repo.MarkStashesDirtyManually();
            }
        }

        log.Complete();
        return true;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo;
}
