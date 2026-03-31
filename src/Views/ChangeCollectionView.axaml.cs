using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Komorebi.Views;

/// <summary>
/// ChangeTreeNodeToggleButtonクラス。
/// </summary>
public class ChangeTreeNodeToggleButton : ToggleButton
{
    /// <summary>スタイルキーをToggleButtonに設定。</summary>
    protected override Type StyleKeyOverride => typeof(ToggleButton);

    /// <summary>
    /// ポインターが押された際のイベント処理。
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            DataContext is ViewModels.ChangeTreeNode { IsFolder: true } node)
        {
            var tree = this.FindAncestorOfType<ChangeCollectionView>();
            tree?.ToggleNodeIsExpanded(node);
        }

        e.Handled = true;
    }
}

/// <summary>
/// ChangeCollectionContainerクラス。
/// </summary>
public class ChangeCollectionContainer : ListBox
{
    /// <summary>スタイルキーをListBoxに設定。</summary>
    protected override Type StyleKeyOverride => typeof(ListBox);

    /// <summary>
    /// キーが押された際のイベント処理。
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (SelectedItems is [ViewModels.ChangeTreeNode node])
        {
            if (((e.Key == Key.Left && node.IsExpanded) || (e.Key == Key.Right && !node.IsExpanded)) &&
                e.KeyModifiers == KeyModifiers.None)
            {
                this.FindAncestorOfType<ChangeCollectionView>()?.ToggleNodeIsExpanded(node);
                e.Handled = true;
            }
        }

        if (!e.Handled && e.Key != Key.Space && e.Key != Key.Enter)
            base.OnKeyDown(e);
    }
}

/// <summary>
/// 変更ファイルコレクションビューのコードビハインド。
/// </summary>
public partial class ChangeCollectionView : UserControl
{
    /// <summary>ステージ前（WorkTree）の変更かどうかを保持するスタイルプロパティ。</summary>
    public static readonly StyledProperty<bool> IsUnstagedChangeProperty =
        AvaloniaProperty.Register<ChangeCollectionView, bool>(nameof(IsUnstagedChange));

    /// <summary>ステージ前の変更であるかどうか。ツールチップ表示時の状態説明に影響する。</summary>
    public bool IsUnstagedChange
    {
        get => GetValue(IsUnstagedChangeProperty);
        set => SetValue(IsUnstagedChangeProperty, value);
    }

    /// <summary>変更ファイルの表示モード（ツリー/グリッド/リスト）を保持するスタイルプロパティ。</summary>
    public static readonly StyledProperty<Models.ChangeViewMode> ViewModeProperty =
        AvaloniaProperty.Register<ChangeCollectionView, Models.ChangeViewMode>(nameof(ViewMode), Models.ChangeViewMode.Tree);

    /// <summary>変更ファイルの表示モード。</summary>
    public Models.ChangeViewMode ViewMode
    {
        get => GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    /// <summary>フォルダのコンパクト表示を有効にするかのスタイルプロパティ。</summary>
    public static readonly StyledProperty<bool> EnableCompactFoldersProperty =
        AvaloniaProperty.Register<ChangeCollectionView, bool>(nameof(EnableCompactFolders));

    /// <summary>子が1つだけのフォルダを連結して表示するかどうか。</summary>
    public bool EnableCompactFolders
    {
        get => GetValue(EnableCompactFoldersProperty);
        set => SetValue(EnableCompactFoldersProperty, value);
    }

    /// <summary>変更ファイル一覧を保持するスタイルプロパティ。</summary>
    public static readonly StyledProperty<List<Models.Change>> ChangesProperty =
        AvaloniaProperty.Register<ChangeCollectionView, List<Models.Change>>(nameof(Changes));

    /// <summary>表示対象の変更ファイル一覧。</summary>
    public List<Models.Change> Changes
    {
        get => GetValue(ChangesProperty);
        set => SetValue(ChangesProperty, value);
    }

    /// <summary>選択中の変更ファイル一覧を保持するスタイルプロパティ。</summary>
    public static readonly StyledProperty<List<Models.Change>> SelectedChangesProperty =
        AvaloniaProperty.Register<ChangeCollectionView, List<Models.Change>>(nameof(SelectedChanges));

    /// <summary>現在選択中の変更ファイル一覧。</summary>
    public List<Models.Change> SelectedChanges
    {
        get => GetValue(SelectedChangesProperty);
        set => SetValue(SelectedChangesProperty, value);
    }

    /// <summary>変更ファイルがダブルタップされた際のルーティングイベント。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ChangeDoubleTappedEvent =
        RoutedEvent.Register<ChangeCollectionView, RoutedEventArgs>(nameof(ChangeDoubleTapped), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

    /// <summary>変更ファイルがダブルタップされた際に発火するイベント。</summary>
    public event EventHandler<RoutedEventArgs> ChangeDoubleTapped
    {
        add { AddHandler(ChangeDoubleTappedEvent, value); }
        remove { RemoveHandler(ChangeDoubleTappedEvent, value); }
    }

    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public ChangeCollectionView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// ToggleNodeIsExpandedの処理を行う。
    /// </summary>
    public void ToggleNodeIsExpanded(ViewModels.ChangeTreeNode node)
    {
        if (Content is ViewModels.ChangeCollectionAsTree tree && node.IsFolder)
        {
            node.IsExpanded = !node.IsExpanded;

            var depth = node.Depth;
            var idx = tree.Rows.IndexOf(node);
            if (idx == -1)
                return;

            if (node.IsExpanded)
            {
                List<ViewModels.ChangeTreeNode> subrows = [];
                MakeTreeRows(subrows, node.Children);
                tree.Rows.InsertRange(idx + 1, subrows);
            }
            else
            {
                var removeCount = 0;
                for (int i = idx + 1; i < tree.Rows.Count; i++)
                {
                    var row = tree.Rows[i];
                    if (row.Depth <= depth)
                        break;

                    removeCount++;
                }

                tree.Rows.RemoveRange(idx + 1, removeCount);
            }
        }
    }

    /// <summary>
    /// GetNextChangeWithoutSelectionの処理を行う。
    /// </summary>
    public Models.Change GetNextChangeWithoutSelection()
    {
        var selected = SelectedChanges;
        var changes = Changes;
        if (selected is null || selected.Count == 0)
            return changes.Count > 0 ? changes[0] : null;
        if (selected.Count == changes.Count)
            return null;

        var set = new HashSet<string>();
        foreach (var c in selected)
        {
            if (!c.IsConflicted)
                set.Add(c.Path);
        }

        if (Content is ViewModels.ChangeCollectionAsTree tree)
        {
            var lastUnselected = -1;
            for (int i = tree.Rows.Count - 1; i >= 0; i--)
            {
                var row = tree.Rows[i];
                if (!row.IsFolder)
                {
                    if (set.Contains(row.FullPath))
                    {
                        if (lastUnselected == -1)
                            continue;

                        break;
                    }

                    lastUnselected = i;
                }
            }

            if (lastUnselected != -1)
                return tree.Rows[lastUnselected].Change;
        }
        else
        {
            var lastUnselected = -1;
            for (int i = changes.Count - 1; i >= 0; i--)
            {
                if (set.Contains(changes[i].Path))
                {
                    if (lastUnselected == -1)
                        continue;

                    break;
                }

                lastUnselected = i;
            }

            if (lastUnselected != -1)
                return changes[lastUnselected];
        }

        return null;
    }

    /// <summary>
    /// TakeFocusの処理を行う。
    /// </summary>
    public void TakeFocus()
    {
        var container = this.FindDescendantOfType<ChangeCollectionContainer>();
        if (container is { IsFocused: false })
            container.Focus();
    }

    /// <summary>
    /// プロパティが変更された際の処理。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ViewModeProperty)
            UpdateDataSource(true);
        else if (change.Property == ChangesProperty)
            UpdateDataSource(false);
        else if (change.Property == SelectedChangesProperty)
            UpdateSelection();

        if (change.Property == EnableCompactFoldersProperty && ViewMode == Models.ChangeViewMode.Tree)
            UpdateDataSource(true);
    }

    /// <summary>
    /// RowDataContextChangedイベントのハンドラ。
    /// </summary>
    private void OnRowDataContextChanged(object sender, EventArgs e)
    {
        if (sender is not Control control)
            return;

        if (control.DataContext is ViewModels.ChangeTreeNode node)
        {
            if (node.Change is { } c)
                UpdateRowTips(control, c);
            else
                ToolTip.SetTip(control, node.FullPath);
        }
        else if (control.DataContext is Models.Change change)
        {
            UpdateRowTips(control, change);
        }
        else
        {
            ToolTip.SetTip(control, null);
        }
    }

    /// <summary>
    /// RowDoubleTappedイベントのハンドラ。
    /// </summary>
    private void OnRowDoubleTapped(object sender, TappedEventArgs e)
    {
        var grid = sender as Grid;
        if (grid?.DataContext is ViewModels.ChangeTreeNode node)
        {
            if (node.IsFolder)
            {
                var posX = e.GetPosition(this).X;
                if (posX < node.Depth * 16 + 16)
                    return;

                ToggleNodeIsExpanded(node);
            }
            else
            {
                RaiseEvent(new RoutedEventArgs(ChangeDoubleTappedEvent));
            }
        }
        else if (grid?.DataContext is Models.Change)
        {
            RaiseEvent(new RoutedEventArgs(ChangeDoubleTappedEvent));
        }
    }

    /// <summary>
    /// RowSelectionChangedイベントのハンドラ。
    /// </summary>
    private void OnRowSelectionChanged(object sender, SelectionChangedEventArgs _)
    {
        if (_disableSelectionChangingEvent)
            return;

        _disableSelectionChangingEvent = true;

        List<Models.Change> selected = [];
        if (sender is ListBox { SelectedItems: { } selectedItems })
        {
            foreach (var item in selectedItems)
            {
                if (item is Models.Change c)
                    selected.Add(c);
                else if (item is ViewModels.ChangeTreeNode node)
                    CollectChangesInNode(selected, node);
            }
        }

        var old = SelectedChanges ?? [];
        if (old.Count != selected.Count)
        {
            SetCurrentValue(SelectedChangesProperty, selected);
        }
        else
        {
            bool allEquals = true;
            foreach (var c in old)
            {
                if (!selected.Contains(c))
                {
                    allEquals = false;
                    break;
                }
            }

            if (!allEquals)
                SetCurrentValue(SelectedChangesProperty, selected);
        }

        _disableSelectionChangingEvent = false;
    }

    /// <summary>
    /// MakeTreeRowsの処理を行う。
    /// </summary>
    private static void MakeTreeRows(List<ViewModels.ChangeTreeNode> rows, List<ViewModels.ChangeTreeNode> nodes)
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
    /// UpdateDataSourceの処理を行う。
    /// </summary>
    private void UpdateDataSource(bool onlyViewModeChange)
    {
        _disableSelectionChangingEvent = !onlyViewModeChange;

        var changes = Changes;
        if (changes is null || changes.Count == 0)
        {
            Content = null;
            _disableSelectionChangingEvent = false;
            return;
        }

        var selected = SelectedChanges ?? [];
        if (ViewMode == Models.ChangeViewMode.Tree)
        {
            HashSet<string> oldFolded = new HashSet<string>();
            if (Content is ViewModels.ChangeCollectionAsTree oldTree)
            {
                foreach (var row in oldTree.Rows)
                {
                    if (row.IsFolder && !row.IsExpanded)
                        oldFolded.Add(row.FullPath);
                }
            }

            var tree = new ViewModels.ChangeCollectionAsTree();
            tree.Tree = ViewModels.ChangeTreeNode.Build(changes, oldFolded, EnableCompactFolders);

            List<ViewModels.ChangeTreeNode> rows = [];
            MakeTreeRows(rows, tree.Tree);
            tree.Rows.AddRange(rows);

            if (selected.Count > 0)
            {
                var sets = new HashSet<Models.Change>(selected);
                List<ViewModels.ChangeTreeNode> nodes = [];
                foreach (var row in tree.Rows)
                {
                    if (row.Change is not null && sets.Contains(row.Change))
                        nodes.Add(row);
                }

                tree.SelectedRows.AddRange(nodes);
            }

            Content = tree;
        }
        else if (ViewMode == Models.ChangeViewMode.Grid)
        {
            var grid = new ViewModels.ChangeCollectionAsGrid();
            grid.Changes.AddRange(changes);
            if (selected.Count > 0)
                grid.SelectedChanges.AddRange(selected);

            Content = grid;
        }
        else
        {
            var list = new ViewModels.ChangeCollectionAsList();
            list.Changes.AddRange(changes);
            if (selected.Count > 0)
                list.SelectedChanges.AddRange(selected);

            Content = list;
        }

        _disableSelectionChangingEvent = false;
    }

    /// <summary>
    /// UpdateSelectionの処理を行う。
    /// </summary>
    private void UpdateSelection()
    {
        if (_disableSelectionChangingEvent)
            return;

        _disableSelectionChangingEvent = true;

        var selected = SelectedChanges ?? [];
        if (Content is ViewModels.ChangeCollectionAsTree tree)
        {
            tree.SelectedRows.Clear();

            if (selected.Count > 0)
            {
                var sets = new HashSet<Models.Change>(selected);

                List<ViewModels.ChangeTreeNode> nodes = [];
                foreach (var row in tree.Rows)
                {
                    if (row.Change is not null && sets.Contains(row.Change))
                        nodes.Add(row);
                }

                tree.SelectedRows.AddRange(nodes);
            }
        }
        else if (Content is ViewModels.ChangeCollectionAsGrid grid)
        {
            grid.SelectedChanges.Clear();
            if (selected.Count > 0)
                grid.SelectedChanges.AddRange(selected);
        }
        else if (Content is ViewModels.ChangeCollectionAsList list)
        {
            list.SelectedChanges.Clear();
            if (selected.Count > 0)
                list.SelectedChanges.AddRange(selected);
        }

        _disableSelectionChangingEvent = false;
    }

    /// <summary>
    /// CollectChangesInNodeの処理を行う。
    /// </summary>
    private static void CollectChangesInNode(List<Models.Change> outs, ViewModels.ChangeTreeNode node)
    {
        if (node.IsFolder)
        {
            foreach (var child in node.Children)
                CollectChangesInNode(outs, child);
        }
        else if (!outs.Contains(node.Change))
        {
            outs.Add(node.Change);
        }
    }

    /// <summary>
    /// UpdateRowTipsの処理を行う。
    /// </summary>
    private void UpdateRowTips(Control control, Models.Change change)
    {
        var tip = new TextBlock() { TextWrapping = TextWrapping.Wrap };
        tip.Inlines!.Add(new Run(change.Path));
        tip.Inlines!.Add(new Run(" • ") { Foreground = Brushes.Gray });
        tip.Inlines!.Add(new Run(IsUnstagedChange ? change.WorkTreeDesc : change.IndexDesc) { Foreground = Brushes.Gray });
        if (change.IsConflicted)
        {
            tip.Inlines!.Add(new Run(" • ") { Foreground = Brushes.Gray });
            tip.Inlines!.Add(new Run(change.ConflictDesc) { Foreground = Brushes.Gray });
        }

        ToolTip.SetTip(control, tip);
    }

    /// <summary>選択変更イベントの再入を防止するフラグ。</summary>
    private bool _disableSelectionChangingEvent = false;
}
