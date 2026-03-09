using System.Collections.Generic;

using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     サブモジュールツリーのノード。フォルダまたはサブモジュール自体を表す。
    /// </summary>
    public class SubmoduleTreeNode : ObservableObject
    {
        /// <summary>
        ///     ノードのフルパス。
        /// </summary>
        public string FullPath { get; private set; } = string.Empty;

        /// <summary>
        ///     ツリー内のネスト深度。
        /// </summary>
        public int Depth { get; private set; } = 0;

        /// <summary>
        ///     対応するサブモジュールモデル。フォルダノードの場合はnull。
        /// </summary>
        public Models.Submodule Module { get; private set; } = null;

        /// <summary>
        ///     子ノードのリスト。
        /// </summary>
        public List<SubmoduleTreeNode> Children { get; private set; } = [];

        /// <summary>
        ///     配下のサブモジュール数カウンタ。
        /// </summary>
        public int Counter = 0;

        /// <summary>
        ///     フォルダノードかどうか（Moduleがnullならフォルダ）。
        /// </summary>
        public bool IsFolder
        {
            get => Module == null;
        }

        /// <summary>
        ///     ツリーノードが展開されているかどうか。
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        ///     子要素数の表示文字列。0の場合は空文字。
        /// </summary>
        public string ChildCounter
        {
            get => Counter > 0 ? $"({Counter})" : string.Empty;
        }

        /// <summary>
        ///     サブモジュールが未コミットの変更を持つかどうか。
        /// </summary>
        public bool IsDirty
        {
            get => Module?.IsDirty ?? false;
        }

        /// <summary>
        ///     サブモジュールノードのコンストラクタ。
        /// </summary>
        public SubmoduleTreeNode(Models.Submodule module, int depth)
        {
            FullPath = module.Path;
            Depth = depth;
            Module = module;
            IsExpanded = false;
        }

        /// <summary>
        ///     フォルダノードのコンストラクタ。
        /// </summary>
        public SubmoduleTreeNode(string path, int depth, bool isExpanded)
        {
            FullPath = path;
            Depth = depth;
            IsExpanded = isExpanded;
            Counter = 1;
        }

        /// <summary>
        ///     サブモジュールリストからツリー構造を構築する。
        ///     パスの区切り文字に基づいてフォルダ階層を生成する。
        /// </summary>
        public static List<SubmoduleTreeNode> Build(IList<Models.Submodule> submodules, HashSet<string> expanded)
        {
            var nodes = new List<SubmoduleTreeNode>();
            var folders = new Dictionary<string, SubmoduleTreeNode>();

            foreach (var module in submodules)
            {
                var sepIdx = module.Path.IndexOf('/');
                if (sepIdx == -1)
                {
                    // パス区切りなし = トップレベルのサブモジュール
                    nodes.Add(new SubmoduleTreeNode(module, 0));
                }
                else
                {
                    SubmoduleTreeNode lastFolder = null;
                    int depth = 0;

                    // パスの各セグメントに対してフォルダノードを作成または取得
                    while (sepIdx != -1)
                    {
                        var folder = module.Path.Substring(0, sepIdx);
                        if (folders.TryGetValue(folder, out var value))
                        {
                            // 既存フォルダのカウンタを増加
                            lastFolder = value;
                            lastFolder.Counter++;
                        }
                        else if (lastFolder == null)
                        {
                            // ルートレベルに新しいフォルダを作成
                            lastFolder = new SubmoduleTreeNode(folder, depth, expanded.Contains(folder));
                            folders.Add(folder, lastFolder);
                            InsertFolder(nodes, lastFolder);
                        }
                        else
                        {
                            // 親フォルダの子として新しいフォルダを作成
                            var cur = new SubmoduleTreeNode(folder, depth, expanded.Contains(folder));
                            folders.Add(folder, cur);
                            InsertFolder(lastFolder.Children, cur);
                            lastFolder = cur;
                        }

                        depth++;
                        sepIdx = module.Path.IndexOf('/', sepIdx + 1);
                    }

                    lastFolder?.Children.Add(new SubmoduleTreeNode(module, depth));
                }
            }

            folders.Clear();
            return nodes;
        }

        /// <summary>
        ///     フォルダノードをコレクション内の適切な位置（非フォルダノードの前）に挿入する。
        /// </summary>
        private static void InsertFolder(List<SubmoduleTreeNode> collection, SubmoduleTreeNode subFolder)
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

        private bool _isExpanded = false;
    }

    /// <summary>
    ///     サブモジュールをツリー形式で表示するコレクション。
    ///     展開状態を保持しながらツリーとフラット行リストを管理する。
    /// </summary>
    public class SubmoduleCollectionAsTree
    {
        /// <summary>
        ///     ツリーのルートノードリスト。
        /// </summary>
        public List<SubmoduleTreeNode> Tree
        {
            get;
            set;
        } = [];

        /// <summary>
        ///     展開状態に基づくフラット化された行リスト（UIバインディング用）。
        /// </summary>
        public AvaloniaList<SubmoduleTreeNode> Rows
        {
            get;
            set;
        } = [];

        /// <summary>
        ///     サブモジュールリストからツリーコレクションを構築する。以前の展開状態を引き継ぐ。
        /// </summary>
        public static SubmoduleCollectionAsTree Build(List<Models.Submodule> submodules, SubmoduleCollectionAsTree old)
        {
            var oldExpanded = new HashSet<string>();
            if (old != null)
            {
                foreach (var row in old.Rows)
                {
                    if (row.IsFolder && row.IsExpanded)
                        oldExpanded.Add(row.FullPath);
                }
            }

            var collection = new SubmoduleCollectionAsTree();
            collection.Tree = SubmoduleTreeNode.Build(submodules, oldExpanded);

            var rows = new List<SubmoduleTreeNode>();
            MakeTreeRows(rows, collection.Tree);
            collection.Rows.AddRange(rows);

            return collection;
        }

        /// <summary>
        ///     ノードの展開/折りたたみを切り替え、行リストを更新する。
        /// </summary>
        public void ToggleExpand(SubmoduleTreeNode node)
        {
            node.IsExpanded = !node.IsExpanded;

            var rows = Rows;
            var depth = node.Depth;
            var idx = rows.IndexOf(node);
            if (idx == -1)
                return;

            if (node.IsExpanded)
            {
                // 展開: 子ノードを行リストに挿入
                var subrows = new List<SubmoduleTreeNode>();
                MakeTreeRows(subrows, node.Children);
                rows.InsertRange(idx + 1, subrows);
            }
            else
            {
                // 折りたたみ: 子ノードを行リストから削除
                var removeCount = 0;
                for (int i = idx + 1; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row.Depth <= depth)
                        break;

                    removeCount++;
                }
                rows.RemoveRange(idx + 1, removeCount);
            }
        }

        /// <summary>
        ///     ツリーノードを再帰的にフラット行リストに変換する。展開されたフォルダの子のみ含む。
        /// </summary>
        private static void MakeTreeRows(List<SubmoduleTreeNode> rows, List<SubmoduleTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                rows.Add(node);

                if (!node.IsExpanded || !node.IsFolder)
                    continue;

                MakeTreeRows(rows, node.Children);
            }
        }
    }

    /// <summary>
    ///     サブモジュールをフラットリスト形式で表示するコレクション。
    /// </summary>
    public class SubmoduleCollectionAsList
    {
        /// <summary>
        ///     サブモジュールのフラットリスト。
        /// </summary>
        public List<Models.Submodule> Submodules
        {
            get;
            set;
        } = [];
    }
}
