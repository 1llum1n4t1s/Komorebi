using System;
using System.Collections.Generic;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// ブランチツリーのノードを表すViewModel。
/// ローカル・リモートブランチをツリー構造で表示するために使用される。
/// フォルダノードとブランチノードの両方を表現する。
/// </summary>
public class BranchTreeNode : ObservableObject
{
    /// <summary>ノードの表示名</summary>
    public string Name { get; private set; } = string.Empty;
    /// <summary>ノードのフルパス（refs/heads/... や refs/remotes/... 形式）</summary>
    public string Path { get; private set; } = string.Empty;
    /// <summary>バックエンドオブジェクト（Models.BranchまたはModels.Remote）</summary>
    public object Backend { get; private set; } = null;
    /// <summary>ソート用のタイムスタンプ</summary>
    public ulong TimeToSort { get; private set; } = 0;
    /// <summary>ツリーの深さ</summary>
    public int Depth { get; set; } = 0;
    /// <summary>選択状態</summary>
    public bool IsSelected { get; set; } = false;
    /// <summary>子ノードのリスト</summary>
    public List<BranchTreeNode> Children { get; private set; } = [];
    /// <summary>配下のブランチ数カウンター</summary>
    public int Counter { get; set; } = 0;

    /// <summary>
    /// グラフ表示のフィルタモード。
    /// </summary>
    public Models.FilterMode FilterMode
    {
        get => _filterMode;
        set => SetProperty(ref _filterMode, value);
    }

    /// <summary>
    /// ツリーノードの展開状態。
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    /// ノードの角丸半径。UI表示用。
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => _cornerRadius;
        set => SetProperty(ref _cornerRadius, value);
    }

    /// <summary>
    /// このノードがブランチ（フォルダではない）かどうか。
    /// </summary>
    public bool IsBranch
    {
        get => Backend is Models.Branch;
    }

    /// <summary>
    /// このノードが現在チェックアウト中のブランチかどうか。
    /// </summary>
    public bool IsCurrent
    {
        get => Backend is Models.Branch { IsCurrent: true };
    }

    /// <summary>
    /// 上流ブランチが削除済みであることを示すヒントを表示するかどうか。
    /// </summary>
    public bool ShowUpstreamGoneTip
    {
        get => Backend is Models.Branch { IsUpstreamGone: true };
    }

    /// <summary>
    /// 配下のブランチ数を括弧付きで表示する文字列。0の場合は空文字。
    /// </summary>
    public string BranchesCount
    {
        get => Counter > 0 ? $"({Counter})" : string.Empty;
    }

    /// <summary>フィルタモード</summary>
    private Models.FilterMode _filterMode = Models.FilterMode.None;
    /// <summary>展開状態</summary>
    private bool _isExpanded = false;
    /// <summary>角丸半径</summary>
    private CornerRadius _cornerRadius = new CornerRadius(4);

    /// <summary>
    /// ブランチツリーを構築するビルダークラス。
    /// ブランチリストとリモートリストからツリー構造を生成する。
    /// </summary>
    public class Builder
    {
        /// <summary>ローカルブランチのツリーノードリスト</summary>
        public List<BranchTreeNode> Locals { get; } = [];
        /// <summary>リモートブランチのツリーノードリスト</summary>
        public List<BranchTreeNode> Remotes { get; } = [];

        /// <summary>
        /// コンストラクタ。ローカル・リモートそれぞれのソートモードを受け取る。
        /// </summary>
        /// <param name="localSortMode">ローカルブランチのソートモード</param>
        /// <param name="remoteSortMode">リモートブランチのソートモード</param>
        public Builder(Models.BranchSortMode localSortMode, Models.BranchSortMode remoteSortMode)
        {
            _localSortMode = localSortMode;
            _remoteSortMode = remoteSortMode;
        }

        /// <summary>
        /// 展開済みノードのパスリストを設定する。
        /// </summary>
        /// <param name="expanded">展開済みノードのパスリスト</param>
        public void SetExpandedNodes(List<string> expanded)
        {
            foreach (var node in expanded)
                _expanded.Add(node);
        }

        /// <summary>
        /// ブランチとリモートのリストからツリー構造を構築する。
        /// </summary>
        /// <param name="branches">ブランチリスト</param>
        /// <param name="remotes">リモートリスト</param>
        /// <param name="bForceExpanded">全ノードを強制展開するかどうか</param>
        public void Run(List<Models.Branch> branches, List<Models.Remote> remotes, bool bForceExpanded)
        {
            Dictionary<string, BranchTreeNode> folders = [];

            // リモートノードをルートレベルに追加する
            var fakeRemoteTime = (ulong)remotes.Count;
            foreach (var remote in remotes)
            {
                var path = $"refs/remotes/{remote.Name}";
                var node = new BranchTreeNode()
                {
                    Name = remote.Name,
                    Path = path,
                    Backend = remote,
                    IsExpanded = bForceExpanded || _expanded.Contains(path),
                    TimeToSort = fakeRemoteTime,
                };

                fakeRemoteTime--;
                folders.Add(path, node);
                Remotes.Add(node);
            }

            // 各ブランチをローカルまたはリモートのツリーに振り分ける
            foreach (var branch in branches)
            {
                if (branch.IsLocal)
                {
                    // ローカルブランチをローカルツリーに追加する
                    MakeBranchNode(branch, Locals, folders, "refs/heads", bForceExpanded);
                    continue;
                }

                // リモートブランチを該当リモートの子ツリーに追加する
                var rk = $"refs/remotes/{branch.Remote}";
                if (folders.TryGetValue(rk, out var remote))
                {
                    remote.Counter++;
                    MakeBranchNode(branch, remote.Children, folders, rk, bForceExpanded);
                }
            }

            folders.Clear();

            // ソートモードに応じてローカルブランチをソートする
            if (_localSortMode == Models.BranchSortMode.Name)
                SortNodesByName(Locals);
            else
                SortNodesByTime(Locals);

            // ソートモードに応じてリモートブランチをソートする
            if (_remoteSortMode == Models.BranchSortMode.Name)
                SortNodesByName(Remotes);
            else
                SortNodesByTime(Remotes);
        }

        /// <summary>
        /// ブランチをツリーノードとして構築する。
        /// パス区切り「/」でフォルダ階層を自動生成する。
        /// </summary>
        /// <param name="branch">対象のブランチ</param>
        /// <param name="roots">追加先のルートノードリスト</param>
        /// <param name="folders">フォルダパスとノードの対応辞書</param>
        /// <param name="prefix">パスプレフィックス</param>
        /// <param name="bForceExpanded">全ノードを強制展開するかどうか</param>
        private void MakeBranchNode(Models.Branch branch, List<BranchTreeNode> roots, Dictionary<string, BranchTreeNode> folders, string prefix, bool bForceExpanded)
        {
            var time = branch.CommitterDate;
            var fullpath = $"{prefix}/{branch.Name}";
            var sepIdx = branch.Name.IndexOf('/');
            if (sepIdx == -1 || branch.IsDetachedHead)
            {
                // パス区切りがない場合はリーフノードとして直接追加する
                roots.Add(new BranchTreeNode()
                {
                    Name = branch.Name,
                    Path = fullpath,
                    Backend = branch,
                    IsExpanded = false,
                    TimeToSort = time,
                });
                return;
            }

            // パス区切りに沿ってフォルダ階層を構築する
            BranchTreeNode lastFolder = null;
            var start = 0;

            while (sepIdx != -1)
            {
                var folder = string.Concat(prefix, "/", branch.Name[..sepIdx]);
                var name = branch.Name[start..sepIdx];
                if (folders.TryGetValue(folder, out var val))
                {
                    // 既存フォルダのカウンターとタイムスタンプを更新する
                    lastFolder = val;
                    lastFolder.Counter++;
                    lastFolder.TimeToSort = Math.Max(lastFolder.TimeToSort, time);
                    if (!lastFolder.IsExpanded)
                        lastFolder.IsExpanded |= (branch.IsCurrent || _expanded.Contains(folder));
                }
                else if (lastFolder is null)
                {
                    // ルートレベルに新しいフォルダノードを作成する
                    lastFolder = new BranchTreeNode()
                    {
                        Name = name,
                        Path = folder,
                        IsExpanded = bForceExpanded || branch.IsCurrent || _expanded.Contains(folder),
                        TimeToSort = time,
                        Counter = 1,
                    };
                    roots.Add(lastFolder);
                    folders.Add(folder, lastFolder);
                }
                else
                {
                    // 親フォルダの子として新しいフォルダノードを作成する
                    var cur = new BranchTreeNode()
                    {
                        Name = name,
                        Path = folder,
                        IsExpanded = bForceExpanded || branch.IsCurrent || _expanded.Contains(folder),
                        TimeToSort = time,
                        Counter = 1,
                    };
                    lastFolder.Children.Add(cur);
                    folders.Add(folder, cur);
                    lastFolder = cur;
                }

                start = sepIdx + 1;
                sepIdx = branch.Name.IndexOf('/', start);
            }

            // リーフノード（ブランチ本体）を最後のフォルダに追加する
            lastFolder?.Children.Add(new BranchTreeNode()
            {
                Name = System.IO.Path.GetFileName(branch.Name),
                Path = fullpath,
                Backend = branch,
                IsExpanded = false,
                TimeToSort = time,
            });
        }

        /// <summary>
        /// ノードリストを名前順で再帰的にソートする。
        /// DetachedHeadは常に先頭、フォルダはブランチより前に配置する。
        /// </summary>
        /// <param name="nodes">ソート対象のノードリスト</param>
        private static void SortNodesByName(List<BranchTreeNode> nodes)
        {
            nodes.Sort((l, r) =>
            {
                // DetachedHeadは常に先頭にする
                if (l.Backend is Models.Branch { IsDetachedHead: true })
                    return -1;

                // ブランチ同士は名前で比較し、フォルダはブランチより前にする
                if (l.Backend is Models.Branch)
                    return r.Backend is Models.Branch ? Models.NumericSort.Compare(l.Name, r.Name) : 1;

                return r.Backend is Models.Branch ? -1 : Models.NumericSort.Compare(l.Name, r.Name);
            });

            // 子ノードも再帰的にソートする
            foreach (var node in nodes)
                SortNodesByName(node.Children);
        }

        /// <summary>
        /// ノードリストをコミット日時順で再帰的にソートする。
        /// DetachedHeadは常に先頭、同じ日時の場合は名前で比較する。
        /// </summary>
        /// <param name="nodes">ソート対象のノードリスト</param>
        private static void SortNodesByTime(List<BranchTreeNode> nodes)
        {
            nodes.Sort((l, r) =>
            {
                // DetachedHeadは常に先頭にする
                if (l.Backend is Models.Branch { IsDetachedHead: true })
                    return -1;

                if (l.Backend is Models.Branch)
                {
                    if (r.Backend is Models.Branch)
                        return r.TimeToSort == l.TimeToSort ? Models.NumericSort.Compare(l.Name, r.Name) : r.TimeToSort.CompareTo(l.TimeToSort);
                    return 1;
                }

                if (r.Backend is Models.Branch)
                    return -1;

                // フォルダ同士は日時比較、同じなら名前で比較する
                if (r.TimeToSort == l.TimeToSort)
                    return Models.NumericSort.Compare(l.Name, r.Name);

                return r.TimeToSort.CompareTo(l.TimeToSort);
            });

            // 子ノードも再帰的にソートする
            foreach (var node in nodes)
                SortNodesByTime(node.Children);
        }

        /// <summary>
        /// 構築済みツリーから展開中のフォルダノードのパスを収集する。
        /// </summary>
        /// <param name="nodes">対象のノードリスト</param>
        /// <param name="result">収集先のリスト</param>
        public static void CollectExpandedPaths(List<BranchTreeNode> nodes, List<string> result)
        {
            foreach (var node in nodes)
            {
                if (node.IsBranch)
                    continue;

                if (node.IsExpanded)
                    result.Add(node.Path);

                CollectExpandedPaths(node.Children, result);
            }
        }

        /// <summary>ローカルブランチのソートモード</summary>
        private readonly Models.BranchSortMode _localSortMode;
        /// <summary>リモートブランチのソートモード</summary>
        private readonly Models.BranchSortMode _remoteSortMode;
        /// <summary>展開済みノードのパスセット</summary>
        private readonly HashSet<string> _expanded = [];
    }
}
