using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     リビジョンのファイル情報を保持するレコード。ファイルパス、表示コンテンツ、外部エディタで開けるかのフラグを持つ。
    /// </summary>
    public class FileHistoriesRevisionFile(string path, object content = null, bool canOpenWithDefaultEditor = false)
    {
        public string Path { get; set; } = path;
        public object Content { get; set; } = content;
        public bool CanOpenWithDefaultEditor { get; set; } = canOpenWithDefaultEditor;
    }

    /// <summary>
    ///     単一リビジョンでのファイル表示ViewModel。
    ///     差分モードとファイル内容表示モードの切り替えに対応する。
    /// </summary>
    public class FileHistoriesSingleRevision : ObservableObject
    {
        /// <summary>
        ///     差分表示モードかどうか。切替時にコンテンツを再読み込みする。
        /// </summary>
        public bool IsDiffMode
        {
            get => _isDiffMode;
            set
            {
                if (SetProperty(ref _isDiffMode, value))
                    RefreshViewContent();
            }
        }

        /// <summary>
        ///     表示コンテンツ（差分表示またはファイル内容）。
        /// </summary>
        public object ViewContent
        {
            get => _viewContent;
            set => SetProperty(ref _viewContent, value);
        }

        /// <summary>
        ///     コンストラクタ。リポジトリパス、ファイル、リビジョン、前回の差分モード状態を指定する。
        /// </summary>
        public FileHistoriesSingleRevision(string repo, string file, Models.Commit revision, bool prevIsDiffMode)
        {
            _repo = repo;
            _file = file;
            _revision = revision;
            _isDiffMode = prevIsDiffMode;
            _viewContent = null;

            RefreshViewContent();
        }

        /// <summary>
        ///     選択リビジョンの状態にファイルをリセットする。
        /// </summary>
        public async Task<bool> ResetToSelectedRevisionAsync()
        {
            return await new Commands.Checkout(_repo)
                .FileWithRevisionAsync(_file, $"{_revision.SHA}")
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     リビジョンのファイルを一時ファイルに保存し、デフォルトエディタで開く。
        /// </summary>
        public async Task OpenWithDefaultEditorAsync()
        {
            if (_viewContent is not FileHistoriesRevisionFile { CanOpenWithDefaultEditor: true })
                return;

            var fullPath = Native.OS.GetAbsPath(_repo, _file);
            var fileName = Path.GetFileNameWithoutExtension(fullPath) ?? "";
            var fileExt = Path.GetExtension(fullPath) ?? "";
            var tmpFile = Path.Combine(Path.GetTempPath(), $"{fileName}~{_revision.SHA.AsSpan(0, 10)}{fileExt}");

            await Commands.SaveRevisionFile
                .RunAsync(_repo, _revision.SHA, _file, tmpFile)
                .ConfigureAwait(false);

            Native.OS.OpenWithDefaultEditor(tmpFile);
        }

        /// <summary>
        ///     表示コンテンツを再読み込みする。差分モードか内容表示モードかで処理を分岐する。
        /// </summary>
        private void RefreshViewContent()
        {
            if (_isDiffMode)
            {
                SetViewContentAsDiff();
                return;
            }

            Task.Run(async () =>
            {
                var objs = await new Commands.QueryRevisionObjects(_repo, _revision.SHA, _file)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                if (objs.Count == 0)
                {
                    Dispatcher.UIThread.Post(() => ViewContent = new FileHistoriesRevisionFile(_file));
                    return;
                }

                var revisionContent = await GetRevisionFileContentAsync(objs[0]).ConfigureAwait(false);
                Dispatcher.UIThread.Post(() => ViewContent = revisionContent);
            });
        }

        /// <summary>
        ///     リビジョンファイルの内容を非同期で取得する。
        ///     Blob（テキスト/バイナリ/画像/LFS）とサブモジュールコミットに対応する。
        /// </summary>
        private async Task<object> GetRevisionFileContentAsync(Models.Object obj)
        {
            if (obj.Type == Models.ObjectType.Blob)
            {
                var isBinary = await new Commands.IsBinary(_repo, _revision.SHA, _file).GetResultAsync().ConfigureAwait(false);
                if (isBinary)
                {
                    var imgDecoder = ImageSource.GetDecoder(_file);
                    if (imgDecoder != Models.ImageDecoder.None)
                    {
                        var source = await ImageSource.FromRevisionAsync(_repo, _revision.SHA, _file, imgDecoder).ConfigureAwait(false);
                        var image = new Models.RevisionImageFile(_file, source.Bitmap, source.Size);
                        return new FileHistoriesRevisionFile(_file, image, true);
                    }

                    var size = await new Commands.QueryFileSize(_repo, _file, _revision.SHA).GetResultAsync().ConfigureAwait(false);
                    var binaryFile = new Models.RevisionBinaryFile() { Size = size };
                    return new FileHistoriesRevisionFile(_file, binaryFile, true);
                }

                var contentStream = await Commands.QueryFileContent.RunAsync(_repo, _revision.SHA, _file).ConfigureAwait(false);
                var content = await new StreamReader(contentStream).ReadToEndAsync();
                var lfs = Models.LFSObject.Parse(content);
                if (lfs != null)
                {
                    var imgDecoder = ImageSource.GetDecoder(_file);
                    if (imgDecoder != Models.ImageDecoder.None)
                    {
                        var combined = new RevisionLFSImage(_repo, _file, lfs, imgDecoder);
                        return new FileHistoriesRevisionFile(_file, combined, true);
                    }

                    var rlfs = new Models.RevisionLFSObject() { Object = lfs };
                    return new FileHistoriesRevisionFile(_file, rlfs, true);
                }

                var txt = new Models.RevisionTextFile() { FileName = obj.Path, Content = content };
                return new FileHistoriesRevisionFile(_file, txt, true);
            }

            if (obj.Type == Models.ObjectType.Commit)
            {
                var submoduleRoot = Path.Combine(_repo, _file);
                var commit = await new Commands.QuerySingleCommit(submoduleRoot, obj.SHA).GetResultAsync().ConfigureAwait(false);
                var message = commit != null ? await new Commands.QueryCommitFullMessage(submoduleRoot, obj.SHA).GetResultAsync().ConfigureAwait(false) : null;
                var module = new Models.RevisionSubmodule()
                {
                    Commit = commit ?? new Models.Commit() { SHA = obj.SHA },
                    FullMessage = new Models.CommitFullMessage { Message = message }
                };

                return new FileHistoriesRevisionFile(_file, module);
            }

            return new FileHistoriesRevisionFile(_file);
        }

        /// <summary>
        ///     差分モードの表示コンテンツを設定する。
        /// </summary>
        private void SetViewContentAsDiff()
        {
            var option = new Models.DiffOption(_revision, _file);
            ViewContent = new DiffContext(_repo, option, _viewContent as DiffContext);
        }

        private string _repo = null;
        private string _file = null;
        private Models.Commit _revision = null;
        private bool _isDiffMode = false;
        private object _viewContent = null;
    }

    /// <summary>
    ///     2つのリビジョン間でファイルを比較するViewModel。
    /// </summary>
    public class FileHistoriesCompareRevisions : ObservableObject
    {
        /// <summary>
        ///     比較の開始リビジョン。
        /// </summary>
        public Models.Commit StartPoint
        {
            get => _startPoint;
            set => SetProperty(ref _startPoint, value);
        }

        /// <summary>
        ///     比較の終了リビジョン。
        /// </summary>
        public Models.Commit EndPoint
        {
            get => _endPoint;
            set => SetProperty(ref _endPoint, value);
        }

        /// <summary>
        ///     差分の表示コンテンツ。
        /// </summary>
        public DiffContext ViewContent
        {
            get => _viewContent;
            set => SetProperty(ref _viewContent, value);
        }

        /// <summary>
        ///     コンストラクタ。リポジトリパス、ファイル、開始・終了リビジョンを指定する。
        /// </summary>
        public FileHistoriesCompareRevisions(string repo, string file, Models.Commit start, Models.Commit end)
        {
            _repo = repo;
            _file = file;
            _startPoint = start;
            _endPoint = end;
            RefreshViewContent();
        }

        /// <summary>
        ///     開始・終了リビジョンを入れ替えて差分を再表示する。
        /// </summary>
        public void Swap()
        {
            (StartPoint, EndPoint) = (_endPoint, _startPoint);
            RefreshViewContent();
        }

        /// <summary>
        ///     差分をパッチファイルとして保存する。
        /// </summary>
        public async Task<bool> SaveAsPatch(string saveTo)
        {
            return await Commands.SaveChangesAsPatch
                .ProcessRevisionCompareChangesAsync(_repo, _changes, _startPoint.SHA, _endPoint.SHA, saveTo)
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     リビジョン間の差分コンテンツを非同期で再読み込みする。
        /// </summary>
        private void RefreshViewContent()
        {
            Task.Run(async () =>
            {
                _changes = await new Commands.CompareRevisions(_repo, _startPoint.SHA, _endPoint.SHA, _file).ReadAsync().ConfigureAwait(false);
                if (_changes.Count == 0)
                {
                    Dispatcher.UIThread.Post(() => ViewContent = null);
                }
                else
                {
                    var option = new Models.DiffOption(_startPoint.SHA, _endPoint.SHA, _changes[0]);
                    Dispatcher.UIThread.Post(() => ViewContent = new DiffContext(_repo, option, _viewContent));
                }
            });
        }

        private string _repo = null;
        private string _file = null;
        private Models.Commit _startPoint = null;
        private Models.Commit _endPoint = null;
        private List<Models.Change> _changes = [];
        private DiffContext _viewContent = null;
    }

    /// <summary>
    ///     ファイルの履歴（コミット一覧）を表示するためのViewModel。
    ///     単一リビジョン表示と2リビジョン比較の両モードに対応する。
    /// </summary>
    public class FileHistories : ObservableObject
    {
        /// <summary>
        ///     表示タイトル（ファイルパスとオプションのコミット）。
        /// </summary>
        public string Title
        {
            get;
        }

        /// <summary>
        ///     コミット履歴の読み込み中かどうか。
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        ///     ファイルに関連するコミット一覧。
        /// </summary>
        public List<Models.Commit> Commits
        {
            get => _commits;
            set => SetProperty(ref _commits, value);
        }

        /// <summary>
        ///     現在選択されているコミットのリスト（1つで単一表示、2つで比較表示）。
        /// </summary>
        public AvaloniaList<Models.Commit> SelectedCommits
        {
            get;
            set;
        } = [];

        /// <summary>
        ///     選択状態に応じた表示コンテンツ（単一リビジョン/比較/選択数）。
        /// </summary>
        public object ViewContent
        {
            get => _viewContent;
            private set => SetProperty(ref _viewContent, value);
        }

        /// <summary>
        ///     コンストラクタ。リポジトリパス、ファイルパス、オプションのコミットSHAを指定する。
        ///     非同期でコミット履歴を取得し、選択変更時のコンテンツ切替ハンドラを登録する。
        /// </summary>
        public FileHistories(string repo, string file, string commit = null)
        {
            if (!string.IsNullOrEmpty(commit))
                Title = $"{file} @ {commit}";
            else
                Title = file;

            _repo = repo;

            Task.Run(async () =>
            {
                var argsBuilder = new StringBuilder();
                argsBuilder
                    .Append("--date-order -n 10000 ")
                    .Append(commit ?? string.Empty)
                    .Append(" -- ")
                    .Append(file.Quoted());

                var commits = await new Commands.QueryCommits(_repo, argsBuilder.ToString(), false)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    IsLoading = false;
                    Commits = commits;
                    if (Commits.Count > 0)
                        SelectedCommits.Add(Commits[0]);
                });
            });

            // 選択コミット変更時のハンドラ：選択数に応じて表示モードを切替
            SelectedCommits.CollectionChanged += (_, _) =>
            {
                // 現在の差分モード状態を保持
                if (_viewContent is FileHistoriesSingleRevision singleRevision)
                    _prevIsDiffMode = singleRevision.IsDiffMode;

                ViewContent = SelectedCommits.Count switch
                {
                    1 => new FileHistoriesSingleRevision(_repo, file, SelectedCommits[0], _prevIsDiffMode),
                    2 => new FileHistoriesCompareRevisions(_repo, file, SelectedCommits[0], SelectedCommits[1]),
                    _ => SelectedCommits.Count,
                };
            };
        }

        /// <summary>
        ///     指定コミットをリポジトリの履歴ビューで表示する。
        ///     ランチャーのページから対象リポジトリを検索してナビゲートする。
        /// </summary>
        public void NavigateToCommit(Models.Commit commit)
        {
            var launcher = App.GetLauncher();
            if (launcher != null)
            {
                foreach (var page in launcher.Pages)
                {
                    if (page.Data is Repository repo && repo.FullPath.Equals(_repo, StringComparison.Ordinal))
                    {
                        repo.NavigateToCommit(commit.SHA);
                        break;
                    }
                }
            }
        }

        /// <summary>
        ///     コミットの完全なメッセージを取得する。キャッシュ済みの場合はキャッシュから返す。
        /// </summary>
        public string GetCommitFullMessage(Models.Commit commit)
        {
            var sha = commit.SHA;
            if (_fullCommitMessages.TryGetValue(sha, out var msg))
                return msg;

            msg = new Commands.QueryCommitFullMessage(_repo, sha).GetResult();
            _fullCommitMessages[sha] = msg;
            return msg;
        }

        private readonly string _repo = null; // リポジトリパス
        private bool _isLoading = true; // 読み込み中フラグ
        private bool _prevIsDiffMode = true; // 前回の差分モード状態
        private List<Models.Commit> _commits = null; // コミット一覧
        private Dictionary<string, string> _fullCommitMessages = new(); // コミットメッセージのキャッシュ
        private object _viewContent = null; // 現在の表示コンテンツ
    }
}
