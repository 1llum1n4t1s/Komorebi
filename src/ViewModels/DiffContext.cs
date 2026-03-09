using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     差分表示のコンテキストを管理するViewModel。
    ///     テキスト差分、バイナリ差分、画像差分、LFS差分、サブモジュール差分など複数の形式に対応する。
    /// </summary>
    public class DiffContext : ObservableObject
    {
        /// <summary>
        ///     差分のタイトル（ファイルパスまたはリネーム情報）。
        /// </summary>
        public string Title
        {
            get;
        }

        /// <summary>
        ///     空白文字の変更を無視するかどうか。変更時に差分を再読み込みする。
        /// </summary>
        public bool IgnoreWhitespace
        {
            get => Preferences.Instance.IgnoreWhitespaceChangesInDiff;
            set
            {
                if (value != Preferences.Instance.IgnoreWhitespaceChangesInDiff)
                {
                    Preferences.Instance.IgnoreWhitespaceChangesInDiff = value;
                    OnPropertyChanged();
                    LoadContent();
                }
            }
        }

        /// <summary>
        ///     ファイル全体を表示するかどうか。
        /// </summary>
        public bool ShowEntireFile
        {
            get => Preferences.Instance.UseFullTextDiff;
            set
            {
                if (value != Preferences.Instance.UseFullTextDiff)
                {
                    Preferences.Instance.UseFullTextDiff = value;
                    OnPropertyChanged();

                    if (Content is TextDiffContext ctx)
                        LoadContent();
                }
            }
        }

        /// <summary>
        ///     サイドバイサイド（左右分割）表示を使用するかどうか。
        /// </summary>
        public bool UseSideBySide
        {
            get => Preferences.Instance.UseSideBySideDiff;
            set
            {
                if (value != Preferences.Instance.UseSideBySideDiff)
                {
                    Preferences.Instance.UseSideBySideDiff = value;
                    OnPropertyChanged();

                    if (Content is TextDiffContext ctx && ctx.IsSideBySide() != value)
                        Content = ctx.SwitchMode();
                }
            }
        }

        /// <summary>
        ///     ファイルモード（パーミッション）の変更情報。
        /// </summary>
        public string FileModeChange
        {
            get => _fileModeChange;
            private set => SetProperty(ref _fileModeChange, value);
        }

        /// <summary>
        ///     テキスト差分かどうか（テキストツールバーの表示制御に使用）。
        /// </summary>
        public bool IsTextDiff
        {
            get => _isTextDiff;
            private set => SetProperty(ref _isTextDiff, value);
        }

        /// <summary>
        ///     差分の表示コンテンツ（TextDiffContext、ImageDiff、BinaryDiff等）。
        /// </summary>
        public object Content
        {
            get => _content;
            private set => SetProperty(ref _content, value);
        }

        /// <summary>
        ///     統合差分のコンテキスト行数。
        /// </summary>
        public int UnifiedLines
        {
            get => _unifiedLines;
            private set => SetProperty(ref _unifiedLines, value);
        }

        /// <summary>
        ///     コンストラクタ。リポジトリパス、差分オプション、前回のコンテキスト（キャッシュ）を指定する。
        /// </summary>
        public DiffContext(string repo, Models.DiffOption option, DiffContext previous = null)
        {
            _repo = repo;
            _option = option;

            if (previous != null)
            {
                _isTextDiff = previous._isTextDiff;
                _content = previous._content;
                _fileModeChange = previous._fileModeChange;
                _unifiedLines = previous._unifiedLines;
                _info = previous._info;
            }

            if (string.IsNullOrEmpty(_option.OrgPath) || _option.OrgPath == "/dev/null")
                Title = _option.Path;
            else
                Title = $"{_option.OrgPath} → {_option.Path}";

            LoadContent();
        }

        /// <summary>
        ///     コンテキスト行数を1増やして差分を再読み込みする。
        /// </summary>
        public void IncrUnified()
        {
            UnifiedLines = _unifiedLines + 1;
            LoadContent();
        }

        /// <summary>
        ///     コンテキスト行数を1減らして差分を再読み込みする（最小4行）。
        /// </summary>
        public void DecrUnified()
        {
            UnifiedLines = Math.Max(4, _unifiedLines - 1);
            LoadContent();
        }

        /// <summary>
        ///     外部マージツールで差分を開く。
        /// </summary>
        public void OpenExternalMergeTool()
        {
            new Commands.DiffTool(_repo, _option).Open();
        }

        /// <summary>
        ///     設定変更を検出し、必要に応じて差分コンテンツを再読み込みまたはモード切替する。
        /// </summary>
        public void CheckSettings()
        {
            if (Content is TextDiffContext ctx)
            {
                if ((ShowEntireFile && _info.UnifiedLines != _entireFileLine) ||
                    (!ShowEntireFile && _info.UnifiedLines == _entireFileLine) ||
                    (IgnoreWhitespace != _info.IgnoreWhitespace))
                {
                    LoadContent();
                    return;
                }

                if (ctx.IsSideBySide() != UseSideBySide)
                    Content = ctx.SwitchMode();
            }
        }

        /// <summary>
        ///     差分コンテンツを非同期で読み込む。
        ///     テキスト差分、バイナリ差分、画像差分、LFS差分、サブモジュール差分を判別して適切な表示オブジェクトを生成する。
        /// </summary>
        private void LoadContent()
        {
            // ディレクトリパスの場合は差分なし
            if (_option.Path.EndsWith('/'))
            {
                Content = null;
                IsTextDiff = false;
                return;
            }

            Task.Run(async () =>
            {
                var numLines = Preferences.Instance.UseFullTextDiff ? _entireFileLine : _unifiedLines;
                var ignoreWhitespace = Preferences.Instance.IgnoreWhitespaceChangesInDiff;

                var latest = await new Commands.Diff(_repo, _option, numLines, ignoreWhitespace)
                    .ReadAsync()
                    .ConfigureAwait(false);

                var info = new Info(_option, numLines, ignoreWhitespace, latest);
                if (_info != null && info.IsSame(_info))
                    return;

                _info = info;

                object rs = null;
                if (latest.TextDiff != null)
                {
                    var count = latest.TextDiff.Lines.Count;
                    var isSubmodule = false;
                    if (count <= 3)
                    {
                        var submoduleDiff = new Models.SubmoduleDiff();
                        var submoduleRoot = $"{_repo}/{_option.Path}".Replace('\\', '/').TrimEnd('/');
                        isSubmodule = true;
                        for (int i = 1; i < count; i++)
                        {
                            var line = latest.TextDiff.Lines[i];
                            if (!line.Content.StartsWith("Subproject commit ", StringComparison.Ordinal))
                            {
                                isSubmodule = false;
                                break;
                            }

                            var sha = line.Content.Substring(18);
                            if (line.Type == Models.TextDiffLineType.Added)
                                submoduleDiff.New = await QuerySubmoduleRevisionAsync(submoduleRoot, sha).ConfigureAwait(false);
                            else if (line.Type == Models.TextDiffLineType.Deleted)
                                submoduleDiff.Old = await QuerySubmoduleRevisionAsync(submoduleRoot, sha).ConfigureAwait(false);
                        }

                        if (isSubmodule)
                            rs = submoduleDiff;
                    }

                    if (!isSubmodule)
                        rs = latest.TextDiff;
                }
                else if (latest.IsBinary)
                {
                    var oldPath = string.IsNullOrEmpty(_option.OrgPath) ? _option.Path : _option.OrgPath;
                    var imgDecoder = ImageSource.GetDecoder(_option.Path);

                    if (imgDecoder != Models.ImageDecoder.None)
                    {
                        var imgDiff = new Models.ImageDiff();

                        if (_option.Revisions.Count == 2)
                        {
                            var oldImage = await ImageSource.FromRevisionAsync(_repo, _option.Revisions[0], oldPath, imgDecoder).ConfigureAwait(false);
                            var newImage = await ImageSource.FromRevisionAsync(_repo, _option.Revisions[1], _option.Path, imgDecoder).ConfigureAwait(false);
                            imgDiff.Old = oldImage.Bitmap;
                            imgDiff.OldFileSize = oldImage.Size;
                            imgDiff.New = newImage.Bitmap;
                            imgDiff.NewFileSize = newImage.Size;
                        }
                        else
                        {
                            if (!oldPath.Equals("/dev/null", StringComparison.Ordinal))
                            {
                                var oldImage = await ImageSource.FromRevisionAsync(_repo, "HEAD", oldPath, imgDecoder).ConfigureAwait(false);
                                imgDiff.Old = oldImage.Bitmap;
                                imgDiff.OldFileSize = oldImage.Size;
                            }

                            var fullPath = Path.Combine(_repo, _option.Path);
                            if (File.Exists(fullPath))
                            {
                                var newImage = await ImageSource.FromFileAsync(fullPath, imgDecoder).ConfigureAwait(false);
                                imgDiff.New = newImage.Bitmap;
                                imgDiff.NewFileSize = newImage.Size;
                            }
                        }

                        rs = imgDiff;
                    }
                    else
                    {
                        var binaryDiff = new Models.BinaryDiff();
                        if (_option.Revisions.Count == 2)
                        {
                            binaryDiff.OldSize = await new Commands.QueryFileSize(_repo, oldPath, _option.Revisions[0]).GetResultAsync().ConfigureAwait(false);
                            binaryDiff.NewSize = await new Commands.QueryFileSize(_repo, _option.Path, _option.Revisions[1]).GetResultAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            var fullPath = Path.Combine(_repo, _option.Path);
                            binaryDiff.OldSize = await new Commands.QueryFileSize(_repo, oldPath, "HEAD").GetResultAsync().ConfigureAwait(false);
                            binaryDiff.NewSize = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
                        }
                        rs = binaryDiff;
                    }
                }
                else if (latest.IsLFS)
                {
                    var imgDecoder = ImageSource.GetDecoder(_option.Path);
                    if (imgDecoder != Models.ImageDecoder.None)
                        rs = new LFSImageDiff(_repo, latest.LFSDiff, imgDecoder);
                    else
                        rs = latest.LFSDiff;
                }
                else
                {
                    rs = new Models.NoOrEOLChange();
                }

                Dispatcher.UIThread.Post(() =>
                {
                    FileModeChange = latest.FileModeChange;

                    if (rs is Models.TextDiff cur)
                    {
                        IsTextDiff = true;

                        if (Preferences.Instance.UseSideBySideDiff)
                            Content = new TwoSideTextDiff(_option, cur, _content as TextDiffContext);
                        else
                            Content = new CombinedTextDiff(_option, cur, _content as TextDiffContext);
                    }
                    else
                    {
                        IsTextDiff = false;
                        Content = rs;
                    }
                });
            });
        }

        /// <summary>
        ///     サブモジュールのリビジョン情報を非同期で取得する。
        /// </summary>
        private async Task<Models.RevisionSubmodule> QuerySubmoduleRevisionAsync(string repo, string sha)
        {
            var commit = await new Commands.QuerySingleCommit(repo, sha).GetResultAsync().ConfigureAwait(false);
            if (commit == null)
                return new Models.RevisionSubmodule() { Commit = new Models.Commit() { SHA = sha } };

            var body = await new Commands.QueryCommitFullMessage(repo, sha).GetResultAsync().ConfigureAwait(false);
            return new Models.RevisionSubmodule()
            {
                Commit = commit,
                FullMessage = new Models.CommitFullMessage { Message = body }
            };
        }

        /// <summary>
        ///     差分読み込みの状態情報。同一内容の再読み込みを防ぐキャッシュキーとして使用する。
        /// </summary>
        private class Info
        {
            public string Argument { get; }
            public int UnifiedLines { get; }
            public bool IgnoreWhitespace { get; }
            public string OldHash { get; }
            public string NewHash { get; }

            public Info(Models.DiffOption option, int unifiedLines, bool ignoreWhitespace, Models.DiffResult result)
            {
                Argument = option.ToString();
                UnifiedLines = unifiedLines;
                IgnoreWhitespace = ignoreWhitespace;
                OldHash = result.OldHash;
                NewHash = result.NewHash;
            }

            public bool IsSame(Info other)
            {
                return Argument.Equals(other.Argument, StringComparison.Ordinal) &&
                    UnifiedLines == other.UnifiedLines &&
                    IgnoreWhitespace == other.IgnoreWhitespace &&
                    OldHash.Equals(other.OldHash, StringComparison.Ordinal) &&
                    NewHash.Equals(other.NewHash, StringComparison.Ordinal);
            }
        }

        private readonly int _entireFileLine = 999999999;
        private readonly string _repo;
        private readonly Models.DiffOption _option = null;
        private string _fileModeChange = string.Empty;
        private int _unifiedLines = 4;
        private bool _isTextDiff = false;
        private object _content = null;
        private Info _info = null;
    }
}
