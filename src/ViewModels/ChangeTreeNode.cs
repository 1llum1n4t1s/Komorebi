using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
///     変更ファイルツリーのノードを表すViewModel。
///     フォルダノードとファイルノードの両方を表現し、ツリー構造を構築する。
/// </summary>
public class ChangeTreeNode : ObservableObject
{
    /// <summary>ノードのフルパス</summary>
    public string FullPath { get; set; }
    /// <summary>ノードの表示名（ファイル名またはフォルダ名）</summary>
    public string DisplayName { get; set; }
    /// <summary>ツリーの深さ</summary>
    public int Depth { get; private set; } = 0;
    /// <summary>関連する変更オブジェクト（フォルダノードの場合はnull）</summary>
    public Models.Change Change { get; set; } = null;
    /// <summary>子ノードのリスト</summary>
    public List<ChangeTreeNode> Children { get; set; } = new List<ChangeTreeNode>();

    /// <summary>
    ///     このノードがフォルダかどうか（Changeがnullならフォルダ）。
    /// </summary>
    public bool IsFolder
    {
        get => Change is null;
    }

    /// <summary>
    ///     コンフリクトマーカーを表示するかどうか。
    /// </summary>
    public bool ShowConflictMarker
    {
        get => Change is { IsConflicted: true };
    }

    /// <summary>
    ///     コンフリクトマーカーの文字列。
    /// </summary>
    public string ConflictMarker
    {
        get => Change?.ConflictMarker ?? string.Empty;
    }

    /// <summary>
    ///     ツリーノードの展開状態。
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    ///     変更ファイルからファイルノードを作成するコンストラクタ。
    /// </summary>
    /// <param name="c">変更オブジェクト</param>
    public ChangeTreeNode(Models.Change c)
    {
        FullPath = c.Path;
        DisplayName = Path.GetFileName(c.Path);
        Change = c;
        IsExpanded = false;
    }

    /// <summary>
    ///     フォルダノードを作成するコンストラクタ。
    /// </summary>
    /// <param name="path">フォルダパス</param>
    /// <param name="isExpanded">初期展開状態</param>
    public ChangeTreeNode(string path, bool isExpanded)
    {
        FullPath = path;
        DisplayName = Path.GetFileName(path);
        IsExpanded = isExpanded;
    }

    /// <summary>
    ///     変更リストからツリー構造を構築する。
    /// </summary>
    /// <param name="changes">変更ファイルのリスト</param>
    /// <param name="folded">折りたたみ状態のフォルダパスセット</param>
    /// <param name="compactFolders">単一子のフォルダを圧縮表示するかどうか</param>
    /// <returns>構築されたツリーノードのルートリスト</returns>
    public static List<ChangeTreeNode> Build(IList<Models.Change> changes, HashSet<string> folded, bool compactFolders)
    {
        var nodes = new List<ChangeTreeNode>();
        var folders = new Dictionary<string, ChangeTreeNode>();

        foreach (var c in changes)
        {
            var sepIdx = c.Path.IndexOf('/');
            if (sepIdx == -1)
            {
                // パス区切りがないファイルはルート直下に追加する
                nodes.Add(new ChangeTreeNode(c));
            }
            else
            {
                // パス区切りに沿ってフォルダ階層を構築する
                ChangeTreeNode lastFolder = null;

                while (sepIdx != -1)
                {
                    var folder = c.Path.Substring(0, sepIdx);
                    if (folders.TryGetValue(folder, out var value))
                    {
                        // 既存フォルダを再利用する
                        lastFolder = value;
                    }
                    else if (lastFolder is null)
                    {
                        // ルートレベルに新しいフォルダノードを作成する
                        lastFolder = new ChangeTreeNode(folder, !folded.Contains(folder));
                        folders.Add(folder, lastFolder);
                        InsertFolder(nodes, lastFolder);
                    }
                    else
                    {
                        // 親フォルダの子として新しいフォルダノードを作成する
                        var cur = new ChangeTreeNode(folder, !folded.Contains(folder));
                        folders.Add(folder, cur);
                        InsertFolder(lastFolder.Children, cur);
                        lastFolder = cur;
                    }

                    sepIdx = c.Path.IndexOf('/', sepIdx + 1);
                }

                // ファイルノードを最後のフォルダに追加する
                lastFolder?.Children.Add(new ChangeTreeNode(c));
            }
        }

        // フォルダ圧縮が有効な場合、単一子フォルダを結合する
        if (compactFolders)
        {
            foreach (var node in nodes)
                Compact(node);
        }

        // ソートと深さの設定を行う
        SortAndSetDepth(nodes, 0);

        folders.Clear();
        return nodes;
    }

    /// <summary>
    ///     フォルダノードをコレクション内のファイルノードの前に挿入する。
    /// </summary>
    /// <param name="collection">挿入先のノードリスト</param>
    /// <param name="subFolder">挿入するフォルダノード</param>
    private static void InsertFolder(List<ChangeTreeNode> collection, ChangeTreeNode subFolder)
    {
        for (int i = 0; i < collection.Count; i++)
        {
            if (!collection[i].IsFolder)
            {
                collection.Insert(i, subFolder);
                return;
            }
        }

        collection.Add(subFolder);
    }

    /// <summary>
    ///     単一子フォルダを親フォルダに結合して表示名を圧縮する。
    /// </summary>
    /// <param name="node">圧縮対象のノード</param>
    private static void Compact(ChangeTreeNode node)
    {
        var childrenCount = node.Children.Count;
        if (childrenCount == 0)
            return;

        // 子が複数ある場合は各子を個別に圧縮する
        if (childrenCount > 1)
        {
            foreach (var c in node.Children)
                Compact(c);
            return;
        }

        // 単一子がファイルノードの場合は圧縮しない
        var child = node.Children[0];
        if (child.Change is not null)
            return;

        // 単一子フォルダを親に吸収して表示名を結合する
        node.FullPath = $"{node.FullPath}/{child.DisplayName}";
        node.DisplayName = $"{node.DisplayName} / {child.DisplayName}";
        node.IsExpanded = child.IsExpanded;
        node.Children = child.Children;
        // 再帰的に圧縮を続ける
        Compact(node);
    }

    /// <summary>
    ///     ノードリストをソートし、各ノードに深さを設定する。
    ///     フォルダが先、コンフリクトファイルが次に、その後名前順でソートする。
    /// </summary>
    /// <param name="nodes">ソート対象のノードリスト</param>
    /// <param name="depth">現在の深さ</param>
    private static void SortAndSetDepth(List<ChangeTreeNode> nodes, int depth)
    {
        foreach (var node in nodes)
        {
            node.Depth = depth;
            if (node.IsFolder)
                SortAndSetDepth(node.Children, depth + 1);
        }

        nodes.Sort((l, r) =>
        {
            // フォルダ同士またはファイル同士の場合
            if (l.IsFolder == r.IsFolder)
            {
                // ファイルの場合はコンフリクトファイルを先にする
                if (!l.IsFolder)
                {
                    var lConflict = l.Change?.IsConflicted ?? false;
                    var rConflict = r.Change?.IsConflicted ?? false;
                    if (lConflict != rConflict)
                        return lConflict ? -1 : 1;
                }

                return Models.NumericSort.Compare(l.DisplayName, r.DisplayName);
            }

            // フォルダをファイルより前に配置する
            return l.IsFolder ? -1 : 1;
        });
    }

    /// <summary>展開状態</summary>
    private bool _isExpanded = true;
}
