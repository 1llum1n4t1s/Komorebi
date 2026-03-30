using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// リポジトリノード（リポジトリまたはグループ）をブックマークリストから削除するためのダイアログViewModel。
/// </summary>
public class DeleteRepositoryNode : Popup
{
    /// <summary>
    /// 削除対象のリポジトリノード。
    /// </summary>
    public RepositoryNode Node
    {
        get;
    }

    /// <summary>
    /// コンストラクタ。削除対象のノードを指定する。
    /// </summary>
    public DeleteRepositoryNode(RepositoryNode node)
    {
        Node = node;
    }

    /// <summary>
    /// ノード削除を実行する確認アクション。
    /// 設定からノードを削除し、ウェルカム画面を更新する。
    /// </summary>
    public override Task<bool> Sure()
    {
        // 設定からノードを再帰的に削除
        Preferences.Instance.RemoveNode(Node, true);
        Welcome.Instance.Refresh();
        return Task.FromResult(true);
    }
}
