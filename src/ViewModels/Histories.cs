using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     コミット履歴の表示・操作を管理するViewModel。
    ///     コミットグラフ、bisect、コミット選択、ナビゲーションなどを提供する。
    /// </summary>
    public class Histories : ObservableObject, IDisposable
    {
        /// <summary>
        ///     履歴データの読み込み中かどうか。
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        ///     履歴グリッドで作者列を表示するかどうか。
        /// </summary>
        public bool IsAuthorColumnVisible
        {
            get => _repo.UIStates.IsAuthorColumnVisibleInHistory;
            set
            {
                if (_repo.UIStates.IsAuthorColumnVisibleInHistory != value)
                {
                    _repo.UIStates.IsAuthorColumnVisibleInHistory = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        ///     履歴グリッドでSHA列を表示するかどうか。
        /// </summary>
        public bool IsSHAColumnVisible
        {
            get => _repo.UIStates.IsSHAColumnVisibleInHistory;
            set
            {
                if (_repo.UIStates.IsSHAColumnVisibleInHistory != value)
                {
                    _repo.UIStates.IsSHAColumnVisibleInHistory = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        ///     履歴グリッドで日時列を表示するかどうか。
        /// </summary>
        public bool IsDateTimeColumnVisible
        {
            get => _repo.UIStates.IsDateTimeColumnVisibleInHistory;
            set
            {
                if (_repo.UIStates.IsDateTimeColumnVisibleInHistory != value)
                {
                    _repo.UIStates.IsDateTimeColumnVisibleInHistory = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        ///     表示対象のコミット一覧。変更時に選択状態を復元する。
        /// </summary>
        public List<Models.Commit> Commits
        {
            get => _commits;
            set
            {
                var lastSelected = SelectedCommit;
                if (SetProperty(ref _commits, value))
                {
                    if (value.Count > 0 && lastSelected != null)
                        SelectedCommit = value.Find(x => x.SHA == lastSelected.SHA);
                }
            }
        }

        /// <summary>
        ///     コミットグラフの描画データ。
        /// </summary>
        public Models.CommitGraph Graph
        {
            get => _graph;
            set => SetProperty(ref _graph, value);
        }

        /// <summary>
        ///     現在選択中のコミット。
        /// </summary>
        public Models.Commit SelectedCommit
        {
            get => _selectedCommit;
            set => SetProperty(ref _selectedCommit, value);
        }

        /// <summary>
        ///     ナビゲーション操作のID。スクロール位置の同期に使用。
        /// </summary>
        public long NavigationId
        {
            get => _navigationId;
            private set => SetProperty(ref _navigationId, value);
        }

        /// <summary>
        ///     詳細パネルに表示するコンテキスト（コミット詳細やリビジョン比較など）。
        /// </summary>
        public IDisposable DetailContext
        {
            get => _detailContext;
            set => SetProperty(ref _detailContext, value);
        }

        /// <summary>
        ///     bisect操作の状態情報。bisect中でなければnull。
        /// </summary>
        public Models.Bisect Bisect
        {
            get => _bisect;
            private set => SetProperty(ref _bisect, value);
        }

        public GridLength LeftArea
        {
            get => _leftArea;
            set => SetProperty(ref _leftArea, value);
        }

        public GridLength RightArea
        {
            get => _rightArea;
            set => SetProperty(ref _rightArea, value);
        }

        public GridLength TopArea
        {
            get => _topArea;
            set => SetProperty(ref _topArea, value);
        }

        public GridLength BottomArea
        {
            get => _bottomArea;
            set => SetProperty(ref _bottomArea, value);
        }

        /// <summary>
        ///     コンストラクタ。リポジトリを指定して履歴VMを初期化する。
        /// </summary>
        public Histories(Repository repo)
        {
            _repo = repo;
            _commitDetailSharedData = new CommitDetailSharedData();
        }

        /// <summary>
        ///     リソースを解放する。コミットリストやグラフデータをクリアする。
        /// </summary>
        public void Dispose()
        {
            Commits = [];
            _repo = null;
            _graph = null;
            _selectedCommit = null;
            _detailContext?.Dispose();
            _detailContext = null;
        }

        /// <summary>
        ///     bisect操作の状態を更新し、現在のbisect状態を返す。
        ///     BISECT_STARTファイルの存在とrefs/bisect内の情報を確認する。
        /// </summary>
        public Models.BisectState UpdateBisectInfo()
        {
            var test = Path.Combine(_repo.GitDir, "BISECT_START");
            if (!File.Exists(test))
            {
                Bisect = null;
                return Models.BisectState.None;
            }

            var info = new Models.Bisect();
            var dir = Path.Combine(_repo.GitDir, "refs", "bisect");
            if (Directory.Exists(dir))
            {
                var files = new DirectoryInfo(dir).GetFiles();
                foreach (var file in files)
                {
                    if (file.Name.StartsWith("bad"))
                        info.Bads.Add(File.ReadAllText(file.FullName).Trim());
                    else if (file.Name.StartsWith("good"))
                        info.Goods.Add(File.ReadAllText(file.FullName).Trim());
                }
            }

            Bisect = info;

            if (info.Bads.Count == 0 || info.Goods.Count == 0)
                return Models.BisectState.WaitingForRange;
            else
                return Models.BisectState.Detecting;
        }

        /// <summary>
        ///     指定されたSHAのコミットへナビゲーションする。
        ///     表示中のコミットリストに存在しない場合は非同期で取得する。
        /// </summary>
        public void NavigateTo(string commitSHA)
        {
            var commit = _commits.Find(x => x.SHA.StartsWith(commitSHA, StringComparison.Ordinal));
            if (commit != null)
            {
                SelectedCommit = commit;
                NavigationId = _navigationId + 1;
                return;
            }

            Task.Run(async () =>
            {
                var c = await new Commands.QuerySingleCommit(_repo.FullPath, commitSHA)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    _ignoreSelectionChange = true;
                    SelectedCommit = null;

                    if (_detailContext is CommitDetail detail)
                    {
                        detail.Commit = c;
                    }
                    else
                    {
                        var commitDetail = new CommitDetail(_repo, _commitDetailSharedData);
                        commitDetail.Commit = c;
                        DetailContext = commitDetail;
                    }

                    _ignoreSelectionChange = false;
                });
            });
        }

        /// <summary>
        ///     コミットの選択状態を処理する。
        ///     0件で詳細クリア、1件でコミット詳細表示、2件でリビジョン比較、3件以上でカウント表示。
        /// </summary>
        public void Select(IList commits)
        {
            if (_ignoreSelectionChange)
                return;

            if (commits.Count == 0)
            {
                _repo.SearchCommitContext.Selected = null;
                DetailContext = null;
            }
            else if (commits.Count == 1)
            {
                var commit = (commits[0] as Models.Commit)!;
                if (_repo.SearchCommitContext.Selected == null || _repo.SearchCommitContext.Selected.SHA != commit.SHA)
                    _repo.SearchCommitContext.Selected = _repo.SearchCommitContext.Results?.Find(x => x.SHA == commit.SHA);

                SelectedCommit = commit;
                NavigationId = _navigationId + 1;

                if (_detailContext is CommitDetail detail)
                {
                    detail.Commit = commit;
                }
                else
                {
                    var commitDetail = new CommitDetail(_repo, _commitDetailSharedData);
                    commitDetail.Commit = commit;
                    DetailContext = commitDetail;
                }
            }
            else if (commits.Count == 2)
            {
                _repo.SearchCommitContext.Selected = null;

                var end = commits[0] as Models.Commit;
                var start = commits[1] as Models.Commit;
                DetailContext = new RevisionCompare(_repo, start, end);
            }
            else
            {
                _repo.SearchCommitContext.Selected = null;
                DetailContext = new Models.Count(commits.Count);
            }
        }

        public async Task<Models.Commit> GetCommitAsync(string sha)
        {
            return await new Commands.QuerySingleCommit(_repo.FullPath, sha)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     デコレータ（ブランチ参照）をクリックしてブランチをチェックアウトする。
        ///     ローカル/リモートブランチの状態に応じて適切な操作を実行する。
        /// </summary>
        public async Task<bool> CheckoutBranchByDecoratorAsync(Models.Decorator decorator)
        {
            if (decorator == null)
                return false;

            if (decorator.Type == Models.DecoratorType.CurrentBranchHead ||
                decorator.Type == Models.DecoratorType.CurrentCommitHead)
                return true;

            if (decorator.Type == Models.DecoratorType.LocalBranchHead)
            {
                var b = _repo.Branches.Find(x => x.Name == decorator.Name);
                if (b == null)
                    return false;

                await _repo.CheckoutBranchAsync(b);
                return true;
            }

            if (decorator.Type == Models.DecoratorType.RemoteBranchHead)
            {
                var rb = _repo.Branches.Find(x => x.FriendlyName == decorator.Name);
                if (rb == null)
                    return false;

                var lb = _repo.Branches.Find(x => x.IsLocal && x.Upstream == rb.FullName);
                if (lb == null || lb.Ahead.Count > 0)
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new CreateBranch(_repo, rb));
                }
                else if (lb.Behind.Count > 0)
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new CheckoutAndFastForward(_repo, lb, rb));
                }
                else if (!lb.IsCurrent)
                {
                    await _repo.CheckoutBranchAsync(lb);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        ///     コミットに関連するブランチをチェックアウトする。
        ///     ローカルブランチ優先、なければリモートブランチから新規作成を提案する。
        /// </summary>
        public async Task CheckoutBranchByCommitAsync(Models.Commit commit)
        {
            if (commit.IsCurrentHead)
                return;

            Models.Branch firstRemoteBranch = null;
            foreach (var d in commit.Decorators)
            {
                if (d.Type == Models.DecoratorType.LocalBranchHead)
                {
                    var b = _repo.Branches.Find(x => x.Name == d.Name);
                    if (b == null)
                        continue;

                    await _repo.CheckoutBranchAsync(b);
                    return;
                }

                if (d.Type == Models.DecoratorType.RemoteBranchHead)
                {
                    var rb = _repo.Branches.Find(x => x.FriendlyName == d.Name);
                    if (rb == null)
                        continue;

                    var lb = _repo.Branches.Find(x => x.IsLocal && x.Upstream == rb.FullName);
                    if (lb != null && lb.Behind.Count > 0 && lb.Ahead.Count == 0)
                    {
                        if (_repo.CanCreatePopup())
                            _repo.ShowPopup(new CheckoutAndFastForward(_repo, lb, rb));
                        return;
                    }

                    firstRemoteBranch ??= rb;
                }
            }

            if (_repo.CanCreatePopup())
            {
                if (firstRemoteBranch != null)
                    _repo.ShowPopup(new CreateBranch(_repo, firstRemoteBranch));
                else if (!_repo.IsBare)
                    _repo.ShowPopup(new CheckoutCommit(_repo, commit));
            }
        }

        /// <summary>
        ///     指定コミットをチェリーピックする。マージコミットの場合は親の選択が必要。
        /// </summary>
        public async Task CherryPickAsync(Models.Commit commit)
        {
            if (_repo.CanCreatePopup())
            {
                if (commit.Parents.Count <= 1)
                {
                    _repo.ShowPopup(new CherryPick(_repo, [commit]));
                }
                else
                {
                    var parents = new List<Models.Commit>();
                    foreach (var sha in commit.Parents)
                    {
                        var parent = _commits.Find(x => x.SHA == sha);
                        if (parent == null)
                            parent = await new Commands.QuerySingleCommit(_repo.FullPath, sha).GetResultAsync();

                        if (parent != null)
                            parents.Add(parent);
                    }

                    _repo.ShowPopup(new CherryPick(_repo, commit, parents));
                }
            }
        }

        /// <summary>
        ///     HEADコミットのメッセージを書き換える（reword）。
        /// </summary>
        public async Task RewordHeadAsync(Models.Commit head)
        {
            if (_repo.CanCreatePopup())
            {
                var message = await new Commands.QueryCommitFullMessage(_repo.FullPath, head.SHA).GetResultAsync();
                _repo.ShowPopup(new Reword(_repo, head, message));
            }
        }

        /// <summary>
        ///     HEADコミットを親コミットにスカッシュまたはフィックスアップする。
        /// </summary>
        public async Task SquashOrFixupHeadAsync(Models.Commit head, bool fixup)
        {
            if (head.Parents.Count == 1)
            {
                // 独立した複数のgitコマンドを並列実行（旧: 最大3回の逐次git呼び出し）
                var parentTask = new Commands.QuerySingleCommit(_repo.FullPath, head.Parents[0]).GetResultAsync();
                var parentMsgTask = new Commands.QueryCommitFullMessage(_repo.FullPath, head.Parents[0]).GetResultAsync();
                var headMsgTask = fixup
                    ? Task.FromResult<string>(null)
                    : new Commands.QueryCommitFullMessage(_repo.FullPath, head.SHA).GetResultAsync();
                await Task.WhenAll(parentTask, parentMsgTask, headMsgTask).ConfigureAwait(false);

                var parent = parentTask.Result;
                if (parent == null)
                    return;

                var message = fixup ? parentMsgTask.Result : $"{parentMsgTask.Result}\n\n{headMsgTask.Result}";

                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new SquashOrFixupHead(_repo, parent, message, fixup));
            }
        }

        /// <summary>
        ///     HEADコミットを削除（drop）する。親コミットへのリセットを行う。
        /// </summary>
        public async Task DropHeadAsync(Models.Commit head)
        {
            if (head.Parents.Count == 0)
                return;

            var parent = _commits.Find(x => x.SHA.Equals(head.Parents[0]));
            if (parent == null)
                parent = await new Commands.QuerySingleCommit(_repo.FullPath, head.Parents[0]).GetResultAsync();

            if (parent != null && _repo.CanCreatePopup())
                _repo.ShowPopup(new DropHead(_repo, head, parent));
        }

        /// <summary>
        ///     対話的リベース（interactive rebase）を開始する。指定コミットとアクションで事前設定する。
        /// </summary>
        public async Task InteractiveRebaseAsync(Models.Commit commit, Models.InteractiveRebaseAction act)
        {
            var prefill = new InteractiveRebasePrefill(commit.SHA, act);
            var start = act switch
            {
                Models.InteractiveRebaseAction.Squash or Models.InteractiveRebaseAction.Fixup => $"{commit.SHA}~~",
                _ => $"{commit.SHA}~",
            };

            var on = await new Commands.QuerySingleCommit(_repo.FullPath, start).GetResultAsync();
            if (on == null)
                App.RaiseException(_repo.FullPath, App.Text("Error.CanNotSquash"));
            else
                await App.ShowDialog(new InteractiveRebase(_repo, on, prefill));
        }

        /// <summary>
        ///     コミットの完全なメッセージを非同期で取得する。
        /// </summary>
        public async Task<string> GetCommitFullMessageAsync(Models.Commit commit)
        {
            return await new Commands.QueryCommitFullMessage(_repo.FullPath, commit.SHA)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     指定コミットをHEADと比較する。HEADが見つからない場合は非同期で取得する。
        /// </summary>
        public async Task<Models.Commit> CompareWithHeadAsync(Models.Commit commit)
        {
            var head = _commits.Find(x => x.IsCurrentHead);
            if (head == null)
            {
                _repo.SearchCommitContext.Selected = null;
                head = await new Commands.QuerySingleCommit(_repo.FullPath, "HEAD").GetResultAsync();
                if (head != null)
                    DetailContext = new RevisionCompare(_repo, commit, head);

                return null;
            }

            return head;
        }

        /// <summary>
        ///     指定コミットをワークツリー（作業ディレクトリ）と比較する。
        /// </summary>
        public void CompareWithWorktree(Models.Commit commit)
        {
            DetailContext = new RevisionCompare(_repo, commit, null);
        }

        private Repository _repo = null;
        private CommitDetailSharedData _commitDetailSharedData = null;
        private bool _isLoading = true;
        private List<Models.Commit> _commits = new List<Models.Commit>();
        private Models.CommitGraph _graph = null;
        private Models.Commit _selectedCommit = null;
        private Models.Bisect _bisect = null;
        private long _navigationId = 0;
        private IDisposable _detailContext = null;
        private bool _ignoreSelectionChange = false;

        private GridLength _leftArea = new GridLength(1, GridUnitType.Star);
        private GridLength _rightArea = new GridLength(1, GridUnitType.Star);
        private GridLength _topArea = new GridLength(1, GridUnitType.Star);
        private GridLength _bottomArea = new GridLength(1, GridUnitType.Star);
    }
}
