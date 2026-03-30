using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// リポジトリノードを別のグループに移動するダイアログのViewModel。
/// ツリー構造のグループ一覧を表示し、移動先を選択させる。
/// </summary>
public class MoveRepositoryNode : Popup
{
    /// <summary>
    /// 移動対象のリポジトリノード。
    /// </summary>
    public RepositoryNode Target
    {
        get;
    } = null;

    /// <summary>
    /// 移動先候補のフラットなリスト（ツリーをDepthで表現）。
    /// </summary>
    public List<RepositoryNode> Rows
    {
        get;
    } = [];

    /// <summary>
    /// 選択された移動先ノード。
    /// </summary>
    public RepositoryNode Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    /// <summary>
    /// 移動対象ノードを指定してダイアログを初期化する。
    /// ROOTノードを先頭に追加し、グループノードをフラットリストとして構築する。
    /// </summary>
    public MoveRepositoryNode(RepositoryNode target)
    {
        Target = target;
        // ルートノードを先頭に追加（ルートへの移動用）
        Rows.Add(new RepositoryNode()
        {
            Name = "ROOT",
            Depth = 0,
            Id = Guid.NewGuid().ToString()
        });
        // 既存のグループノードをフラットリストに展開
        MakeRows(Preferences.Instance.RepositoryNodes, 1);
    }

    /// <summary>
    /// 選択された移動先にノードを移動し、ウェルカムページを更新する。
    /// </summary>
    public override Task<bool> Sure()
    {
        if (_selected is not null)
        {
            // 選択されたノードのIDから実際のノードを検索して移動を実行
            var node = Preferences.Instance.FindNode(_selected.Id);
            Preferences.Instance.MoveNode(Target, node, true);
            Welcome.Instance.Refresh();
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// リポジトリノードコレクションからグループのみを再帰的にフラットリストに追加する。
    /// 移動対象自身とリポジトリノードはスキップする。
    /// </summary>
    private void MakeRows(List<RepositoryNode> collection, int depth)
    {
        foreach (var node in collection)
        {
            // リポジトリノードと移動対象自身はスキップ
            if (node.IsRepository || node.Id == Target.Id)
                continue;

            var dump = new RepositoryNode()
            {
                Name = node.Name,
                Depth = depth,
                Id = node.Id
            };
            Rows.Add(dump);
            // 子ノードを再帰的に処理
            MakeRows(node.SubNodes, depth + 1);
        }
    }

    /// <summary>選択された移動先ノード</summary>
    private RepositoryNode _selected = null;
}
