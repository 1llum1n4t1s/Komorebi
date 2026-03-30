using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// サブモジュールの初期化を解除（deinit）するためのダイアログViewModel。
/// </summary>
public class DeinitSubmodule : Popup
{
    /// <summary>
    /// 初期化解除対象のサブモジュールパス。
    /// </summary>
    public string Submodule
    {
        get;
        private set;
    }

    /// <summary>
    /// 強制的に初期化解除するかどうか。
    /// </summary>
    public bool Force
    {
        get;
        set;
    }

    /// <summary>
    /// コンストラクタ。対象リポジトリとサブモジュールパスを指定する。
    /// </summary>
    public DeinitSubmodule(Repository repo, string submodule)
    {
        _repo = repo;
        Submodule = submodule;
        Force = false;
    }

    /// <summary>
    /// サブモジュールの初期化解除を実行する確認アクション。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.DeinitSubmodule");

        var log = _repo.CreateLog("De-initialize Submodule");
        Use(log);

        var succ = await new Commands.Submodule(_repo.FullPath)
            .Use(log)
            .DeinitAsync(Submodule, false);

        log.Complete();
        _repo.MarkSubmodulesDirtyManually();
        return succ;
    }

    private Repository _repo;
}
