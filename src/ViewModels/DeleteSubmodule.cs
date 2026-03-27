using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
///     サブモジュールを完全に削除するためのダイアログViewModel。
/// </summary>
public class DeleteSubmodule : Popup
{
    /// <summary>
    ///     削除対象のサブモジュールパス。
    /// </summary>
    public string Submodule
    {
        get;
        private set;
    }

    /// <summary>
    ///     コンストラクタ。対象リポジトリとサブモジュールパスを指定する。
    /// </summary>
    public DeleteSubmodule(Repository repo, string submodule)
    {
        _repo = repo;
        Submodule = submodule;
    }

    /// <summary>
    ///     サブモジュール削除を実行する確認アクション。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.DeletingSubmodule");

        var log = _repo.CreateLog("Delete Submodule");
        Use(log);

        var succ = await new Commands.Submodule(_repo.FullPath)
            .Use(log)
            .DeleteAsync(Submodule);

        log.Complete();
        return succ;
    }

    private readonly Repository _repo = null;
}
