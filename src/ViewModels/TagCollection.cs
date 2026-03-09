using System.Collections;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     タグのツールチップに表示する情報を保持するクラス。
    /// </summary>
    public class TagToolTip
    {
        /// <summary>タグ名。</summary>
        public string Name { get; private set; }
        /// <summary>注釈付きタグかどうか。</summary>
        public bool IsAnnotated { get; private set; }
        /// <summary>タグ作成者。</summary>
        public Models.User Creator { get; private set; }
        /// <summary>タグ作成日時の文字列表現。</summary>
        public string CreatorDateStr { get; private set; }
        /// <summary>タグメッセージ。</summary>
        public string Message { get; private set; }

        /// <summary>
        ///     コンストラクタ。タグモデルからツールチップ情報を抽出する。
        /// </summary>
        public TagToolTip(Models.Tag t)
        {
            Name = t.Name;
            IsAnnotated = t.IsAnnotated;
            Creator = t.Creator;
            CreatorDateStr = t.CreatorDateStr;
            Message = t.Message;
        }
    }

    /// <summary>
    ///     タグツリーのノード。フォルダまたはタグ自体を表す。
    ///     パスの区切り文字に基づいて階層構造を形成する。
    /// </summary>
    public class TagTreeNode : ObservableObject
    {
        /// <summary>タグまたはフォルダのフルパス。</summary>
        public string FullPath { get; private set; }
        /// <summary>ツリー内のネスト深度。</summary>
        public int Depth { get; private set; } = 0;
        /// <summary>対応するタグモデル。フォルダの場合はnull。</summary>
        public Models.Tag Tag { get; private set; } = null;
        /// <summary>ツールチップ表示用のタグ情報。</summary>
        public TagToolTip ToolTip { get; private set; } = null;
        /// <summary>子ノードのリスト。</summary>
        public List<TagTreeNode> Children { get; private set; } = [];
        /// <summary>配下のタグ数カウンタ。</summary>
        public int Counter { get; set; } = 0;

        /// <summary>
        ///     フォルダノードかどうか。
        /// </summary>
        public bool IsFolder
        {
            get => Tag == null;
        }

        /// <summary>
        ///     ノードが選択されているかどうか。
        /// </summary>
        public bool IsSelected
        {
            get;
            set;
        }

        /// <summary>
        ///     コミットグラフのフィルタモード。
        /// </summary>
        public Models.FilterMode FilterMode
        {
            get => _filterMode;
            set => SetProperty(ref _filterMode, value);
        }

        /// <summary>
        ///     選択状態の連続表示に使用する角丸半径。
        /// </summary>
        public CornerRadius CornerRadius
        {
            get => _cornerRadius;
            set => SetProperty(ref _cornerRadius, value);
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
        ///     配下タグ数の表示文字列。
        /// </summary>
        public string TagsCount
        {
            get => Counter > 0 ? $"({Counter})" : string.Empty;
        }

        /// <summary>
        ///     タグノードのコンストラクタ。
        /// </summary>
        public TagTreeNode(Models.Tag t, int depth)
        {
            FullPath = t.Name;
            Depth = depth;
            Tag = t;
            ToolTip = new TagToolTip(t);
            IsExpanded = false;
        }

        /// <summary>
        ///     フォルダノードのコンストラクタ。
        /// </summary>
        public TagTreeNode(string path, bool isExpanded, int depth)
        {
            FullPath = path;
            Depth = depth;
            IsExpanded = isExpanded;
            Counter = 1;
        }

        /// <summary>
        ///     フィルタ辞書に基づいてタグのフィルタモードを再帰的に更新する。
        /// </summary>
        public void UpdateFilterMode(Dictionary<string, Models.FilterMode> filters)
        {
            if (Tag == null)
            {
                foreach (var child in Children)
                    child.UpdateFilterMode(filters);
            }
            else
            {
                FilterMode = filters.GetValueOrDefault(FullPath, Models.FilterMode.None);
            }
        }

        /// <summary>
        ///     タグリストからツリー構造を構築する。パスの'/'区切りでフォルダ階層を生成する。
        /// </summary>
        public static List<TagTreeNode> Build(List<Models.Tag> tags, HashSet<string> expanded)
        {
            var nodes = new List<TagTreeNode>();
            var folders = new Dictionary<string, TagTreeNode>();

            foreach (var tag in tags)
            {
                var sepIdx = tag.Name.IndexOf('/');
                if (sepIdx == -1)
                {
                    // パス区切りなし = トップレベルのタグ
                    nodes.Add(new TagTreeNode(tag, 0));
                }
                else
                {
                    TagTreeNode lastFolder = null;
                    int depth = 0;

                    // パスの各セグメントに対してフォルダノードを作成または取得
                    while (sepIdx != -1)
                    {
                        var folder = tag.Name.Substring(0, sepIdx);
                        if (folders.TryGetValue(folder, out var value))
                        {
                            lastFolder = value;
                            lastFolder.Counter++;
                        }
                        else if (lastFolder == null)
                        {
                            lastFolder = new TagTreeNode(folder, expanded.Contains(folder), depth);
                            folders.Add(folder, lastFolder);
                            InsertFolder(nodes, lastFolder);
                        }
                        else
                        {
                            var cur = new TagTreeNode(folder, expanded.Contains(folder), depth);
                            folders.Add(folder, cur);
                            InsertFolder(lastFolder.Children, cur);
                            lastFolder = cur;
                        }

                        depth++;
                        sepIdx = tag.Name.IndexOf('/', sepIdx + 1);
                    }

                    lastFolder?.Children.Add(new TagTreeNode(tag, depth));
                }
            }

            folders.Clear();
            return nodes;
        }

        /// <summary>
        ///     フォルダノードをコレクション内の適切な位置（非フォルダノードの前）に挿入する。
        /// </summary>
        private static void InsertFolder(List<TagTreeNode> collection, TagTreeNode subFolder)
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

        private Models.FilterMode _filterMode = Models.FilterMode.None;
        private CornerRadius _cornerRadius = new CornerRadius(4);
        private bool _isExpanded = true;
    }

    /// <summary>
    ///     リスト表示用のタグアイテム。選択状態やフィルタモードを管理する。
    /// </summary>
    public class TagListItem : ObservableObject
    {
        /// <summary>
        ///     対応するタグモデル。
        /// </summary>
        public Models.Tag Tag
        {
            get;
            set;
        }

        /// <summary>
        ///     選択されているかどうか。
        /// </summary>
        public bool IsSelected
        {
            get;
            set;
        }

        /// <summary>
        ///     コミットグラフのフィルタモード。
        /// </summary>
        public Models.FilterMode FilterMode
        {
            get => _filterMode;
            set => SetProperty(ref _filterMode, value);
        }

        /// <summary>
        ///     ツールチップ表示用のタグ情報。
        /// </summary>
        public TagToolTip ToolTip
        {
            get;
            set;
        }

        /// <summary>
        ///     選択状態の連続表示に使用する角丸半径。
        /// </summary>
        public CornerRadius CornerRadius
        {
            get => _cornerRadius;
            set => SetProperty(ref _cornerRadius, value);
        }

        private Models.FilterMode _filterMode = Models.FilterMode.None;
        private CornerRadius _cornerRadius = new CornerRadius(4);
    }

    /// <summary>
    ///     タグをフラットリスト形式で表示するコレクション。
    ///     選択状態の管理と連続選択の角丸表示を処理する。
    /// </summary>
    public class TagCollectionAsList
    {
        /// <summary>
        ///     タグアイテムのリスト。
        /// </summary>
        public List<TagListItem> TagItems
        {
            get;
            set;
        } = [];

        /// <summary>
        ///     コンストラクタ。タグモデルリストからリストアイテムを生成する。
        /// </summary>
        public TagCollectionAsList(List<Models.Tag> tags)
        {
            foreach (var tag in tags)
                TagItems.Add(new TagListItem() { Tag = tag, ToolTip = new TagToolTip(tag) });
        }

        /// <summary>
        ///     全アイテムの選択状態をクリアする。
        /// </summary>
        public void ClearSelection()
        {
            foreach (var item in TagItems)
            {
                item.IsSelected = false;
                item.CornerRadius = new CornerRadius(4);
            }
        }

        /// <summary>
        ///     選択アイテムリストに基づいて選択状態と角丸を更新する。
        ///     連続選択されたアイテム間の角丸を調整して視覚的に一体化させる。
        /// </summary>
        public void UpdateSelection(IList selectedItems)
        {
            var set = new HashSet<string>();
            foreach (var item in selectedItems)
            {
                if (item is TagListItem tagItem)
                    set.Add(tagItem.Tag.Name);
            }

            TagListItem last = null;
            foreach (var item in TagItems)
            {
                item.IsSelected = set.Contains(item.Tag.Name);
                if (item.IsSelected)
                {
                    // 前のアイテムも選択中なら角丸を調整して連続表示
                    if (last is { IsSelected: true })
                    {
                        last.CornerRadius = new CornerRadius(last.CornerRadius.TopLeft, 0);
                        item.CornerRadius = new CornerRadius(0, 4);
                    }
                    else
                    {
                        item.CornerRadius = new CornerRadius(4);
                    }
                }
                else
                {
                    item.CornerRadius = new CornerRadius(4);
                }

                last = item;
            }
        }
    }

    /// <summary>
    ///     タグをツリー形式で表示するコレクション。
    ///     展開状態を保持しながらツリーとフラット行リストを管理する。
    /// </summary>
    public class TagCollectionAsTree
    {
        /// <summary>
        ///     ツリーのルートノードリスト。
        /// </summary>
        public List<TagTreeNode> Tree
        {
            get;
            set;
        } = [];

        /// <summary>
        ///     展開状態に基づくフラット化された行リスト（UIバインディング用）。
        /// </summary>
        public AvaloniaList<TagTreeNode> Rows
        {
            get;
            set;
        } = [];

        /// <summary>
        ///     タグリストからツリーコレクションを構築する。以前の展開状態を引き継ぐ。
        /// </summary>
        public static TagCollectionAsTree Build(List<Models.Tag> tags, TagCollectionAsTree old)
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

            var collection = new TagCollectionAsTree();
            collection.Tree = TagTreeNode.Build(tags, oldExpanded);

            var rows = new List<TagTreeNode>();
            MakeTreeRows(rows, collection.Tree);
            collection.Rows.AddRange(rows);

            return collection;
        }

        /// <summary>
        ///     ノードの展開/折りたたみを切り替え、行リストを更新する。
        /// </summary>
        public void ToggleExpand(TagTreeNode node)
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
                var subrows = new List<TagTreeNode>();
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
        ///     全ノードの選択状態をクリアする。
        /// </summary>
        public void ClearSelection()
        {
            foreach (var node in Tree)
                ClearSelectionRecursively(node);
        }

        /// <summary>
        ///     選択アイテムリストに基づいて選択状態と角丸を更新する。
        /// </summary>
        public void UpdateSelection(IList selectedItems)
        {
            var set = new HashSet<string>();
            foreach (var item in selectedItems)
            {
                if (item is TagTreeNode node)
                    set.Add(node.FullPath);
            }

            TagTreeNode last = null;
            foreach (var row in Rows)
            {
                row.IsSelected = set.Contains(row.FullPath);
                if (row.IsSelected)
                {
                    if (last is { IsSelected: true })
                    {
                        last.CornerRadius = new CornerRadius(last.CornerRadius.TopLeft, 0);
                        row.CornerRadius = new CornerRadius(0, 4);
                    }
                    else
                    {
                        row.CornerRadius = new CornerRadius(4);
                    }
                }
                else
                {
                    row.CornerRadius = new CornerRadius(4);
                }

                last = row;
            }
        }

        /// <summary>
        ///     ツリーノードを再帰的にフラット行リストに変換する。
        /// </summary>
        private static void MakeTreeRows(List<TagTreeNode> rows, List<TagTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                rows.Add(node);

                if (!node.IsExpanded || !node.IsFolder)
                    continue;

                MakeTreeRows(rows, node.Children);
            }
        }

        /// <summary>
        ///     ノードの選択状態を再帰的にクリアする。
        /// </summary>
        private static void ClearSelectionRecursively(TagTreeNode node)
        {
            if (node.IsSelected)
            {
                node.IsSelected = false;
                node.CornerRadius = new CornerRadius(4);
            }

            foreach (var child in node.Children)
                ClearSelectionRecursively(child);
        }
    }
}
