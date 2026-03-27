using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
///     ウェルカムページのViewModel。
///     リポジトリツリーの表示、検索、ノード管理、クローン、初期化などの操作を提供する。
/// </summary>
public class Welcome : ObservableObject
{
    /// <summary>シングルトンインスタンス。</summary>
    public static Welcome Instance { get; } = new();

    /// <summary>
    ///     表示用のフラット化されたリポジトリノード行リスト。
    /// </summary>
    public AvaloniaList<RepositoryNode> Rows
    {
        get;
        private set;
    } = [];

    /// <summary>
    ///     リポジトリの検索フィルタ文字列。変更時に自動的にリフレッシュされる。
    /// </summary>
    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
                Refresh();
        }
    }

    /// <summary>
    ///     コンストラクタ。初期表示のためにリフレッシュを実行する。
    /// </summary>
    public Welcome()
    {
        Refresh();
    }

    /// <summary>
    ///     リポジトリツリーの表示を更新する。検索フィルタに応じて可視性を設定し、行リストを再構築する。
    /// </summary>
    public void Refresh()
    {
        if (string.IsNullOrWhiteSpace(_searchFilter))
        {
            // フィルタなし: 全ノードを表示
            foreach (var node in Preferences.Instance.RepositoryNodes)
                ResetVisibility(node);
        }
        else
        {
            // フィルタあり: 検索条件に基づいて可視性を設定
            foreach (var node in Preferences.Instance.RepositoryNodes)
                SetVisibilityBySearch(node);
        }

        var rows = new List<RepositoryNode>();
        MakeTreeRows(rows, Preferences.Instance.RepositoryNodes);
        Rows.Clear();
        Rows.AddRange(rows);
    }

    /// <summary>
    ///     全リポジトリノードの状態を非同期で更新する。
    ///     既に更新中の場合は重複実行を防止する。
    /// </summary>
    public async Task UpdateStatusAsync(bool force, CancellationToken? token)
    {
        if (_isUpdatingStatus)
            return;

        _isUpdatingStatus = true;

        // 列挙中のコレクション変更を回避するためコピーを作成
        var nodes = new List<RepositoryNode>();
        nodes.AddRange(Preferences.Instance.RepositoryNodes);

        foreach (var node in nodes)
            await node.UpdateStatusAsync(force, token);

        _isUpdatingStatus = false;
    }

    /// <summary>
    ///     ノードの展開/折りたたみを切り替え、行リストを更新する。
    /// </summary>
    public void ToggleNodeIsExpanded(RepositoryNode node)
    {
        node.IsExpanded = !node.IsExpanded;

        var depth = node.Depth;
        var idx = Rows.IndexOf(node);
        if (idx == -1)
            return;

        if (node.IsExpanded)
        {
            // 展開: 子ノードを行リストに挿入
            var subrows = new List<RepositoryNode>();
            MakeTreeRows(subrows, node.SubNodes, depth + 1);
            Rows.InsertRange(idx + 1, subrows);
        }
        else
        {
            // 折りたたみ: 子ノードを行リストから削除
            var removeCount = 0;
            for (int i = idx + 1; i < Rows.Count; i++)
            {
                var row = Rows[i];
                if (row.Depth <= depth)
                    break;

                removeCount++;
            }
            Rows.RemoveRange(idx + 1, removeCount);
        }
    }

    /// <summary>
    ///     指定パスからgitリポジトリのルートディレクトリを非同期で取得する。
    ///     ベアリポジトリ、通常リポジトリの両方に対応する。
    /// </summary>
    public async Task<string> GetRepositoryRootAsync(string path)
    {
        if (!Preferences.Instance.IsGitConfigured())
        {
            App.RaiseException(string.Empty, App.Text("NotConfigured"));
            return null;
        }

        var root = path;
        if (!Directory.Exists(root))
        {
            // ファイルの場合は親ディレクトリを使用
            if (File.Exists(root))
                root = Path.GetDirectoryName(root);
            else
                return null;
        }

        // ベアリポジトリの場合はそのまま返す
        var isBare = await new Commands.IsBareRepository(root).GetResultAsync();
        if (isBare)
            return root;

        // 通常リポジトリのルートパスを検索
        var rs = await new Commands.QueryRepositoryRootPath(root).GetResultAsync();
        if (!rs.IsSuccess || string.IsNullOrWhiteSpace(rs.StdOut))
            return null;

        return rs.StdOut.Trim();
    }

    /// <summary>
    ///     新しいgitリポジトリの初期化ダイアログを表示する。
    /// </summary>
    public void InitRepository(string path, RepositoryNode parent, string reason)
    {
        if (!Preferences.Instance.IsGitConfigured())
        {
            App.RaiseException(string.Empty, App.Text("NotConfigured"));
            return;
        }

        var activePage = App.GetLauncher().ActivePage;
        if (activePage is not null && activePage.CanCreatePopup())
            activePage.Popup = new Init(activePage.Node.Id, path, parent, reason);
    }

    /// <summary>
    ///     リポジトリをツリーに追加し、オプションで開く。
    /// </summary>
    public async Task AddRepositoryAsync(string path, RepositoryNode parent, bool moveNode, bool open)
    {
        var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(path, parent, moveNode);
        await node.UpdateStatusAsync(false, null);

        if (open)
            node.Open();
    }

    /// <summary>
    ///     リポジトリのクローンダイアログを表示する。
    /// </summary>
    public void Clone()
    {
        if (!Preferences.Instance.IsGitConfigured())
        {
            App.RaiseException(string.Empty, App.Text("NotConfigured"));
            return;
        }

        var activePage = App.GetLauncher().ActivePage;
        if (activePage is not null && activePage.CanCreatePopup())
            activePage.Popup = new Clone(activePage.Node.Id);
    }

    /// <summary>
    ///     ターミナルを開く。
    /// </summary>
    public void OpenTerminal()
    {
        if (!Preferences.Instance.IsGitConfigured())
            App.RaiseException(string.Empty, App.Text("NotConfigured"));
        else
            Native.OS.OpenTerminal(null);
    }

    /// <summary>
    ///     デフォルトクローンディレクトリをスキャンしてリポジトリを検出するダイアログを表示する。
    /// </summary>
    public void ScanDefaultCloneDir()
    {
        if (!Preferences.Instance.IsGitConfigured())
        {
            App.RaiseException(string.Empty, App.Text("NotConfigured"));
            return;
        }

        var activePage = App.GetLauncher().ActivePage;
        if (activePage is not null && activePage.CanCreatePopup())
            activePage.Popup = new ScanRepositories();
    }

    /// <summary>
    ///     検索フィルタをクリアする。
    /// </summary>
    public void ClearSearchFilter()
    {
        SearchFilter = string.Empty;
    }

    /// <summary>
    ///     ルートレベルに新しいグループノードを作成するダイアログを表示する。
    /// </summary>
    public void AddRootNode()
    {
        var activePage = App.GetLauncher().ActivePage;
        if (activePage is not null && activePage.CanCreatePopup())
            activePage.Popup = new CreateGroup(null);
    }

    /// <summary>
    ///     IDでリポジトリノードを再帰的に検索する。
    /// </summary>
    public RepositoryNode FindNodeById(string id, RepositoryNode root = null)
    {
        var collection = (root is null) ? Preferences.Instance.RepositoryNodes : root.SubNodes;
        foreach (var node in collection)
        {
            if (node.Id.Equals(id, StringComparison.Ordinal))
                return node;

            var sub = FindNodeById(id, node);
            if (sub is not null)
                return sub;
        }

        return null;
    }

    /// <summary>
    ///     指定ノードの親グループを再帰的に検索する。
    /// </summary>
    public RepositoryNode FindParentGroup(RepositoryNode node, RepositoryNode group = null)
    {
        var collection = (group is null) ? Preferences.Instance.RepositoryNodes : group.SubNodes;
        if (collection.Contains(node))
            return group;

        foreach (var item in collection)
        {
            if (!item.IsRepository)
            {
                var parent = FindParentGroup(node, item);
                if (parent is not null)
                    return parent;
            }
        }

        return null;
    }

    /// <summary>
    ///     ノードを別の場所に移動し、表示をリフレッシュする。
    /// </summary>
    public void MoveNode(RepositoryNode from, RepositoryNode to)
    {
        Preferences.Instance.MoveNode(from, to, true);
        Refresh();
    }

    /// <summary>
    ///     ノードとその子ノードの可視性を全てリセット（表示）する。
    /// </summary>
    private void ResetVisibility(RepositoryNode node)
    {
        node.IsVisible = true;
        foreach (var subNode in node.SubNodes)
            ResetVisibility(subNode);
    }

    /// <summary>
    ///     検索フィルタに基づいてノードの可視性を再帰的に設定する。
    ///     グループノードは名前一致またはサブノードの可視性で判定する。
    /// </summary>
    private void SetVisibilityBySearch(RepositoryNode node)
    {
        if (!node.IsRepository)
        {
            // グループ名が検索条件に一致する場合は配下を全表示
            if (node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
            {
                node.IsVisible = true;
                foreach (var subNode in node.SubNodes)
                    ResetVisibility(subNode);
            }
            else
            {
                // 子ノードに可視のものがあればグループも表示
                bool hasVisibleSubNode = false;
                foreach (var subNode in node.SubNodes)
                {
                    SetVisibilityBySearch(subNode);
                    hasVisibleSubNode |= subNode.IsVisible;
                }
                node.IsVisible = hasVisibleSubNode;
            }
        }
        else
        {
            // リポジトリノードは名前またはIDで検索
            node.IsVisible = node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                node.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    ///     ツリーノードを再帰的にフラット行リストに変換する。可視ノードのみ含む。
    /// </summary>
    private void MakeTreeRows(List<RepositoryNode> rows, List<RepositoryNode> nodes, int depth = 0)
    {
        foreach (var node in nodes)
        {
            if (!node.IsVisible)
                continue;

            node.Depth = depth;
            rows.Add(node);

            if (node.IsRepository || !node.IsExpanded)
                continue;

            MakeTreeRows(rows, node.SubNodes, depth + 1);
        }
    }

    private string _searchFilter = string.Empty;
    private bool _isUpdatingStatus = false;
}
