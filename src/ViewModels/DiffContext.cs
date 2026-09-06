// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// 差分表示のコンテキストを管理するViewModel。
/// テキスト差分、バイナリ差分、画像差分、LFS差分、サブモジュール差分など複数の形式に対応する。
/// </summary>
public class DiffContext : ObservableObject
{
    /// <summary>
    /// 差分のタイトル（ファイルパスまたはリネーム情報）。
    /// </summary>
    public string Title
    {
        get;
    }

    /// <summary>
    /// ファイルモード（パーミッション）の変更情報。
    /// </summary>
    public string FileModeChange
    {
        get => _fileModeChange;
        private set => SetProperty(ref _fileModeChange, value);
    }

    public string FileModeDescription
    {
        get => _fileModeDescription;
        private set => SetProperty(ref _fileModeDescription, value);
    }

    private static string DescribeFileMode(string mode) => App.Text(mode switch
    {
        "100644" => "FileModeChange.Normal",
        "100755" => "FileModeChange.Executable",
        "040000" or "40000" => "FileModeChange.Directory",
        "120000" => "FileModeChange.Symlink",
        "160000" => "FileModeChange.Submodule",
        _ => "FileModeChange.Unknown"
    });

    private string _fileModeDescription = string.Empty;

    /// <summary>
    /// テキスト差分かどうか（テキストツールバーの表示制御に使用）。
    /// </summary>
    public bool IsTextDiff
    {
        get => _isTextDiff;
        private set => SetProperty(ref _isTextDiff, value);
    }

    /// <summary>
    /// 空白無視トグルボタンを表示するかどうか（テキスト差分と「変更なし」表示のみ）。
    /// </summary>
    public bool IsIgnoreWhitespaceVisible
    {
        get => _isIgnoreWhitespaceVisible;
        private set => SetProperty(ref _isIgnoreWhitespaceVisible, value);
    }

    /// <summary>
    /// 差分の表示コンテンツ（TextDiffContext、ImageDiff、BinaryDiff等）。
    /// </summary>
    public object Content
    {
        get => _content;
        private set => SetProperty(ref _content, value);
    }

    /// <summary>
    /// 統合差分のコンテキスト行数。
    /// </summary>
    public int UnifiedLines
    {
        get => _unifiedLines;
        private set => SetProperty(ref _unifiedLines, value);
    }

    /// <summary>
    /// コンストラクタ。リポジトリパス、差分オプション、前回のコンテキスト（キャッシュ）を指定する。
    /// </summary>
    public DiffContext(string repo, Models.DiffOption option, DiffContext previous = null)
    {
        _repo = repo;
        _option = option;

        if (previous is not null)
        {
            _isTextDiff = previous._isTextDiff;
            _isIgnoreWhitespaceVisible = previous._isIgnoreWhitespaceVisible;
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
    /// コンテキスト行数を1増やして差分を再読み込みする。
    /// </summary>
    public void IncrUnified()
    {
        UnifiedLines = _unifiedLines + 1;
        LoadContent();
    }

    /// <summary>
    /// コンテキスト行数を1減らして差分を再読み込みする（最小4行）。
    /// </summary>
    public void DecrUnified()
    {
        UnifiedLines = Math.Max(4, _unifiedLines - 1);
        LoadContent();
    }

    /// <summary>
    /// 外部マージツールで差分を開く。
    /// </summary>
    public void OpenExternalMergeTool()
    {
        new Commands.DiffTool(_repo, _option).Open();
    }

    /// <summary>
    /// 設定変更を検出し、必要に応じて差分コンテンツを再読み込みまたはモード切替する。
    /// `Preferences.Instance`を直接参照することで、複数のDiffビューアが開いている場合でも
    /// 各ビューの`ToggleButton`（Show All Lines / Ignore Whitespace / Side-by-Side）操作を
    /// 全ビューアに同期させる。
    /// </summary>
    public void CheckSettings()
    {
        var pref = Preferences.Instance;
        var numLines = pref.UseFullTextDiff ? _entireFileLines : _unifiedLines;
        // 読み込み中に設定を元に戻す操作も、新しい要求として扱う。
        if (numLines != _requestedNumLines ||
            pref.IgnoreWhitespaceChangesInDiff != _requestedIgnoreWhitespace ||
            pref.IgnoreCRAtEOLInDiff != _requestedIgnoreCRAtEOL)
        {
            LoadContent();
            return;
        }

        if (Content is TextDiffContext ctx && ctx.IsSideBySide() != pref.UseSideBySideDiff)
            Content = ctx.SwitchMode();
    }

    /// <summary>
    /// 差分コンテンツを非同期で読み込む。
    /// テキスト差分、バイナリ差分、画像差分、LFS差分、サブモジュール差分を判別して適切な表示オブジェクトを生成する。
    /// </summary>
    private void LoadContent()
    {
        var request = BeginLoadRequest();
        // 上流との差分: 設定を要求時に固定し、完了順ではなく要求順で表示する。
        var numLines = _requestedNumLines = Preferences.Instance.UseFullTextDiff ? _entireFileLines : _unifiedLines;
        var ignoreWhitespace = _requestedIgnoreWhitespace = Preferences.Instance.IgnoreWhitespaceChangesInDiff;
        var ignoreCRAtEOL = _requestedIgnoreCRAtEOL = Preferences.Instance.IgnoreCRAtEOLInDiff;
        // ディレクトリパスの場合は差分なし
        if (_option.Path.EndsWith('/'))
        {
            FileModeChange = string.Empty;
            IsTextDiff = false;
            IsIgnoreWhitespaceVisible = false;
            Content = null;
            _info = null;
            return;
        }

        var previousInfo = _info;
        Task.Run(async () =>
        {

            var latest = await new Commands.Diff(_repo, _option, numLines, ignoreWhitespace, ignoreCRAtEOL)
                .ReadAsync()
                .ConfigureAwait(false);

            var info = new Info(_option, numLines, ignoreWhitespace, latest, ignoreCRAtEOL);
            if (previousInfo is not null && info.IsSame(previousInfo))
                return;
            object rs = null;
            if (latest.TextDiff is not null)
            {
                var count = latest.TextDiff.Lines.Count;
                var isSubmodule = false;
                if (count is > 1 and <= 3 && (latest.OldMode == "160000" || latest.NewMode == "160000"))
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

                        var sha = line.Content[18..];
                        if (line.Type == Models.TextDiffLineType.Added)
                            submoduleDiff.New = await new Commands.QuerySubmoduleRevision(submoduleRoot, sha).GetResultAsync().ConfigureAwait(false);
                        else if (line.Type == Models.TextDiffLineType.Deleted)
                            submoduleDiff.Old = await new Commands.QuerySubmoduleRevision(submoduleRoot, sha).GetResultAsync().ConfigureAwait(false);
                    }

                    if (isSubmodule)
                    {
                        submoduleDiff.FullPath = submoduleRoot;
                        rs = submoduleDiff;
                    }
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
                    var fullPath = Path.Combine(_repo, _option.Path);
                    var oldRevision = _option.Revisions.Count == 2 ? _option.Revisions[0] : "HEAD";
                    if (oldPath != "/dev/null")
                    {
                        var oldImage = oldRevision == "-R"
                            ? await ImageSource.FromFileAsync(fullPath, imgDecoder).ConfigureAwait(false)
                            : await ImageSource.FromRevisionAsync(_repo, oldRevision, oldPath, imgDecoder).ConfigureAwait(false);
                        imgDiff.Old = oldImage.Bitmap;
                        imgDiff.OldFileSize = oldImage.Size;
                    }
                    var fromWorktree = _option.Revisions.Count == 2 ? string.IsNullOrEmpty(_option.Revisions[1]) : _option.IsUnstaged;
                    var newImage = fromWorktree
                        ? await ImageSource.FromFileAsync(fullPath, imgDecoder).ConfigureAwait(false)
                        : await ImageSource.FromRevisionAsync(_repo, _option.Revisions.Count == 2 ? _option.Revisions[1] : string.Empty, _option.Path, imgDecoder).ConfigureAwait(false);
                    imgDiff.New = newImage.Bitmap;
                    imgDiff.NewFileSize = newImage.Size;

                    rs = imgDiff;
                }
                else
                {
                    var binaryDiff = new Models.BinaryDiff { Repository = _repo, FilePath = _option.Path };
                    var fullPath = Path.Combine(_repo, _option.Path);
                    var newRevision = _option.Revisions.Count == 2 ? _option.Revisions[1] : (_option.IsUnstaged ? null : string.Empty);
                    var oldRevision = _option.Revisions.Count == 2 ? _option.Revisions[0] : "HEAD";
                    if (oldRevision == "-R")
                        binaryDiff.OldSize = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
                    else if (oldPath != "/dev/null")
                        binaryDiff.OldSize = await new Commands.QueryFileSize(_repo, oldPath, oldRevision).GetResultAsync().ConfigureAwait(false);

                    // 空のリビジョンはステージ済みの index、null は作業ツリーを表す。
                    if (_option.Revisions.Count == 2 && string.IsNullOrEmpty(newRevision))
                        newRevision = null;
                    binaryDiff.NewRevision = newRevision;
                    binaryDiff.NewSize = newRevision is null
                        ? (File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0)
                        : await new Commands.QueryFileSize(_repo, _option.Path, newRevision).GetResultAsync().ConfigureAwait(false);
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
            else if (IsEmptyFileHash(latest.OldHash) || IsEmptyFileHash(latest.NewHash))
            {
                rs = new Models.EmptyFile();
            }
            else
            {
                rs = new Models.NoOrEOLChange();
            }

            Dispatcher.UIThread.Post(() => ApplyLoadedContent(request, info, latest, rs));
        });
    }

    internal long BeginLoadRequest() => ++_loadRequest;

    internal void ApplyLoadedContent(long request, Info info, Models.DiffResult latest, object rs)
    {
        if (request != _loadRequest || (_info is not null && info.IsSame(_info)))
        {
            // 表示に渡さなかった画像だけはここで解放する。
            if (rs is Models.ImageDiff image)
            {
                image.Old?.Dispose();
                image.New?.Dispose();
            }
            return;
        }

        _info = info;
        FileModeChange = latest.FileModeChange;
        FileModeDescription = string.IsNullOrEmpty(latest.OldMode)
            ? App.Text("FileModeChange.New") + DescribeFileMode(latest.NewMode)
            : string.IsNullOrEmpty(latest.NewMode)
                ? App.Text("FileModeChange.Deleted") + DescribeFileMode(latest.OldMode)
                : App.Text("FileModeChange") + DescribeFileMode(latest.OldMode) + " → " + DescribeFileMode(latest.NewMode);

        if (rs is Models.TextDiff cur)
        {
            IsTextDiff = true;
            IsIgnoreWhitespaceVisible = true;

            if (Preferences.Instance.UseSideBySideDiff)
                Content = new TwoSideTextDiff(_option, cur, _content as TextDiffContext);
            else
                Content = new CombinedTextDiff(_option, cur, _content as TextDiffContext);
        }
        else
        {
            IsTextDiff = false;
            IsIgnoreWhitespaceVisible = rs is Models.NoOrEOLChange;
            Content = rs;
        }
    }

    /// <summary>
    /// 指定ハッシュが空blobの既知ハッシュ（SHA-1 / SHA-256）と一致するか判定する。
    /// </summary>
    /// <param name="hash">判定対象のハッシュ</param>
    /// <returns>空blobのハッシュならtrue</returns>
    private static bool IsEmptyFileHash(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return false;

        if (hash.Length == 40)
            return hash.Equals(Models.EmptyFile.SHA1, StringComparison.Ordinal);

        if (hash.Length == 64)
            return hash.Equals(Models.EmptyFile.SHA256, StringComparison.Ordinal);

        return false;
    }

    /// <summary>
    /// 差分読み込みの状態情報。同一内容の再読み込みを防ぐキャッシュキーとして使用する。
    /// </summary>
    internal class Info
    {
        public string Argument { get; }
        public int UnifiedLines { get; }
        public bool IgnoreWhitespace { get; }
        public bool IgnoreCRAtEOL { get; }
        public string OldHash { get; }
        public string NewHash { get; }

        public Info(Models.DiffOption option, int unifiedLines, bool ignoreWhitespace, Models.DiffResult result, bool ignoreCRAtEOL = false)
        {
            Argument = option.ToString();
            UnifiedLines = unifiedLines;
            IgnoreWhitespace = ignoreWhitespace;
            IgnoreCRAtEOL = ignoreCRAtEOL;
            OldHash = result.OldHash;
            NewHash = result.NewHash;
        }

        public bool IsSame(Info other)
        {
            return Argument.Equals(other.Argument, StringComparison.Ordinal) &&
                UnifiedLines == other.UnifiedLines &&
                IgnoreWhitespace == other.IgnoreWhitespace &&
                IgnoreCRAtEOL == other.IgnoreCRAtEOL &&
                OldHash.Equals(other.OldHash, StringComparison.Ordinal) &&
                NewHash.Equals(other.NewHash, StringComparison.Ordinal);
        }
    }

    private readonly int _entireFileLines = 999999999;
    private readonly string _repo;
    private readonly Models.DiffOption _option = null;
    private string _fileModeChange = string.Empty;
    private int _unifiedLines = 4;
    private bool _isTextDiff = false;
    private bool _isIgnoreWhitespaceVisible = true;
    private object _content = null;
    private Info _info = null;
    private long _loadRequest;
    private int _requestedNumLines;
    private bool _requestedIgnoreWhitespace;
    private bool _requestedIgnoreCRAtEOL;
}
