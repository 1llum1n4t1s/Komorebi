using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     コミット詳細の共有データ。
    ///     複数のCommitDetailインスタンス間でアクティブタブのインデックスを共有する。
    /// </summary>
    public class CommitDetailSharedData
    {
        /// <summary>
        ///     アクティブなタブのインデックス（0: 情報タブ、1: 変更タブ）。
        /// </summary>
        public int ActiveTabIndex
        {
            get;
            set;
        }

        /// <summary>
        ///     コンストラクタ。設定に基づいてデフォルトのタブインデックスを設定する。
        /// </summary>
        public CommitDetailSharedData()
        {
            // デフォルトで変更タブを表示する設定の場合は1、それ以外は0
            ActiveTabIndex = Preferences.Instance.ShowChangesInCommitDetailByDefault ? 1 : 0;
        }
    }

    /// <summary>
    ///     コミット詳細ビューのViewModel。
    ///     選択されたコミットの詳細情報（メッセージ、署名、変更ファイル、差分、
    ///     リビジョンファイルの表示）を管理する。ファイルリセット操作も提供する。
    /// </summary>
    public partial class CommitDetail : ObservableObject, IDisposable
    {
        /// <summary>
        ///     対象のリポジトリViewModel。
        /// </summary>
        public Repository Repository
        {
            get => _repo;
        }

        /// <summary>
        ///     アクティブなタブのインデックス。変更タブに切り替え時、差分コンテキストを生成する。
        /// </summary>
        public int ActiveTabIndex
        {
            get => _sharedData.ActiveTabIndex;
            set
            {
                if (value != _sharedData.ActiveTabIndex)
                {
                    _sharedData.ActiveTabIndex = value;

                    // 変更タブに切り替え時、選択が1件で差分コンテキストがない場合は生成する
                    if (value == 1 && DiffContext == null && _selectedChanges is { Count: 1 })
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption(_commit, _selectedChanges[0]));
                }
            }
        }

        /// <summary>
        ///     表示対象のコミット。変更時に全データを再読み込みする。
        /// </summary>
        public Models.Commit Commit
        {
            get => _commit;
            set
            {
                // 同じSHAのコミットが再設定された場合は無視する
                if (_commit != null && value != null && _commit.SHA.Equals(value.SHA, StringComparison.Ordinal))
                    return;

                if (SetProperty(ref _commit, value))
                    Refresh();
            }
        }

        /// <summary>
        ///     コミットのフルメッセージ（インライン要素付き）。
        /// </summary>
        public Models.CommitFullMessage FullMessage
        {
            get => _fullMessage;
            private set => SetProperty(ref _fullMessage, value);
        }

        /// <summary>
        ///     コミットの署名情報（GPG/SSH署名）。
        /// </summary>
        public Models.CommitSignInfo SignInfo
        {
            get => _signInfo;
            private set => SetProperty(ref _signInfo, value);
        }

        /// <summary>
        ///     コミットに関連するWebリンク一覧（イシュートラッカー等）。
        /// </summary>
        public List<Models.CommitLink> WebLinks
        {
            get;
            private set;
        }

        /// <summary>
        ///     このコミットの子コミットSHAリスト。
        /// </summary>
        public List<string> Children
        {
            get => _children;
            private set => SetProperty(ref _children, value);
        }

        /// <summary>
        ///     コミットの全変更ファイルリスト。
        /// </summary>
        public List<Models.Change> Changes
        {
            get => _changes;
            set => SetProperty(ref _changes, value);
        }

        /// <summary>
        ///     フィルタ適用後の表示中変更ファイルリスト。
        /// </summary>
        public List<Models.Change> VisibleChanges
        {
            get => _visibleChanges;
            set => SetProperty(ref _visibleChanges, value);
        }

        /// <summary>
        ///     選択中の変更ファイルリスト。変更時にDiffContextを更新する。
        /// </summary>
        public List<Models.Change> SelectedChanges
        {
            get => _selectedChanges;
            set
            {
                if (SetProperty(ref _selectedChanges, value))
                {
                    // 変更タブ以外または選択が1件でない場合は差分コンテキストをクリアする
                    if (ActiveTabIndex != 1 || value is not { Count: 1 })
                        DiffContext = null;
                    else
                        DiffContext = new DiffContext(_repo.FullPath, new Models.DiffOption(_commit, value[0]), _diffContext);
                }
            }
        }

        /// <summary>
        ///     差分表示用のDiffContext。
        /// </summary>
        public DiffContext DiffContext
        {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        /// <summary>
        ///     変更ファイルの検索フィルタ文字列。変更時に表示リストを更新する。
        /// </summary>
        public string SearchChangeFilter
        {
            get => _searchChangeFilter;
            set
            {
                if (SetProperty(ref _searchChangeFilter, value))
                    // フィルタ変更時に表示リストを再構築する
                    RefreshVisibleChanges();
            }
        }

        /// <summary>
        ///     リビジョンファイルビューアで表示中のファイルパス。
        /// </summary>
        public string ViewRevisionFilePath
        {
            get => _viewRevisionFilePath;
            private set => SetProperty(ref _viewRevisionFilePath, value);
        }

        /// <summary>
        ///     リビジョンファイルビューアで表示中のファイル内容。
        ///     テキスト、画像、バイナリ、LFS、サブモジュールなど複数の型を取り得る。
        /// </summary>
        public object ViewRevisionFileContent
        {
            get => _viewRevisionFileContent;
            private set => SetProperty(ref _viewRevisionFileContent, value);
        }

        /// <summary>
        ///     リビジョンファイル検索のフィルタ文字列。変更時にサジェストを更新する。
        /// </summary>
        public string RevisionFileSearchFilter
        {
            get => _revisionFileSearchFilter;
            set
            {
                if (SetProperty(ref _revisionFileSearchFilter, value))
                    // フィルタ変更時にサジェストリストを更新する
                    RefreshRevisionSearchSuggestion();
            }
        }

        /// <summary>
        ///     リビジョンファイル検索のサジェストリスト。
        /// </summary>
        public List<string> RevisionFileSearchSuggestion
        {
            get => _revisionFileSearchSuggestion;
            private set => SetProperty(ref _revisionFileSearchSuggestion, value);
        }

        /// <summary>
        ///     リビジョンファイルをデフォルトエディタで開けるかどうか。
        /// </summary>
        public bool CanOpenRevisionFileWithDefaultEditor
        {
            get => _canOpenRevisionFileWithDefaultEditor;
            private set => SetProperty(ref _canOpenRevisionFileWithDefaultEditor, value);
        }

        /// <summary>
        ///     スクロール位置のオフセット。
        /// </summary>
        public Vector ScrollOffset
        {
            get => _scrollOffset;
            set => SetProperty(ref _scrollOffset, value);
        }

        /// <summary>
        ///     コンストラクタ。リポジトリと共有データを受け取って初期化する。
        /// </summary>
        /// <param name="repo">対象のリポジトリViewModel</param>
        /// <param name="sharedData">タブインデックス共有データ（nullの場合は新規作成）</param>
        public CommitDetail(Repository repo, CommitDetailSharedData sharedData)
        {
            _repo = repo;
            _sharedData = sharedData ?? new CommitDetailSharedData();
            // リモート情報からWebリンクを生成する
            WebLinks = Models.CommitLink.Get(repo.Remotes);
        }

        /// <summary>
        ///     リソースを解放する。全フィールドをnullに設定してGCを促進する。
        /// </summary>
        public void Dispose()
        {
            _repo = null;
            _commit = null;
            _changes = null;
            _visibleChanges = null;
            _selectedChanges = null;
            _signInfo = null;
            _searchChangeFilter = null;
            _diffContext = null;
            _viewRevisionFileContent = null;
            _cancellationSource = null;
            _requestingRevisionFiles = false;
            _revisionFiles = null;
            _revisionFileSearchSuggestion = null;
        }

        /// <summary>
        ///     指定コミットSHAにナビゲートする。
        /// </summary>
        /// <param name="commitSHA">ナビゲート先のコミットSHA</param>
        public void NavigateTo(string commitSHA)
        {
            _repo?.NavigateToCommit(commitSHA);
        }

        /// <summary>
        ///     このコミットを含む全参照（ブランチ・タグ）を非同期で取得する。
        /// </summary>
        /// <returns>参照デコレータのリスト</returns>
        public async Task<List<Models.Decorator>> GetRefsContainsThisCommitAsync()
        {
            return await new Commands.QueryRefsContainsCommit(_repo.FullPath, _commit.SHA)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     変更ファイル検索フィルタをクリアする。
        /// </summary>
        public void ClearSearchChangeFilter()
        {
            SearchChangeFilter = string.Empty;
        }

        /// <summary>
        ///     リビジョンファイル検索フィルタをクリアする。
        /// </summary>
        public void ClearRevisionFileSearchFilter()
        {
            RevisionFileSearchFilter = string.Empty;
        }

        /// <summary>
        ///     リビジョンファイルのサジェストリストをキャンセル（非表示に）する。
        /// </summary>
        public void CancelRevisionFileSuggestions()
        {
            RevisionFileSearchSuggestion = null;
        }

        /// <summary>
        ///     指定SHAのコミット情報を非同期で取得する。
        /// </summary>
        /// <param name="sha">取得対象のコミットSHA</param>
        /// <returns>コミット情報</returns>
        public async Task<Models.Commit> GetCommitAsync(string sha)
        {
            return await new Commands.QuerySingleCommit(_repo.FullPath, sha)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     リポジトリ内の相対パスから絶対パスを取得する。
        /// </summary>
        /// <param name="path">リポジトリ内の相対パス</param>
        /// <returns>絶対パス</returns>
        public string GetAbsPath(string path)
        {
            return Native.OS.GetAbsPath(_repo.FullPath, path);
        }

        /// <summary>
        ///     外部マージツールで変更を開く。
        /// </summary>
        /// <param name="c">対象の変更ファイル</param>
        public void OpenChangeInMergeTool(Models.Change c)
        {
            new Commands.DiffTool(_repo.FullPath, new Models.DiffOption(_commit, c)).Open();
        }

        /// <summary>
        ///     選択した変更をパッチファイルとして保存する。
        /// </summary>
        /// <param name="changes">パッチに含める変更ファイルリスト</param>
        /// <param name="saveTo">保存先のファイルパス</param>
        public async Task SaveChangesAsPatchAsync(List<Models.Change> changes, string saveTo)
        {
            if (_commit == null)
                return;

            // 親コミットのSHAを取得する（親がない場合は空ツリーSHAを使用）
            var baseRevision = _commit.Parents.Count == 0 ? Models.Commit.EmptyTreeSHA1 : _commit.Parents[0];
            var succ = await Commands.SaveChangesAsPatch.ProcessRevisionCompareChangesAsync(_repo.FullPath, changes, baseRevision, _commit.SHA, saveTo);
            if (succ)
                App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
        }

        /// <summary>
        ///     指定パスのファイルをこのコミットのリビジョンにリセットする。
        ///     変更リスト内のファイルの場合は変更種別に応じた処理を行う。
        /// </summary>
        /// <param name="path">リセット対象のファイルパス</param>
        public async Task ResetToThisRevisionAsync(string path)
        {
            // 変更リストから該当ファイルを検索する
            var c = _changes?.Find(x => x.Path.Equals(path, StringComparison.Ordinal));
            if (c != null)
            {
                await ResetToThisRevisionAsync(c);
                return;
            }

            // 変更リストにない場合は直接チェックアウトする
            var log = _repo.CreateLog($"Reset File to '{_commit.SHA}'");
            await new Commands.Checkout(_repo.FullPath).Use(log).FileWithRevisionAsync(path, _commit.SHA);
            log.Complete();
        }

        /// <summary>
        ///     変更ファイルをこのコミットのリビジョンにリセットする。
        ///     変更種別（削除、リネーム、その他）に応じた処理を行う。
        /// </summary>
        /// <param name="change">リセット対象の変更ファイル</param>
        public async Task ResetToThisRevisionAsync(Models.Change change)
        {
            var log = _repo.CreateLog($"Reset File to '{_commit.SHA}'");

            if (change.Index == Models.ChangeState.Deleted)
            {
                // このコミットで削除されたファイルは実際に削除する
                var fullpath = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
                if (File.Exists(fullpath))
                    await new Commands.Remove(_repo.FullPath, [change.Path])
                        .Use(log)
                        .ExecAsync();
            }
            else if (change.Index == Models.ChangeState.Renamed)
            {
                // リネームされたファイルは元のパスを削除し、新しいパスをチェックアウトする
                var old = Native.OS.GetAbsPath(_repo.FullPath, change.OriginalPath);
                if (File.Exists(old))
                    await new Commands.Remove(_repo.FullPath, [change.OriginalPath])
                        .Use(log)
                        .ExecAsync();

                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.Path, _commit.SHA);
            }
            else
            {
                // その他の変更はこのコミットの状態にチェックアウトする
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.Path, _commit.SHA);
            }

            log.Complete();
        }

        /// <summary>
        ///     変更ファイルを親コミットのリビジョンにリセットする。
        ///     変更種別（追加、リネーム、その他）に応じた処理を行う。
        /// </summary>
        /// <param name="change">リセット対象の変更ファイル</param>
        public async Task ResetToParentRevisionAsync(Models.Change change)
        {
            var log = _repo.CreateLog($"Reset File to '{_commit.SHA}~1'");

            if (change.Index == Models.ChangeState.Added)
            {
                // このコミットで追加されたファイルは削除する（親にはない）
                var fullpath = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
                if (File.Exists(fullpath))
                    await new Commands.Remove(_repo.FullPath, [change.Path])
                        .Use(log)
                        .ExecAsync();
            }
            else if (change.Index == Models.ChangeState.Renamed)
            {
                // リネームされたファイルは新パスを削除し、元パスを親リビジョンからチェックアウトする
                var renamed = Native.OS.GetAbsPath(_repo.FullPath, change.Path);
                if (File.Exists(renamed))
                    await new Commands.Remove(_repo.FullPath, [change.Path])
                        .Use(log)
                        .ExecAsync();

                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.OriginalPath, $"{_commit.SHA}~1");
            }
            else
            {
                // その他の変更は親コミットの状態にチェックアウトする
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .FileWithRevisionAsync(change.Path, $"{_commit.SHA}~1");
            }

            log.Complete();
        }

        /// <summary>
        ///     複数ファイルをこのコミットのリビジョンに一括リセットする。
        ///     削除対象とチェックアウト対象を分類して効率的に処理する。
        /// </summary>
        /// <param name="changes">リセット対象の変更ファイルリスト</param>
        public async Task ResetMultipleToThisRevisionAsync(List<Models.Change> changes)
        {
            var checkouts = new List<string>();
            var removes = new List<string>();

            // 変更種別に応じて削除リストとチェックアウトリストに分類する
            foreach (var c in changes)
            {
                if (c.Index == Models.ChangeState.Deleted)
                {
                    // 削除されたファイルは削除リストに追加する
                    var fullpath = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
                    if (File.Exists(fullpath))
                        removes.Add(c.Path);
                }
                else if (c.Index == Models.ChangeState.Renamed)
                {
                    // リネームされたファイルは元パスを削除し、新パスをチェックアウトする
                    var old = Native.OS.GetAbsPath(_repo.FullPath, c.OriginalPath);
                    if (File.Exists(old))
                        removes.Add(c.OriginalPath);

                    checkouts.Add(c.Path);
                }
                else
                {
                    // その他の変更はチェックアウトリストに追加する
                    checkouts.Add(c.Path);
                }
            }

            var log = _repo.CreateLog($"Reset Files to '{_commit.SHA}'");

            // git rmで不要ファイルを一括削除する
            if (removes.Count > 0)
                await new Commands.Remove(_repo.FullPath, removes)
                    .Use(log)
                    .ExecAsync();

            // git checkoutでこのコミットの状態に一括復元する
            if (checkouts.Count > 0)
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .MultipleFilesWithRevisionAsync(checkouts, _commit.SHA);

            log.Complete();
        }

        /// <summary>
        ///     複数ファイルを親コミットのリビジョンに一括リセットする。
        ///     削除対象とチェックアウト対象を分類して効率的に処理する。
        /// </summary>
        /// <param name="changes">リセット対象の変更ファイルリスト</param>
        public async Task ResetMultipleToParentRevisionAsync(List<Models.Change> changes)
        {
            var checkouts = new List<string>();
            var removes = new List<string>();

            // 変更種別に応じて削除リストとチェックアウトリストに分類する
            foreach (var c in changes)
            {
                if (c.Index == Models.ChangeState.Added)
                {
                    // 追加されたファイルは削除リストに追加する
                    var fullpath = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
                    if (File.Exists(fullpath))
                        removes.Add(c.Path);
                }
                else if (c.Index == Models.ChangeState.Renamed)
                {
                    // リネームされたファイルは新パスを削除し、元パスをチェックアウトする
                    var renamed = Native.OS.GetAbsPath(_repo.FullPath, c.Path);
                    if (File.Exists(renamed))
                        removes.Add(c.Path);

                    checkouts.Add(c.OriginalPath);
                }
                else
                {
                    // その他の変更はチェックアウトリストに追加する
                    checkouts.Add(c.Path);
                }
            }

            var log = _repo.CreateLog($"Reset Files to '{_commit.SHA}~1'");

            // git rmで不要ファイルを一括削除する
            if (removes.Count > 0)
                await new Commands.Remove(_repo.FullPath, removes)
                    .Use(log)
                    .ExecAsync();

            // git checkoutで親コミットの状態に一括復元する
            if (checkouts.Count > 0)
                await new Commands.Checkout(_repo.FullPath)
                    .Use(log)
                    .MultipleFilesWithRevisionAsync(checkouts, $"{_commit.SHA}~1");

            log.Complete();
        }

        /// <summary>
        ///     指定フォルダ配下のリビジョンファイルオブジェクトを非同期で取得する。
        /// </summary>
        /// <param name="parentFolder">親フォルダパス</param>
        /// <returns>リビジョンファイルオブジェクトのリスト</returns>
        public async Task<List<Models.Object>> GetRevisionFilesUnderFolderAsync(string parentFolder)
        {
            return await new Commands.QueryRevisionObjects(_repo.FullPath, _commit.SHA, parentFolder)
                .GetResultAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     リビジョンファイルを表示する。ファイルの種別（Blob/Commit）に応じて表示内容を切り替える。
        /// </summary>
        /// <param name="file">表示対象のファイルオブジェクト（nullの場合は表示をクリア）</param>
        public async Task ViewRevisionFileAsync(Models.Object file)
        {
            var obj = file ?? new Models.Object() { Path = string.Empty, Type = Models.ObjectType.None };
            ViewRevisionFilePath = obj.Path;

            switch (obj.Type)
            {
                case Models.ObjectType.Blob:
                    // 通常ファイル（Blob）の場合はデフォルトエディタで開ける
                    CanOpenRevisionFileWithDefaultEditor = true;
                    await SetViewingBlobAsync(obj);
                    break;
                case Models.ObjectType.Commit:
                    // サブモジュール（Commit）の場合はエディタで開けない
                    CanOpenRevisionFileWithDefaultEditor = false;
                    await SetViewingCommitAsync(obj);
                    break;
                default:
                    // その他の場合は表示をクリアする
                    CanOpenRevisionFileWithDefaultEditor = false;
                    ViewRevisionFileContent = null;
                    break;
            }
        }

        /// <summary>
        ///     リビジョンファイルを一時ファイルに保存して外部ツールまたはデフォルトエディタで開く。
        /// </summary>
        /// <param name="file">開くファイルのパス</param>
        /// <param name="tool">使用する外部ツール（nullの場合はデフォルトエディタ）</param>
        public async Task OpenRevisionFileAsync(string file, Models.ExternalTool tool)
        {
            // 一時ファイルのパスを組み立てる（ファイル名にコミットSHAの短縮形を含める）
            var fullPath = Native.OS.GetAbsPath(_repo.FullPath, file);
            var fileName = Path.GetFileNameWithoutExtension(fullPath) ?? "";
            var fileExt = Path.GetExtension(fullPath) ?? "";
            var tmpFile = Path.Combine(Path.GetTempPath(), $"{fileName}~{_commit.SHA.AsSpan(0, 10)}{fileExt}");

            // リビジョンファイルを一時ファイルに保存する
            await Commands.SaveRevisionFile
                .RunAsync(_repo.FullPath, _commit.SHA, file, tmpFile)
                .ConfigureAwait(false);

            // 外部ツールまたはデフォルトエディタで開く
            if (tool == null)
                Native.OS.OpenWithDefaultEditor(tmpFile);
            else
                tool.Launch(tmpFile.Quoted());
        }

        /// <summary>
        ///     リビジョンファイルを指定パスに保存する。
        /// </summary>
        /// <param name="file">保存対象のファイルオブジェクト</param>
        /// <param name="saveTo">保存先のファイルパス</param>
        public async Task SaveRevisionFileAsync(Models.Object file, string saveTo)
        {
            await Commands.SaveRevisionFile
                .RunAsync(_repo.FullPath, _commit.SHA, file.Path, saveTo)
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     コミットが変更された時に全データを再読み込みする。
        ///     フルメッセージ、署名情報、子コミット、変更ファイル一覧を非同期で取得する。
        /// </summary>
        private void Refresh()
        {
            // リビジョンファイル関連の状態をリセットする
            _requestingRevisionFiles = false;
            _revisionFiles = null;

            SignInfo = null;
            ViewRevisionFileContent = null;
            ViewRevisionFilePath = string.Empty;
            CanOpenRevisionFileWithDefaultEditor = false;
            Children = null;
            RevisionFileSearchFilter = string.Empty;
            RevisionFileSearchSuggestion = null;
            ScrollOffset = Vector.Zero;

            // コミットがnullの場合は空の状態にする
            if (_commit == null)
            {
                Changes = [];
                VisibleChanges = [];
                SelectedChanges = null;
                return;
            }

            // 前回の非同期処理をキャンセルする
            if (_cancellationSource is { IsCancellationRequested: false })
                _cancellationSource.Cancel();

            _cancellationSource = new CancellationTokenSource();
            var token = _cancellationSource.Token;

            // コミットのフルメッセージを非同期で取得する
            Task.Run(async () =>
            {
                var message = await new Commands.QueryCommitFullMessage(_repo.FullPath, _commit.SHA)
                    .GetResultAsync()
                    .ConfigureAwait(false);
                // メッセージ内のインライン要素（URL、SHA、コード）を解析する
                var inlines = await ParseInlinesInMessageAsync(message).ConfigureAwait(false);

                if (!token.IsCancellationRequested)
                    Dispatcher.UIThread.Post(() =>
                    {
                        FullMessage = new Models.CommitFullMessage
                        {
                            Message = message,
                            Inlines = inlines
                        };
                    });
            }, token);

            // コミットの署名情報を非同期で取得する
            Task.Run(async () =>
            {
                var signInfo = await new Commands.QueryCommitSignInfo(_repo.FullPath, _commit.SHA, !_repo.HasAllowedSignersFile)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                if (!token.IsCancellationRequested)
                    Dispatcher.UIThread.Post(() => SignInfo = signInfo);
            }, token);

            // 子コミットの表示が有効な場合は子コミットを非同期で取得する
            if (Preferences.Instance.ShowChildren)
            {
                Task.Run(async () =>
                {
                    var max = Preferences.Instance.MaxHistoryCommits;
                    var cmd = new Commands.QueryCommitChildren(_repo.FullPath, _commit.SHA, max) { CancellationToken = token };
                    var children = await cmd.GetResultAsync().ConfigureAwait(false);
                    if (!token.IsCancellationRequested)
                        Dispatcher.UIThread.Post(() => Children = children);
                }, token);
            }

            // 変更ファイル一覧を非同期で取得する
            Task.Run(async () =>
            {
                // 親コミットとの差分を取得する（親がない場合は空ツリーと比較）
                var parent = _commit.Parents.Count == 0 ? Models.Commit.EmptyTreeSHA1 : $"{_commit.SHA}^";
                var cmd = new Commands.CompareRevisions(_repo.FullPath, parent, _commit.SHA) { CancellationToken = token };
                var changes = await cmd.ReadAsync().ConfigureAwait(false);

                // フィルタが設定されている場合はフィルタを適用する
                var visible = changes;
                if (!string.IsNullOrWhiteSpace(_searchChangeFilter))
                {
                    visible = new List<Models.Change>();
                    foreach (var c in changes)
                    {
                        if (c.Path.Contains(_searchChangeFilter, StringComparison.OrdinalIgnoreCase))
                            visible.Add(c);
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    // UIスレッドで結果を反映し、先頭の変更を自動選択する
                    Dispatcher.UIThread.Post(() =>
                    {
                        Changes = changes;
                        VisibleChanges = visible;

                        if (visible.Count == 0)
                            SelectedChanges = null;
                        else
                            SelectedChanges = [VisibleChanges[0]];
                    });
                }
            }, token);
        }

        /// <summary>
        ///     コミットメッセージ内のインライン要素（URL、コミットSHA、インラインコード）を解析する。
        ///     イシュートラッカーのルールも適用する。
        /// </summary>
        /// <param name="message">コミットメッセージ</param>
        /// <returns>インライン要素のコレクション</returns>
        private async Task<Models.InlineElementCollector> ParseInlinesInMessageAsync(string message)
        {
            var inlines = new Models.InlineElementCollector();

            // イシュートラッカーのルールにマッチするリンクを追加する
            if (_repo.IssueTrackers is { Count: > 0 } rules)
            {
                foreach (var rule in rules)
                    rule.Matches(inlines, message);
            }

            // URLパターンにマッチするリンクを追加する
            var urlMatches = REG_URL_FORMAT().Matches(message);
            foreach (Match match in urlMatches)
            {
                var start = match.Index;
                var len = match.Length;
                // 既存のインライン要素と重複しないか確認する
                if (inlines.Intersect(start, len) != null)
                    continue;

                var url = message.Substring(start, len);
                if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    inlines.Add(new Models.InlineElement(Models.InlineElementType.Link, start, len, url));
            }

            // コミットSHAパターンにマッチする参照を追加する
            var shaMatches = REG_SHA_FORMAT().Matches(message);
            foreach (Match match in shaMatches)
            {
                var start = match.Index;
                var len = match.Length;
                if (inlines.Intersect(start, len) != null)
                    continue;

                // 実際にコミットSHAとして有効か確認する
                var sha = match.Groups[1].Value;
                var isCommitSHA = await new Commands.IsCommitSHA(_repo.FullPath, sha).GetResultAsync().ConfigureAwait(false);
                if (isCommitSHA)
                    inlines.Add(new Models.InlineElement(Models.InlineElementType.CommitSHA, start, len, sha));
            }

            // インラインコードパターン（バッククォート囲み）を追加する
            var inlineCodeMatches = REG_INLINECODE_FORMAT().Matches(message);
            foreach (Match match in inlineCodeMatches)
            {
                var start = match.Index;
                var len = match.Length;
                if (inlines.Intersect(start, len) != null)
                    continue;

                // バッククォートの内側の範囲で要素を追加する
                inlines.Add(new Models.InlineElement(Models.InlineElementType.Code, start + 1, len - 2, string.Empty));
            }

            // 位置順でソートする
            inlines.Sort();
            return inlines;
        }

        /// <summary>
        ///     フィルタに基づいて表示用変更ファイルリストを更新する。
        /// </summary>
        private void RefreshVisibleChanges()
        {
            if (string.IsNullOrEmpty(_searchChangeFilter))
            {
                // フィルタなしの場合は全変更を表示する
                VisibleChanges = _changes;
            }
            else
            {
                // パスにフィルタ文字列を含む変更のみ表示する
                var visible = new List<Models.Change>();
                foreach (var c in _changes)
                {
                    if (c.Path.Contains(_searchChangeFilter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(c);
                }

                VisibleChanges = visible;
            }
        }

        /// <summary>
        ///     リビジョンファイル検索のサジェストリストを更新する。
        ///     ファイル名リストが未取得の場合は非同期で取得する。
        /// </summary>
        private void RefreshRevisionSearchSuggestion()
        {
            if (!string.IsNullOrEmpty(_revisionFileSearchFilter))
            {
                if (_revisionFiles == null)
                {
                    // ファイルリストがまだ取得されていない場合は非同期で取得する
                    if (_requestingRevisionFiles)
                        return;

                    var sha = Commit.SHA;
                    _requestingRevisionFiles = true;

                    Task.Run(async () =>
                    {
                        // コミット内の全ファイル名を取得する
                        var files = await new Commands.QueryRevisionFileNames(_repo.FullPath, sha)
                            .GetResultAsync()
                            .ConfigureAwait(false);

                        Dispatcher.UIThread.Post(() =>
                        {
                            // 取得結果が現在のコミットに対応しているか確認する
                            if (sha == Commit.SHA && _requestingRevisionFiles)
                            {
                                _revisionFiles = files;
                                _requestingRevisionFiles = false;

                                // フィルタが設定されている場合はサジェストを計算する
                                if (!string.IsNullOrEmpty(_revisionFileSearchFilter))
                                    CalcRevisionFileSearchSuggestion();
                            }
                        });
                    });
                }
                else
                {
                    // ファイルリストが既に取得済みの場合はサジェストを直接計算する
                    CalcRevisionFileSearchSuggestion();
                }
            }
            else
            {
                // フィルタが空の場合はサジェストをクリアしてGCを促す
                RevisionFileSearchSuggestion = null;
                GC.Collect();
            }
        }

        /// <summary>
        ///     フィルタ文字列に基づいてサジェストリストを計算する。
        ///     最大100件まで返す。
        /// </summary>
        private void CalcRevisionFileSearchSuggestion()
        {
            var suggestion = new List<string>();
            foreach (var file in _revisionFiles)
            {
                // フィルタ文字列を含み、かつ完全一致ではないファイルをサジェストに追加する
                if (file.Contains(_revisionFileSearchFilter, StringComparison.OrdinalIgnoreCase) &&
                    file.Length != _revisionFileSearchFilter.Length)
                    suggestion.Add(file);

                // サジェスト数の上限を100件とする
                if (suggestion.Count >= 100)
                    break;
            }

            RevisionFileSearchSuggestion = suggestion;
        }

        /// <summary>
        ///     Blobオブジェクト（通常ファイル）の表示内容を設定する。
        ///     バイナリ/テキスト/LFS/画像を判別して適切な表示モデルを生成する。
        /// </summary>
        /// <param name="file">表示対象のファイルオブジェクト</param>
        private async Task SetViewingBlobAsync(Models.Object file)
        {
            // バイナリファイルかどうかを判定する
            var isBinary = await new Commands.IsBinary(_repo.FullPath, _commit.SHA, file.Path).GetResultAsync();
            if (isBinary)
            {
                // 画像ファイルの場合はデコーダを使用して表示する
                var imgDecoder = ImageSource.GetDecoder(file.Path);
                if (imgDecoder != Models.ImageDecoder.None)
                {
                    var source = await ImageSource.FromRevisionAsync(_repo.FullPath, _commit.SHA, file.Path, imgDecoder);
                    ViewRevisionFileContent = new Models.RevisionImageFile(file.Path, source.Bitmap, source.Size);
                }
                else
                {
                    // 画像以外のバイナリファイルはサイズ情報のみ表示する
                    var size = await new Commands.QueryFileSize(_repo.FullPath, file.Path, _commit.SHA).GetResultAsync();
                    ViewRevisionFileContent = new Models.RevisionBinaryFile() { Size = size };
                }

                return;
            }

            // テキストファイルの内容を読み込む
            var contentStream = await Commands.QueryFileContent.RunAsync(_repo.FullPath, _commit.SHA, file.Path);
            var content = await new StreamReader(contentStream).ReadToEndAsync();

            // LFSオブジェクトかどうかを判定する
            var lfs = Models.LFSObject.Parse(content);
            if (lfs != null)
            {
                // LFSオブジェクトの場合は画像かどうかで表示を切り替える
                var imgDecoder = ImageSource.GetDecoder(file.Path);
                if (imgDecoder != Models.ImageDecoder.None)
                    ViewRevisionFileContent = new RevisionLFSImage(_repo.FullPath, file.Path, lfs, imgDecoder);
                else
                    ViewRevisionFileContent = new Models.RevisionLFSObject() { Object = lfs };
            }
            else
            {
                // 通常テキストファイルの内容を表示する
                ViewRevisionFileContent = new Models.RevisionTextFile() { FileName = file.Path, Content = content };
            }
        }

        /// <summary>
        ///     Commitオブジェクト（サブモジュール）の表示内容を設定する。
        ///     サブモジュールのコミット情報とメッセージを取得して表示する。
        /// </summary>
        /// <param name="file">表示対象のファイルオブジェクト（サブモジュール）</param>
        private async Task SetViewingCommitAsync(Models.Object file)
        {
            // サブモジュールのルートパスを構築する
            var submoduleRoot = Path.Combine(_repo.FullPath, file.Path).Replace('\\', '/').Trim('/');
            var commit = await new Commands.QuerySingleCommit(submoduleRoot, file.SHA).GetResultAsync();
            if (commit == null)
            {
                // コミット情報が取得できない場合はSHAのみ表示する
                ViewRevisionFileContent = new Models.RevisionSubmodule()
                {
                    Commit = new Models.Commit() { SHA = file.SHA },
                    FullMessage = new Models.CommitFullMessage()
                };
            }
            else
            {
                // コミット情報とフルメッセージを取得して表示する
                var message = await new Commands.QueryCommitFullMessage(submoduleRoot, file.SHA).GetResultAsync();
                ViewRevisionFileContent = new Models.RevisionSubmodule()
                {
                    Commit = commit,
                    FullMessage = new Models.CommitFullMessage { Message = message }
                };
            }
        }

        /// <summary>URLパターンの正規表現（http/https/ftp）</summary>
        [GeneratedRegex(@"\b(https?://|ftp://)[\w\d\._/\-~%@()+:?&=#!]*[\w\d/]")]
        private static partial Regex REG_URL_FORMAT();

        /// <summary>コミットSHAパターンの正規表現（6-40桁の16進数）</summary>
        [GeneratedRegex(@"\b([0-9a-fA-F]{6,40})\b")]
        private static partial Regex REG_SHA_FORMAT();

        /// <summary>インラインコードパターンの正規表現（バッククォート囲み）</summary>
        [GeneratedRegex(@"`.*?`")]
        private static partial Regex REG_INLINECODE_FORMAT();

        /// <summary>対象リポジトリへの参照</summary>
        private Repository _repo = null;
        /// <summary>タブインデックス共有データ</summary>
        private CommitDetailSharedData _sharedData = null;
        /// <summary>表示対象のコミット</summary>
        private Models.Commit _commit = null;
        /// <summary>コミットのフルメッセージ</summary>
        private Models.CommitFullMessage _fullMessage = null;
        /// <summary>コミットの署名情報</summary>
        private Models.CommitSignInfo _signInfo = null;
        /// <summary>子コミットSHAリスト</summary>
        private List<string> _children = null;
        /// <summary>全変更ファイルリスト</summary>
        private List<Models.Change> _changes = [];
        /// <summary>表示中の変更ファイルリスト</summary>
        private List<Models.Change> _visibleChanges = [];
        /// <summary>選択中の変更ファイルリスト</summary>
        private List<Models.Change> _selectedChanges = null;
        /// <summary>変更ファイル検索フィルタ文字列</summary>
        private string _searchChangeFilter = string.Empty;
        /// <summary>差分表示用コンテキスト</summary>
        private DiffContext _diffContext = null;
        /// <summary>リビジョンファイルビューアの表示パス</summary>
        private string _viewRevisionFilePath = string.Empty;
        /// <summary>リビジョンファイルビューアの表示内容</summary>
        private object _viewRevisionFileContent = null;
        /// <summary>非同期処理キャンセル用トークンソース</summary>
        private CancellationTokenSource _cancellationSource = null;
        /// <summary>リビジョンファイル名一覧取得中フラグ</summary>
        private bool _requestingRevisionFiles = false;
        /// <summary>リビジョンファイル名一覧のキャッシュ</summary>
        private List<string> _revisionFiles = null;
        /// <summary>リビジョンファイル検索フィルタ文字列</summary>
        private string _revisionFileSearchFilter = string.Empty;
        /// <summary>リビジョンファイル検索サジェストリスト</summary>
        private List<string> _revisionFileSearchSuggestion = null;
        /// <summary>デフォルトエディタで開けるかフラグ</summary>
        private bool _canOpenRevisionFileWithDefaultEditor = false;
        /// <summary>スクロール位置オフセット</summary>
        private Vector _scrollOffset = Vector.Zero;
    }
}
