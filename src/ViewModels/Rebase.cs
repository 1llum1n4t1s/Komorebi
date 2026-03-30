using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// 現在のブランチを別のブランチまたはコミットにリベースするダイアログのViewModel。
/// ブランチまたはコミットをリベース先として指定でき、AutoStashオプションに対応する。
/// </summary>
public class Rebase : Popup
{
    /// <summary>
    /// リベース対象の現在のブランチ。
    /// </summary>
    public Models.Branch Current
    {
        get;
        private set;
    }

    /// <summary>
    /// リベース先のブランチまたはコミット。
    /// </summary>
    public object On
    {
        get;
        private set;
    }

    /// <summary>
    /// リベース前にローカル変更を自動スタッシュするかどうか。
    /// </summary>
    public bool AutoStash
    {
        get;
        set;
    }

    /// <summary>
    /// ブランチを指定してリベースダイアログを初期化する。
    /// ブランチのHEADコミットをリビジョンとして使用する。
    /// </summary>
    public Rebase(Repository repo, Models.Branch current, Models.Branch on)
    {
        _repo = repo;
        _revision = on.Head;
        Current = current;
        On = on;
        AutoStash = true;
    }

    /// <summary>
    /// コミットを指定してリベースダイアログを初期化する。
    /// コミットのSHAをリビジョンとして使用する。
    /// </summary>
    public Rebase(Repository repo, Models.Branch current, Models.Commit on)
    {
        _repo = repo;
        _revision = on.SHA;
        Current = current;
        On = on;
        AutoStash = true;
    }

    /// <summary>
    /// リベースを実行する。
    /// コミットメッセージをクリアし、指定されたリビジョンにリベースする。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        // リベース前にコミットメッセージをクリア
        _repo.ClearCommitMessage();
        ProgressDescription = App.Text("Progress.Rebasing");

        var log = _repo.CreateLog("Rebase");
        Use(log);

        // git rebase コマンドを実行
        await new Commands.Rebase(_repo.FullPath, _revision, AutoStash)
            .Use(log)
            .ExecAsync();

        log.Complete();
        return true;
    }

    /// <summary>対象リポジトリ</summary>
    private readonly Repository _repo;
    /// <summary>リベース先のリビジョン（SHA）</summary>
    private readonly string _revision;
}
