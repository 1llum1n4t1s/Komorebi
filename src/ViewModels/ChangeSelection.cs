using System.Collections;
using System.Collections.Generic;

namespace Komorebi.ViewModels;

/// <summary>既存の変更リスト契約を保ち、フォルダーを選択したことも保持する。</summary>
public sealed class ChangeSelection : List<Models.Change>
{
    public bool IsSingleFolder { get; }
    public string SingleFolderPath { get; } = string.Empty;

    public ChangeSelection(IList selected)
    {
        if (selected is { Count: 1 } && selected[0] is ChangeTreeNode { IsFolder: true } folder)
        {
            IsSingleFolder = true;
            SingleFolderPath = folder.FullPath;
        }

        if (selected is null)
            return;

        // 全選択ではフォルダーとファイルの両方から同じ変更へ到達するため、重複判定は O(1) の集合で行う。
        var seen = new HashSet<Models.Change>();
        foreach (var item in selected)
        {
            if (item is Models.Change change)
            {
                if (seen.Add(change))
                    Add(change);
            }
            else if (item is ChangeTreeNode node)
            {
                Collect(node, seen);
            }
        }
    }

    private void Collect(ChangeTreeNode node, HashSet<Models.Change> seen)
    {
        if (node.IsFolder)
        {
            foreach (var child in node.Children)
                Collect(child, seen);
        }
        else if (seen.Add(node.Change))
        {
            Add(node.Change);
        }
    }
}
