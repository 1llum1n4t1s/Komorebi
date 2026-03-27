using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
///     Git Flowブランチの完了（finish）操作を行うダイアログのViewModel。
///     feature/release/hotfixブランチをメインブランチにマージして完了させる。
/// </summary>
public class GitFlowFinish : Popup
{
    /// <summary>
    ///     完了対象のブランチ。
    /// </summary>
    public Models.Branch Branch
    {
        get;
    }

    /// <summary>
    ///     Git Flowブランチの種類（feature/release/hotfix）。
    /// </summary>
    public Models.GitFlowBranchType Type
    {
        get;
        private set;
    }

    /// <summary>
    ///     マージ時にスカッシュ（コミットを1つにまとめる）するかどうか。
    /// </summary>
    public bool Squash
    {
        get;
        set;
    } = false;

    /// <summary>
    ///     完了後に自動的にリモートへプッシュするかどうか。
    /// </summary>
    public bool AutoPush
    {
        get;
        set;
    } = false;

    /// <summary>
    ///     完了後もブランチを削除せず保持するかどうか。
    /// </summary>
    public bool KeepBranch
    {
        get;
        set;
    } = false;

    /// <summary>
    ///     コンストラクタ。リポジトリ、対象ブランチ、ブランチ種別を指定して初期化する。
    /// </summary>
    public GitFlowFinish(Repository repo, Models.Branch branch, Models.GitFlowBranchType type)
    {
        _repo = repo;
        Branch = branch;
        Type = type;
    }

    /// <summary>
    ///     確認ボタン押下時の処理。Git Flowのfinishコマンドを実行する。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.GitFlowFinish", Branch.Name);

        var log = _repo.CreateLog("GitFlow - Finish");
        Use(log);

        // ブランチ名からプレフィックスを除去して短縮名を取得
        var prefix = _repo.GitFlow.GetPrefix(Type);
        var name = Branch.Name.StartsWith(prefix) ? Branch.Name.Substring(prefix.Length) : Branch.Name;
        var succ = await Commands.GitFlow.FinishAsync(_repo.FullPath, Type, name, Squash, AutoPush, KeepBranch, log);

        log.Complete();
        return succ;
    }

    /// <summary>対象リポジトリへの参照。</summary>
    private readonly Repository _repo;
}
