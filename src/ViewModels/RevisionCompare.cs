using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     2つのリビジョン（コミット）間の差分比較を管理するViewModel。
    ///     変更ファイルの一覧表示、検索フィルタリング、差分表示、ファイルリセット操作を提供する。
    /// </summary>
    public class RevisionCompare : ObservableObject, IDisposable
    {
        /// <summary>
        ///     比較データの読み込み中かどうか。
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        ///     比較の開始ポイント（左側のリビジョン）。コミットまたはワークツリー。
        /// </summary>
        public object StartPoint
        {
            get => _startPoint;
            private set => SetProperty(ref _startPoint, value);
        }

        /// <summary>
        ///     比較の終了ポイント（右側のリビジョン）。コミットまたはワークツリー。
        /// </summary>
        public object EndPoint
        {
            get => _endPoint;
            private set => SetProperty(ref _endPoint, value);
        }

        /// <summary>
        ///     左側リビジョンの表示用説明文。
        /// </summary>
        public string LeftSideDesc
        {
            get => GetDesc(StartPoint);
        }

        /// <summary>
        ///     右側リビジョンの表示用説明文。
        /// </summary>
        public string RightSideDesc
        {
            get => GetDesc(EndPoint);
        }

        /// <summary>
        ///     左側リビジョンへのリセットが可能かどうか。ベアリポジトリでは不可。
        /// </summary>
        public bool CanResetToLeft
        {
            get => !_repo.IsBare && _startPoint != null;
        }

        /// <summary>
        ///     右側リビジョンへのリセットが可能かどうか。
        /// </summary>
        public bool CanResetToRight
        {
            get => !_repo.IsBare && _endPoint != null;
        }

        /// <summary>
        ///     パッチとして保存可能かどうか。両方のポイントが設定されている必要がある。
        /// </summary>
        public bool CanSaveAsPatch
        {
            get => _startPoint != null && _endPoint != null;
        }

        /// <summary>
        ///     変更ファイルの総数。
        /// </summary>
        public int TotalChanges
        {
            get => _totalChanges;
            private set => SetProperty(ref _totalChanges, value);
        }

        /// <summary>
        ///     フィルタ適用後の表示対象変更ファイルリスト。
        /// </summary>
        public List<Models.Change> VisibleChanges
        {
            get => _visibleChanges;
            private set => SetProperty(ref _visibleChanges, value);
        }

        /// <summary>
        ///     現在選択されている変更ファイルリスト。1件選択時に差分コンテキストを更新する。
        /// </summary>
        public List<Models.Change> SelectedChanges
        {
            get => _selectedChanges;
            set
            {
                if (SetProperty(ref _selectedChanges, value))
                {
                    if (value is { Count: 1 })
                    {
                        var option = new Models.DiffOption(GetSHA(_startPoint), GetSHA(_endPoint), value[0]);
                        DiffContext = new DiffContext(_repo.FullPath, option, _diffContext);
                    }
                    else
                    {
                        DiffContext = null;
                    }
                }
            }
        }

        /// <summary>
        ///     変更ファイルの検索フィルタ文字列。パスの部分一致で絞り込む。
        /// </summary>
        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    RefreshVisible();
            }
        }

        /// <summary>
        ///     選択されたファイルの差分表示コンテキスト。
        /// </summary>
        public DiffContext DiffContext
        {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        /// <summary>
        ///     コンストラクタ。リポジトリと比較する2つのコミットを指定して差分比較を初期化する。
        /// </summary>
        /// <param name="repo">対象リポジトリ</param>
        /// <param name="startPoint">開始コミット（nullの場合はワークツリー）</param>
        /// <param name="endPoint">終了コミット（nullの場合はワークツリー）</param>
        public RevisionCompare(Repository repo, Models.Commit startPoint, Models.Commit endPoint)
        {
            _repo = repo;
            _startPoint = (object)startPoint ?? new Models.Null();
            _endPoint = (object)endPoint ?? new Models.Null();
            Refresh();
        }

        /// <summary>
        ///     リソースを解放する。全てのリストとコンテキストをクリアする。
        /// </summary>
        public void Dispose()
        {
            _repo = null;
            _startPoint = null;
            _endPoint = null;
            _changes?.Clear();
            _visibleChanges?.Clear();
            _selectedChanges?.Clear();
            _searchFilter = null;
            _diffContext = null;
        }

        /// <summary>
        ///     外部差分ツールで変更ファイルを開く。
        /// </summary>
        public void OpenChangeWithExternalDiffTool(Models.Change change)
        {
            var opt = new Models.DiffOption(GetSHA(_startPoint), GetSHA(_endPoint), change);
            new Commands.DiffTool(_repo.FullPath, opt).Open();
        }

        /// <summary>
        ///     指定されたコミットSHAに履歴画面をナビゲートする。
        /// </summary>
        public void NavigateTo(string commitSHA)
        {
            _repo?.NavigateToCommit(commitSHA);
        }

        /// <summary>
        ///     比較の開始ポイントと終了ポイントを入れ替えて差分を再計算する。
        /// </summary>
        public void Swap()
        {
            (StartPoint, EndPoint) = (_endPoint, _startPoint);
            VisibleChanges = [];
            SelectedChanges = [];
            IsLoading = true;
            Refresh();
        }

        /// <summary>
        ///     相対パスをリポジトリルートからの絶対パスに変換する。
        /// </summary>
        public string GetAbsPath(string path)
        {
            return Native.OS.GetAbsPath(_repo.FullPath, path);
        }

        /// <summary>
        ///     指定ファイルを左側リビジョンの状態にリセットする。
        ///     追加されたファイルは削除、名前変更は元に戻す、それ以外はチェックアウトする。
        /// </summary>
        public async Task ResetToLeftAsync(Models.Change change)
        {
            var sha = GetSHA(_startPoint);
            var log = _repo.CreateLog($"Reset File to '{GetDesc(_startPoint)}'");

            if (change.Index == Models.ChangeState.Added)
            {
                var fullpath = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
                if (File.Exists(fullpath))
                    await new Commands.Remove(_repo.FullPath, [change.Path])
                        .Use(log)
                        .ExecAsync();
            }
            else if (change.Index == Models.ChangeState.Renamed)
            {
                var renamed = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
                if (File.Exists(renamed))
                    await new Commands.Remove(_repo.FullPath, [change.Path])
                        .Use(log)
                        .ExecAsync();

                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.OriginalPath, sha);
            }
            else
            {
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.Path, sha);
            }

            log.Complete();
        }

        /// <summary>
        ///     指定ファイルを右側リビジョンの状態にリセットする。
        /// </summary>
        public async Task ResetToRightAsync(Models.Change change)
        {
            var sha = GetSHA(_endPoint);
            var log = _repo.CreateLog($"Reset File to '{GetDesc(_endPoint)}'");

            if (change.Index == Models.ChangeState.Deleted)
            {
                var fullpath = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
                if (File.Exists(fullpath))
                    await new Commands.Remove(_repo.FullPath, [change.Path])
                        .Use(log)
                        .ExecAsync();
            }
            else if (change.Index == Models.ChangeState.Renamed)
            {
                var old = Native.OS.GetAbsPath(_repo.FullPath, change.OriginalPath);
                if (File.Exists(old))
                    await new Commands.Remove(_repo.FullPath, [change.OriginalPath])
                        .Use(log)
                        .ExecAsync();

                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.Path, sha);
            }
            else
            {
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.Path, sha);
            }

            log.Complete();
        }

        /// <summary>
        ///     複数ファイルを一括で左側リビジョンの状態にリセットする。
        /// </summary>
        public async Task ResetMultipleToLeftAsync(List<Models.Change> changes)
        {
            var sha = GetSHA(_startPoint);
            var checkouts = new List<string>();
            var removes = new List<string>();

            foreach (var c in changes)
            {
                if (c.Index == Models.ChangeState.Added)
                {
                    var fullpath = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
                    if (File.Exists(fullpath))
                        removes.Add(c.Path);
                }
                else if (c.Index == Models.ChangeState.Renamed)
                {
                    var old = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
                    if (File.Exists(old))
                        removes.Add(c.Path);

                    checkouts.Add(c.OriginalPath);
                }
                else
                {
                    checkouts.Add(c.Path);
                }
            }

            var log = _repo.CreateLog($"Reset Files to '{GetDesc(_startPoint)}'");

            if (removes.Count > 0)
                await new Commands.Remove(_repo.FullPath, removes)
                    .Use(log)
                    .ExecAsync();

            if (checkouts.Count > 0)
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .MultipleFilesWithRevisionAsync(checkouts, sha);

            log.Complete();
        }

        /// <summary>
        ///     複数ファイルを一括で右側リビジョンの状態にリセットする。
        /// </summary>
        public async Task ResetMultipleToRightAsync(List<Models.Change> changes)
        {
            var sha = GetSHA(_endPoint);
            var checkouts = new List<string>();
            var removes = new List<string>();

            foreach (var c in changes)
            {
                if (c.Index == Models.ChangeState.Deleted)
                {
                    var fullpath = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
                    if (File.Exists(fullpath))
                        removes.Add(c.Path);
                }
                else if (c.Index == Models.ChangeState.Renamed)
                {
                    var renamed = Native.OS.GetAbsPath(_repo.FullPath, c.OriginalPath);
                    if (File.Exists(renamed))
                        removes.Add(c.OriginalPath);

                    checkouts.Add(c.Path);
                }
                else
                {
                    checkouts.Add(c.Path);
                }
            }

            var log = _repo.CreateLog($"Reset Files to '{GetDesc(_endPoint)}'");

            if (removes.Count > 0)
                await new Commands.Remove(_repo.FullPath, removes)
                    .Use(log)
                    .ExecAsync();

            if (checkouts.Count > 0)
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .MultipleFilesWithRevisionAsync(checkouts, sha);

            log.Complete();
        }

        /// <summary>
        ///     変更内容をパッチファイルとして保存する。
        /// </summary>
        public async Task SaveChangesAsPatchAsync(List<Models.Change> changes, string saveTo)
        {
            var succ = await Commands.SaveChangesAsPatch.ProcessRevisionCompareChangesAsync(_repo.FullPath, changes ?? _changes, GetSHA(_startPoint), GetSHA(_endPoint), saveTo);
            if (succ)
                App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
        }

        /// <summary>
        ///     検索フィルタをクリアして全ファイルを表示する。
        /// </summary>
        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        /// <summary>
        ///     検索フィルタに基づいて表示対象の変更リストを更新する。
        /// </summary>
        private void RefreshVisible()
        {
            if (_changes == null)
                return;

            if (string.IsNullOrEmpty(_searchFilter))
            {
                VisibleChanges = _changes;
            }
            else
            {
                var visible = new List<Models.Change>();
                foreach (var c in _changes)
                {
                    if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(c);
                }

                VisibleChanges = visible;
            }
        }

        /// <summary>
        ///     バックグラウンドでリビジョン間の差分を取得し、UIを更新する。
        /// </summary>
        private void Refresh()
        {
            Task.Run(async () =>
            {
                _changes = await new Commands.CompareRevisions(_repo.FullPath, GetSHA(_startPoint), GetSHA(_endPoint))
                    .ReadAsync()
                    .ConfigureAwait(false);

                var visible = _changes;
                if (!string.IsNullOrWhiteSpace(_searchFilter))
                {
                    visible = [];
                    foreach (var c in _changes)
                    {
                        if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                            visible.Add(c);
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    TotalChanges = _changes.Count;
                    VisibleChanges = visible;
                    IsLoading = false;

                    if (VisibleChanges.Count > 0)
                        SelectedChanges = [VisibleChanges[0]];
                    else
                        SelectedChanges = [];
                });
            });
        }

        /// <summary>
        ///     オブジェクトからコミットSHAを取得する。コミットでない場合は空文字列を返す。
        /// </summary>
        private string GetSHA(object obj)
        {
            return obj is Models.Commit commit ? commit.SHA : string.Empty;
        }

        /// <summary>
        ///     オブジェクトから表示用の説明文を取得する。コミットでない場合は「ワークツリー」を返す。
        /// </summary>
        private string GetDesc(object obj)
        {
            return obj is Models.Commit commit ? commit.GetFriendlyName() : App.Text("Worktree");
        }

        private Repository _repo;
        private bool _isLoading = true;
        private object _startPoint = null;
        private object _endPoint = null;
        private int _totalChanges = 0;
        private List<Models.Change> _changes = null;
        private List<Models.Change> _visibleChanges = null;
        private List<Models.Change> _selectedChanges = null;
        private string _searchFilter = string.Empty;
        private DiffContext _diffContext = null;
    }
}
