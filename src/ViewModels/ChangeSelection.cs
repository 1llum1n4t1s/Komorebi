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

        foreach (var item in selected)
        {
            if (item is Models.Change change && !Contains(change))
                Add(change);
            else if (item is ChangeTreeNode node)
                Collect(node);
        }
    }

    private void Collect(ChangeTreeNode node)
    {
        if (node.IsFolder)
        {
            foreach (var child in node.Children)
                Collect(child);
        }
        else if (!Contains(node.Change))
        {
            Add(node.Change);
        }
    }
}
