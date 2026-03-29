using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// ファイル履歴の特定リビジョンにおけるファイル情報。
/// パス、コンテンツ、デフォルトエディタで開けるかどうかを保持する。
/// </summary>
public class FileHistoriesRevisionFile(string path, object content = null, bool canOpenWithDefaultEditor = false)
{
    /// <summary>ファイルパス。</summary>
    public string Path { get; set; } = path;
    /// <summary>ファイルの内容（テキスト、画像、バイナリ等の型で格納）。</summary>
    public object Content { get; set; } = content;
    /// <summary>デフォルトエディタで開けるかどうか。</summary>
    public bool CanOpenWithDefaultEditor { get; set; } = canOpenWithDefaultEditor;
}

/// <summary>
/// 単一リビジョンのファイル履歴表示ViewModel。
/// diff表示モードとファイル内容表示モードの切り替えに対応する。
/// </summary>
public class FileHistoriesSingleRevision : ObservableObject
{
    /// <summary>diff表示モードかどうか。切り替え時にコンテンツを再読み込みする。</summary>
    public bool IsDiffMode
    {
        get => _isDiffMode;
        set
        {
            if (SetProperty(ref _isDiffMode, value))
                RefreshViewContent();
        }
    }

    /// <summary>表示中のコンテンツ（DiffContextまたはFileHistoriesRevisionFile）。</summary>
    public object ViewContent
    {
        get => _viewContent;
        set => SetProperty(ref _viewContent, value);
    }

    /// <summary>
    /// コンストラクタ。リポジトリパス、対象リビジョン、前回のdiffモード状態を指定する。
    /// </summary>
    public FileHistoriesSingleRevision(string repo, Models.FileVersion revision, bool prevIsDiffMode)
    {
        _repo = repo;
        _file = revision.Path;
        _revision = revision;
        _isDiffMode = prevIsDiffMode;
        _viewContent = null;

        RefreshViewContent();
    }

    /// <summary>
    /// 作業ディレクトリのファイルを選択中のリビジョンにリセットする。
    /// </summary>
    public async Task<bool> ResetToSelectedRevisionAsync()
    {
        return await new Commands.Checkout(_repo)
            .FileWithRevisionAsync(_file, $"{_revision.SHA}")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 選択リビジョンのファイルを一時ファイルに保存し、デフォルトエディタで開く。
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
    /// 表示モードに応じてコンテンツを再読み込みする。
    /// diffモードではDiffContextを、ファイルモードではリビジョンのファイル内容を取得する。
    /// </summary>
    private void RefreshViewContent()
    {
        if (_isDiffMode)
        {
            ViewContent = new DiffContext(_repo, new(_revision), _viewContent as DiffContext);
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
    /// Gitオブジェクトの種類（Blob/Commit）に応じてファイルコンテンツを取得する。
    /// バイナリ、画像、LFS、テキスト、サブモジュールの各形式に対応する。
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
            string content;
            using (var reader = new StreamReader(contentStream))
                content = await reader.ReadToEndAsync();
            var lfs = Models.LFSObject.Parse(content);
            if (lfs is not null)
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
            var message = commit is not null ? await new Commands.QueryCommitFullMessage(submoduleRoot, obj.SHA).GetResultAsync().ConfigureAwait(false) : null;
            var module = new Models.RevisionSubmodule()
            {
                Commit = commit ?? new Models.Commit() { SHA = obj.SHA },
                FullMessage = new Models.CommitFullMessage { Message = message }
            };

            return new FileHistoriesRevisionFile(_file, module);
        }

        return new FileHistoriesRevisionFile(_file);
    }

    private string _repo = null; // リポジトリパス
    private string _file = null; // 対象ファイルパス
    private Models.FileVersion _revision = null; // 対象リビジョン
    private bool _isDiffMode = false; // diff表示モードフラグ
    private object _viewContent = null; // 現在の表示コンテンツ
}

/// <summary>
/// 2つのリビジョン間のファイル比較ViewModel。
/// 開始・終了リビジョンを指定してdiff表示を行う。
/// </summary>
public class FileHistoriesCompareRevisions : ObservableObject
{
    /// <summary>比較の開始リビジョン。</summary>
    public Models.FileVersion StartPoint
    {
        get => _startPoint;
        set => SetProperty(ref _startPoint, value);
    }

    /// <summary>比較の終了リビジョン。</summary>
    public Models.FileVersion EndPoint
    {
        get => _endPoint;
        set => SetProperty(ref _endPoint, value);
    }

    /// <summary>diff表示コンテキスト。</summary>
    public DiffContext ViewContent
    {
        get => _viewContent;
        set => SetProperty(ref _viewContent, value);
    }

    /// <summary>
    /// コンストラクタ。リポジトリパスと比較対象の2つのリビジョンを指定する。
    /// </summary>
    public FileHistoriesCompareRevisions(string repo, Models.FileVersion start, Models.FileVersion end)
    {
        _repo = repo;
        _startPoint = start;
        _endPoint = end;
        _viewContent = new(_repo, new(start, end));
    }

    /// <summary>開始・終了リビジョンを入れ替えて比較を再実行する。</summary>
    public void Swap()
    {
        (StartPoint, EndPoint) = (_endPoint, _startPoint);
        ViewContent = new(_repo, new(_startPoint, _endPoint), _viewContent);
    }

    /// <summary>比較結果をパッチファイルとして保存する。</summary>
    public async Task<bool> SaveAsPatch(string saveTo)
    {
        return await Commands.SaveChangesAsPatch
            .ProcessRevisionCompareChangesAsync(_repo, _changes, _startPoint.SHA, _endPoint.SHA, saveTo)
            .ConfigureAwait(false);
    }

    private string _repo = null; // リポジトリパス
    private Models.FileVersion _startPoint = null; // 比較開始リビジョン
    private Models.FileVersion _endPoint = null; // 比較終了リビジョン
    private List<Models.Change> _changes = []; // 変更ファイルリスト
    private DiffContext _viewContent = null; // diff表示コンテキスト
}

/// <summary>
/// ファイル履歴画面のメインViewModel。
/// 特定ファイルの全リビジョンを一覧表示し、選択に応じて詳細・比較を表示する。
/// </summary>
public class FileHistories : ObservableObject
{
    /// <summary>画面タイトル（ファイルパス、オプションでコミットSHA付き）。</summary>
    public string Title
    {
        get;
    }

    /// <summary>リビジョン一覧の読み込み中かどうか。</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>ファイルのリビジョン一覧。</summary>
    public List<Models.FileVersion> Revisions
    {
        get => _revisions;
        set => SetProperty(ref _revisions, value);
    }

    /// <summary>選択中のリビジョン。1つ選択で詳細表示、2つ選択で比較表示。</summary>
    public AvaloniaList<Models.FileVersion> SelectedRevisions
    {
        get;
        set;
    } = [];

    /// <summary>詳細パネルのコンテンツ（単一リビジョン表示または比較表示）。</summary>
    public object ViewContent
    {
        get => _viewContent;
        private set => SetProperty(ref _viewContent, value);
    }

    /// <summary>
    /// コンストラクタ。リポジトリパス、ファイルパス、オプションでコミットSHAを指定する。
    /// バックグラウンドでリビジョン一覧を取得する。
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
            var revisions = await new Commands.QueryFileHistory(_repo, file, commit)
                .GetResultAsync()
                .ConfigureAwait(false);

            Dispatcher.UIThread.Post(() =>
            {
                IsLoading = false;
                Revisions = revisions;
                if (revisions.Count > 0)
                    SelectedRevisions.Add(revisions[0]);
            });
        });

        SelectedRevisions.CollectionChanged += (_, _) =>
        {
            if (_viewContent is FileHistoriesSingleRevision singleRevision)
                _prevIsDiffMode = singleRevision.IsDiffMode;

            ViewContent = SelectedRevisions.Count switch
            {
                1 => new FileHistoriesSingleRevision(_repo, SelectedRevisions[0], _prevIsDiffMode),
                2 => new FileHistoriesCompareRevisions(_repo, SelectedRevisions[0], SelectedRevisions[1]),
                _ => SelectedRevisions.Count,
            };
        };
    }

    /// <summary>
    /// 指定リビジョンのコミットへメインのリポジトリビューでナビゲートする。
    /// </summary>
    public void NavigateToCommit(Models.FileVersion revision)
    {
        var launcher = App.GetLauncher();
        if (launcher is not null)
        {
            foreach (var page in launcher.Pages)
            {
                if (page.Data is Repository repo && repo.FullPath.Equals(_repo, StringComparison.Ordinal))
                {
                    repo.NavigateToCommit(revision.SHA);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 指定リビジョンのコミットの完全なメッセージを取得する（キャッシュ付き）。
    /// </summary>
    public string GetCommitFullMessage(Models.FileVersion revision)
    {
        var sha = revision.SHA;
        if (_fullCommitMessages.TryGetValue(sha, out var msg))
            return msg;

        msg = new Commands.QueryCommitFullMessage(_repo, sha).GetResult();
        _fullCommitMessages[sha] = msg;
        return msg;
    }

    private readonly string _repo = null; // リポジトリパス
    private bool _isLoading = true; // 読み込み中フラグ
    private bool _prevIsDiffMode = true; // 前回のdiffモード状態
    private List<Models.FileVersion> _revisions = null; // リビジョン一覧
    private Dictionary<string, string> _fullCommitMessages = new(); // コミットメッセージキャッシュ
    private object _viewContent = null; // 詳細パネルのコンテンツ
}
