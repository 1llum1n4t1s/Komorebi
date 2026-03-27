using System.Collections.Generic;

using Avalonia.Collections;

namespace Komorebi.ViewModels;

/// <summary>
///     変更ファイルをツリー形式で管理するコレクション。
///     フォルダ階層を持つツリーノードと、フラットな行リストを保持する。
/// </summary>
public class ChangeCollectionAsTree
{
    /// <summary>ツリー構造のルートノードリスト</summary>
    public List<ChangeTreeNode> Tree { get; set; } = new List<ChangeTreeNode>();
    /// <summary>ツリーを展開した行リスト（表示用）</summary>
    public AvaloniaList<ChangeTreeNode> Rows { get; set; } = new AvaloniaList<ChangeTreeNode>();
    /// <summary>選択された行のリスト</summary>
    public AvaloniaList<ChangeTreeNode> SelectedRows { get; set; } = new AvaloniaList<ChangeTreeNode>();
}

/// <summary>
///     変更ファイルをグリッド形式で管理するコレクション。
///     フラットな変更リストとして扱う。
/// </summary>
public class ChangeCollectionAsGrid
{
    /// <summary>変更ファイルのフラットリスト</summary>
    public AvaloniaList<Models.Change> Changes { get; set; } = new AvaloniaList<Models.Change>();
    /// <summary>選択された変更ファイルのリスト</summary>
    public AvaloniaList<Models.Change> SelectedChanges { get; set; } = new AvaloniaList<Models.Change>();
}

/// <summary>
///     変更ファイルをリスト形式で管理するコレクション。
///     フラットな変更リストとして扱う。
/// </summary>
public class ChangeCollectionAsList
{
    /// <summary>変更ファイルのフラットリスト</summary>
    public AvaloniaList<Models.Change> Changes { get; set; } = new AvaloniaList<Models.Change>();
    /// <summary>選択された変更ファイルのリスト</summary>
    public AvaloniaList<Models.Change> SelectedChanges { get; set; } = new AvaloniaList<Models.Change>();
}
