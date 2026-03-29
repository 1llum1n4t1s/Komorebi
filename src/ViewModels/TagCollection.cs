using System.Collections;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// タグのツールチップ表示用データ。注釈付きタグの作成者やメッセージを保持する。
/// </summary>
public class TagToolTip
{
    /// <summary>タグ名。</summary>
    public string Name { get; private set; }
    /// <summary>注釈付きタグかどうか。</summary>
    public bool IsAnnotated { get; private set; }
    /// <summary>タグの作成者。</summary>
    public Models.User Creator { get; private set; }
    /// <summary>タグの作成日時（Unix時間）。</summary>
    public ulong CreatorDate { get; private set; }
    /// <summary>タグのメッセージ。</summary>
    public string Message { get; private set; }

    /// <summary>タグモデルからツールチップデータを構築する。</summary>
    public TagToolTip(Models.Tag t)
    {
        Name = t.Name;
        IsAnnotated = t.IsAnnotated;
        Creator = t.Creator;
        CreatorDate = t.CreatorDate;
        Message = t.Message;
    }
}

/// <summary>
/// タグツリー内のノード。フォルダまたはタグ自体を表す。
/// パスの区切り文字に基づいて階層構造を形成する。
/// </summary>
public class TagTreeNode : ObservableObject
{
    /// <summary>ノードのフルパス。</summary>
    public string FullPath { get; private set; }
    /// <summary>ツリー内のネスト深度。</summary>
    public int Depth { get; private set; } = 0;
    /// <summary>対応するタグモデル。フォルダノードの場合はnull。</summary>
    public Models.Tag Tag { get; private set; } = null;
    /// <summary>タグのツールチップ情報。</summary>
    public TagToolTip ToolTip { get; private set; } = null;
    /// <summary>子ノードのリスト。</summary>
    public List<TagTreeNode> Children { get; private set; } = [];
    /// <summary>配下のタグ数カウンタ。</summary>
    public int Counter { get; set; } = 0;

    /// <summary>フォルダノードかどうか（Tagがnullならフォルダ）。</summary>
    public bool IsFolder
    {
        get => Tag is null;
    }

    /// <summary>選択状態かどうか。</summary>
    public bool IsSelected
    {
        get;
        set;
    }

    /// <summary>フィルタモード（表示/除外/なし）。</summary>
    public Models.FilterMode FilterMode
    {
        get => _filterMode;
        set => SetProperty(ref _filterMode, value);
    }

    /// <summary>角丸半径。連続選択時のUI表示に使用。</summary>
    public CornerRadius CornerRadius
    {
        get => _cornerRadius;
        set => SetProperty(ref _cornerRadius, value);
    }

    /// <summary>ツリーノードが展開されているかどうか。</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>子タグ数の表示文字列。0の場合は空文字。</summary>
    public string TagsCount
    {
        get => Counter > 0 ? $"({Counter})" : string.Empty;
    }

    /// <summary>タグノードのコンストラクタ。</summary>
    public TagTreeNode(Models.Tag t, int depth)
    {
        FullPath = t.Name;
        Depth = depth;
        Tag = t;
        ToolTip = new TagToolTip(t);
        IsExpanded = false;
    }

    /// <summary>フォルダノードのコンストラクタ。</summary>
    public TagTreeNode(string path, bool isExpanded, int depth)
    {
        FullPath = path;
        Depth = depth;
        IsExpanded = isExpanded;
        Counter = 1;
    }

    /// <summary>フィルタモードを再帰的に更新する。</summary>
    public void UpdateFilterMode(Dictionary<string, Models.FilterMode> filters)
    {
        if (Tag is null)
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
    /// タグリストからツリー構造を構築する。
    /// パスの区切り文字に基づいてフォルダ階層を生成する。
    /// </summary>
    public static List<TagTreeNode> Build(List<Models.Tag> tags, HashSet<string> expanded)
    {
        List<TagTreeNode> nodes = [];
        Dictionary<string, TagTreeNode> folders = [];

        foreach (var tag in tags)
        {
            var sepIdx = tag.Name.IndexOf('/');
            if (sepIdx == -1)
            {
                nodes.Add(new TagTreeNode(tag, 0));
            }
            else
            {
                TagTreeNode lastFolder = null;
                int depth = 0;

                while (sepIdx != -1)
                {
                    var folder = tag.Name.Substring(0, sepIdx);
                    if (folders.TryGetValue(folder, out var value))
                    {
                        lastFolder = value;
                        lastFolder.Counter++;
                    }
                    else if (lastFolder is null)
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

    /// <summary>フォルダノードをコレクション内の適切な位置（非フォルダノードの前）に挿入する。</summary>
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
/// タグをフラットリスト表示する際の各項目を表すViewModel。
/// </summary>
public class TagListItem : ObservableObject
{
    /// <summary>対応するタグモデル。</summary>
    public Models.Tag Tag
    {
        get;
        set;
    }

    /// <summary>選択状態かどうか。</summary>
    public bool IsSelected
    {
        get;
        set;
    }

    /// <summary>フィルタモード（表示/除外/なし）。</summary>
    public Models.FilterMode FilterMode
    {
        get => _filterMode;
        set => SetProperty(ref _filterMode, value);
    }

    /// <summary>タグのツールチップ情報。</summary>
    public TagToolTip ToolTip
    {
        get;
        set;
    }

    /// <summary>角丸半径。連続選択時のUI表示に使用。</summary>
    public CornerRadius CornerRadius
    {
        get => _cornerRadius;
        set => SetProperty(ref _cornerRadius, value);
    }

    private Models.FilterMode _filterMode = Models.FilterMode.None;
    private CornerRadius _cornerRadius = new CornerRadius(4);
}

/// <summary>
/// タグをフラットリスト形式で表示するコレクション。
/// </summary>
public class TagCollectionAsList
{
    /// <summary>タグ項目のフラットリスト。</summary>
    public List<TagListItem> TagItems
    {
        get;
        set;
    } = [];

    /// <summary>タグリストからフラットコレクションを構築する。</summary>
    public TagCollectionAsList(List<Models.Tag> tags)
    {
        foreach (var tag in tags)
            TagItems.Add(new TagListItem() { Tag = tag, ToolTip = new TagToolTip(tag) });
    }

    /// <summary>全項目の選択状態をクリアする。</summary>
    public void ClearSelection()
    {
        foreach (var item in TagItems)
        {
            item.IsSelected = false;
            item.CornerRadius = new CornerRadius(4);
        }
    }

    /// <summary>選択項目のリストに基づいて選択状態と角丸を更新する。</summary>
    public void UpdateSelection(IList selectedItems)
    {
        HashSet<string> set = [];
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
                // 連続選択時は角丸を調整して視覚的にグループ化
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
/// タグをツリー形式で表示するコレクション。
/// 展開状態を保持しながらツリーとフラット行リストを管理する。
/// </summary>
public class TagCollectionAsTree
{
    /// <summary>ツリーのルートノードリスト。</summary>
    public List<TagTreeNode> Tree
    {
        get;
        set;
    } = [];

    /// <summary>展開状態に基づくフラット化された行リスト（UIバインディング用）。</summary>
    public AvaloniaList<TagTreeNode> Rows
    {
        get;
        set;
    } = [];

    /// <summary>タグリストからツリーコレクションを構築する。以前の展開状態を引き継ぐ。</summary>
    public static TagCollectionAsTree Build(List<Models.Tag> tags, TagCollectionAsTree old)
    {
        HashSet<string> oldExpanded = [];
        if (old is not null)
        {
            foreach (var row in old.Rows)
            {
                if (row.IsFolder && row.IsExpanded)
                    oldExpanded.Add(row.FullPath);
            }
        }

        var collection = new TagCollectionAsTree();
        collection.Tree = TagTreeNode.Build(tags, oldExpanded);

        List<TagTreeNode> rows = [];
        MakeTreeRows(rows, collection.Tree);
        collection.Rows.AddRange(rows);

        return collection;
    }

    /// <summary>ノードの展開/折りたたみを切り替え、行リストを更新する。</summary>
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
            List<TagTreeNode> subrows = [];
            MakeTreeRows(subrows, node.Children);
            rows.InsertRange(idx + 1, subrows);
        }
        else
        {
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

    /// <summary>全ノードの選択状態を再帰的にクリアする。</summary>
    public void ClearSelection()
    {
        foreach (var node in Tree)
            ClearSelectionRecursively(node);
    }

    /// <summary>選択項目のリストに基づいて選択状態と角丸を更新する。</summary>
    public void UpdateSelection(IList selectedItems)
    {
        HashSet<string> set = [];
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

    /// <summary>ツリーノードを再帰的にフラット行リストに変換する。展開されたフォルダの子のみ含む。</summary>
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

    /// <summary>ノードの選択状態を再帰的にクリアする。</summary>
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
