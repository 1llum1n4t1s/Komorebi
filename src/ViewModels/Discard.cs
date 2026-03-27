using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
///     すべてのローカル変更を破棄するモード。追跡外ファイルと無視ファイルの含有オプション付き。
/// </summary>
public class DiscardAllMode
{
    /// <summary>
    ///     追跡されていないファイルも破棄対象に含めるかどうか。
    /// </summary>
    public bool IncludeUntracked
    {
        get;
        set;
    } = false;

    /// <summary>
    ///     無視ファイルも破棄対象に含めるかどうか。
    /// </summary>
    public bool IncludeIgnored
    {
        get;
        set;
    } = false;
}

/// <summary>
///     単一ファイルの変更を破棄するモード。対象ファイルのパスを保持する。
/// </summary>
public class DiscardSingleFile
{
    /// <summary>
    ///     破棄対象のファイルパス。
    /// </summary>
    public string Path
    {
        get;
        set;
    } = string.Empty;
}

/// <summary>
///     複数ファイルの変更を破棄するモード。対象ファイル数を保持する。
/// </summary>
public class DiscardMultipleFiles
{
    /// <summary>
    ///     破棄対象のファイル数。
    /// </summary>
    public int Count
    {
        get;
        set;
    } = 0;
}

/// <summary>
///     ローカル変更の破棄を確認するダイアログViewModel。
///     全変更破棄、単一ファイル破棄、複数ファイル破棄の3つのモードに対応する。
/// </summary>
public class Discard : Popup
{
    /// <summary>
    ///     破棄モード（DiscardAllMode / DiscardSingleFile / DiscardMultipleFiles）。
    /// </summary>
    public object Mode
    {
        get;
    }

    /// <summary>
    ///     コンストラクタ。全変更破棄モードで初期化する。
    /// </summary>
    public Discard(Repository repo)
    {
        _repo = repo;
        Mode = new DiscardAllMode();
    }

    /// <summary>
    ///     コンストラクタ。変更リストに応じて適切な破棄モードを選択する。
    /// </summary>
    public Discard(Repository repo, List<Models.Change> changes)
    {
        _repo = repo;
        _changes = changes;

        if (_changes is null)
            Mode = new DiscardAllMode();
        else if (_changes.Count == 1)
            Mode = new DiscardSingleFile() { Path = _changes[0].Path };
        else
            Mode = new DiscardMultipleFiles() { Count = _changes.Count };
    }

    /// <summary>
    ///     変更破棄を実行する確認アクション。
    ///     全変更破棄の場合はコミットメッセージもクリアする。
    /// </summary>
    public override async Task<bool> Sure()
    {
        ProgressDescription = _changes is null ? "Discard all local changes ..." : $"Discard total {_changes.Count} changes ...";

        var log = _repo.CreateLog("Discard Changes");
        Use(log);

        // Watcherをロックして破棄操作中のFSイベントによるリフレッシュを抑止する。
        // ロック解除後にMarkWorkingCopyDirtyManuallyを呼ぶことで、
        // 遅延FSイベントによるリフレッシュ競合（キャンセル）を防ぐ。
        using (_repo.LockWatcher())
        {
            if (Mode is DiscardAllMode all)
            {
                // 全変更破棄：追跡外・無視ファイルのオプション付き
                await Commands.Discard.AllAsync(_repo.FullPath, all.IncludeUntracked, all.IncludeIgnored, log);
                _repo.ClearCommitMessage();
            }
            else
            {
                // 選択された変更のみ破棄
                await Commands.Discard.ChangesAsync(_repo.FullPath, _changes, log);
            }
        }

        log.Complete();
        _repo.MarkWorkingCopyDirtyManually();
        return true;
    }

    private readonly Repository _repo = null; // 対象リポジトリ
    private readonly List<Models.Change> _changes = null; // 破棄対象の変更リスト（nullの場合は全変更）
}
