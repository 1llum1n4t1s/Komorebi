using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    /// 三方マージのコンフリクトエディタViewModel。
    /// コンフリクトマーカーを解析し、Ours/Theirs/結果パネルを管理する。
    /// ユーザーがコンフリクトの解決方法を選択し、保存・ステージングを行う。
    /// </summary>
    public class MergeConflictEditor : ObservableObject
    {
        /// <summary>
        /// コンフリクトファイルのリポジトリ内相対パス。
        /// </summary>
        public string FilePath
        {
            get => _filePath;
        }

        /// <summary>
        /// 自分側（Mine）のコミットまたはブランチ情報。
        /// 操作の種類（マージ、チェリーピック、リベース等）に応じて異なるオブジェクトが設定される。
        /// </summary>
        public object Mine
        {
            get;
        }

        /// <summary>
        /// 相手側（Theirs）のコミットまたはブランチ情報。
        /// </summary>
        public object Theirs
        {
            get;
        }

        /// <summary>
        /// エラーメッセージ。バイナリファイルや保存失敗時に設定される。
        /// </summary>
        public string Error
        {
            get => _error;
            private set => SetProperty(ref _error, value);
        }

        /// <summary>
        /// 自分側（Ours）の行データリスト。左パネルに表示される。
        /// </summary>
        public List<Models.ConflictLine> OursLines
        {
            get => _oursLines;
            private set => SetProperty(ref _oursLines, value);
        }

        /// <summary>
        /// 相手側（Theirs）の行データリスト。右パネルに表示される。
        /// </summary>
        public List<Models.ConflictLine> TheirsLines
        {
            get => _theirsLines;
            private set => SetProperty(ref _theirsLines, value);
        }

        /// <summary>
        /// 解決結果の行データリスト。下部パネルに表示される。
        /// </summary>
        public List<Models.ConflictLine> ResultLines
        {
            get => _resultLines;
            private set => SetProperty(ref _resultLines, value);
        }

        /// <summary>
        /// Ours/Theirsの最大行番号。行番号表示幅の計算に使用する。
        /// </summary>
        public int MaxLineNumber
        {
            get => _maxLineNumber;
            private set => SetProperty(ref _maxLineNumber, value);
        }

        /// <summary>
        /// 未解決のコンフリクト数。
        /// </summary>
        public int UnsolvedCount
        {
            get => _unsolvedCount;
            private set => SetProperty(ref _unsolvedCount, value);
        }

        /// <summary>
        /// スクロールオフセット。3つのパネルのスクロール同期に使用する。
        /// </summary>
        public Vector ScrollOffset
        {
            get => _scrollOffset;
            set => SetProperty(ref _scrollOffset, value);
        }

        /// <summary>
        /// 現在選択中のコンフリクトチャンク。
        /// </summary>
        public Models.ConflictSelectedChunk SelectedChunk
        {
            get => _selectedChunk;
            set => SetProperty(ref _selectedChunk, value);
        }

        /// <summary>
        /// コンフリクト領域の読み取り専用リスト。
        /// </summary>
        public IReadOnlyList<Models.ConflictRegion> ConflictRegions
        {
            get => _conflictRegions;
        }

        /// <summary>
        /// リポジトリ、HEADコミット、ファイルパスを指定してコンフリクトエディタを初期化する。
        /// 進行中の操作（マージ、チェリーピック、リベース等）からMine/Theirsを判定し、
        /// ファイル内容を読み込んでコンフリクトマーカーを解析する。
        /// </summary>
        public MergeConflictEditor(Repository repo, Models.Commit head, string filePath)
        {
            _repo = repo;
            _filePath = filePath;

            // 進行中の操作の種類に応じてMine/Theirsを決定
            (Mine, Theirs) = repo.InProgressContext switch
            {
                CherryPickInProgress cherryPick => (head, cherryPick.Head),
                RebaseInProgress rebase => (rebase.Onto, rebase.StoppedAt),
                RevertInProgress revert => (head, revert.Head),
                MergeInProgress merge => (head, merge.Source),
                _ => (head, (object)"Stash or Patch"),
            };

            // ワーキングコピーからファイル内容を読み込む
            var workingCopyPath = Path.Combine(_repo.FullPath, _filePath);
            var workingCopyContent = string.Empty;
            if (File.Exists(workingCopyPath))
                workingCopyContent = File.ReadAllText(workingCopyPath);

            // バイナリファイルはサポート外
            if (workingCopyContent.IndexOf('\0', StringComparison.Ordinal) >= 0)
            {
                _error = "Binary file is not supported.";
                return;
            }

            // コンフリクトマーカーを解析し、表示データを構築
            ParseOriginalContent(workingCopyContent);
            RefreshDisplayData();
        }

        /// <summary>
        /// 指定行のコンフリクト状態を取得する。
        /// </summary>
        public Models.ConflictLineState GetLineState(int line)
        {
            if (line >= 0 && line < _lineStates.Count)
                return _lineStates[line];
            return Models.ConflictLineState.Normal;
        }

        /// <summary>
        /// 選択中のコンフリクトチャンクに対して指定された解決方法を適用する。
        /// 既に解決済みの領域への再解決や、未解決領域への取り消しは無視する。
        /// </summary>
        public void Resolve(object param)
        {
            if (_selectedChunk == null)
                return;

            var region = _conflictRegions[_selectedChunk.ConflictIndex];
            if (param is not Models.ConflictResolution resolution)
                return;

            // 既に解決済みの領域に対する解決操作はスキップ
            if (resolution != Models.ConflictResolution.None && region.IsResolved)
                return;

            // 未解決の領域に対する取り消し操作はスキップ
            if (resolution == Models.ConflictResolution.None && !region.IsResolved)
                return;

            // 解決状態と解決方法を設定し、表示データを更新
            region.IsResolved = resolution != Models.ConflictResolution.None;
            region.ResolutionType = resolution;
            RefreshDisplayData();
        }

        /// <summary>
        /// 解決済みコンテンツをファイルに書き込み、git addでステージングする。
        /// 未解決のコンフリクトが残っている場合はエラーを返す。
        /// </summary>
        public async Task<bool> SaveAndStageAsync()
        {
            if (_conflictRegions.Count == 0)
                return true;

            // 未解決コンフリクトが残っている場合は保存不可
            if (_unsolvedCount > 0)
            {
                Error = "Cannot save: there are still unresolved conflicts.";
                return false;
            }

            // 元のコンテンツを行分割し、解決結果を組み立てる
            var lines = _originalContent.Split('\n', StringSplitOptions.None);
            var builder = new StringBuilder();
            var lastLineIdx = 0;

            foreach (var r in _conflictRegions)
            {
                // コンフリクト領域の前の通常行を追加
                for (var i = lastLineIdx; i < r.StartLineInOriginal; i++)
                    builder.Append(lines[i]).Append('\n');

                // 解決方法に応じたコンテンツを追加
                if (r.ResolutionType == Models.ConflictResolution.UseOurs)
                {
                    foreach (var l in r.OursContent)
                        builder.Append(l).Append('\n');
                }
                else if (r.ResolutionType == Models.ConflictResolution.UseTheirs)
                {
                    foreach (var l in r.TheirsContent)
                        builder.Append(l).Append('\n');
                }
                else if (r.ResolutionType == Models.ConflictResolution.UseBothMineFirst)
                {
                    foreach (var l in r.OursContent)
                        builder.Append(l).Append('\n');

                    foreach (var l in r.TheirsContent)
                        builder.Append(l).Append('\n');
                }
                else if (r.ResolutionType == Models.ConflictResolution.UseBothTheirsFirst)
                {
                    foreach (var l in r.TheirsContent)
                        builder.Append(l).Append('\n');

                    foreach (var l in r.OursContent)
                        builder.Append(l).Append('\n');
                }

                lastLineIdx = r.EndLineInOriginal + 1;
            }

            // 最後のコンフリクト領域以降の通常行を追加
            for (var j = lastLineIdx; j < lines.Length; j++)
                builder.Append(lines[j]).Append('\n');

            try
            {
                // マージ結果をファイルに書き込む
                var fullPath = Path.Combine(_repo.FullPath, _filePath);
                await File.WriteAllTextAsync(fullPath, builder.ToString());

                // 一時ファイル経由でgit addを実行しステージングする
                var pathSpecFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(pathSpecFile, _filePath);
                await new Commands.Add(_repo.FullPath, pathSpecFile).ExecAsync();
                File.Delete(pathSpecFile);

                // ワーキングコピーの変更を手動で通知
                _repo.MarkWorkingCopyDirtyManually();
                return true;
            }
            catch (Exception ex)
            {
                Error = $"Failed to save and stage: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// エラーメッセージをクリアする。
        /// </summary>
        public void ClearErrorMessage()
        {
            Error = string.Empty;
        }

        /// <summary>
        /// ファイル内容を解析し、コンフリクトマーカー（&lt;&lt;&lt;&lt;&lt;&lt;&lt;, =======, &gt;&gt;&gt;&gt;&gt;&gt;&gt;）を
        /// 検出してコンフリクト領域リストとOurs/Theirsの行データを構築する。
        /// diff3形式のベースセクション（|||||||）にも対応する。
        /// </summary>
        private void ParseOriginalContent(string content)
        {
            _originalContent = content;
            _conflictRegions.Clear();

            if (string.IsNullOrEmpty(content))
                return;

            var lines = content.Split('\n', StringSplitOptions.None);
            var oursLines = new List<Models.ConflictLine>();
            var theirsLines = new List<Models.ConflictLine>();
            int oursLineNumber = 1;
            int theirsLineNumber = 1;
            int i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                // コンフリクト開始マーカーを検出
                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    var region = new Models.ConflictRegion
                    {
                        StartLineInOriginal = i,
                        StartMarker = line,
                    };

                    oursLines.Add(new());
                    theirsLines.Add(new());
                    i++;

                    // Collect ours content
                    while (i < lines.Length &&
                           !lines[i].StartsWith("|||||||", StringComparison.Ordinal) &&
                           !lines[i].StartsWith("=======", StringComparison.Ordinal))
                    {
                        line = lines[i];
                        region.OursContent.Add(line);
                        oursLines.Add(new(Models.ConflictLineType.Ours, line, oursLineNumber++));
                        theirsLines.Add(new());
                        i++;
                    }

                    // Skip diff3 base section if present
                    if (i < lines.Length && lines[i].StartsWith("|||||||", StringComparison.Ordinal))
                    {
                        i++;
                        while (i < lines.Length && !lines[i].StartsWith("=======", StringComparison.Ordinal))
                            i++;
                    }

                    // Capture separator marker
                    if (i < lines.Length && lines[i].StartsWith("=======", StringComparison.Ordinal))
                    {
                        oursLines.Add(new());
                        theirsLines.Add(new());
                        region.SeparatorMarker = lines[i];
                        i++;
                    }

                    // Collect theirs content
                    while (i < lines.Length && !lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        line = lines[i];
                        region.TheirsContent.Add(line);
                        oursLines.Add(new());
                        theirsLines.Add(new(Models.ConflictLineType.Theirs, line, theirsLineNumber++));
                        i++;
                    }

                    // Capture end marker (e.g., ">>>>>>> feature-branch")
                    if (i < lines.Length && lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        oursLines.Add(new());
                        theirsLines.Add(new());

                        region.EndMarker = lines[i];
                        region.EndLineInOriginal = i;
                        i++;
                    }

                    _conflictRegions.Add(region);
                }
                else
                {
                    oursLines.Add(new(Models.ConflictLineType.Common, line, oursLineNumber));
                    theirsLines.Add(new(Models.ConflictLineType.Common, line, theirsLineNumber));
                    i++;
                    oursLineNumber++;
                    theirsLineNumber++;
                }
            }

            MaxLineNumber = Math.Max(oursLineNumber, theirsLineNumber);
            OursLines = oursLines;
            TheirsLines = theirsLines;
        }

        /// <summary>
        /// コンフリクト領域の解決状態に基づいて結果パネルの表示データを再構築する。
        /// 解決済み/未解決のブロックを適切な行タイプで表示し、未解決数を更新する。
        /// </summary>
        private void RefreshDisplayData()
        {
            var resultLines = new List<Models.ConflictLine>();
            _lineStates.Clear();

            if (_oursLines == null || _oursLines.Count == 0)
            {
                ResultLines = resultLines;
                return;
            }

            int resultLineNumber = 1;
            int currentLine = 0;
            int conflictIdx = 0;

            while (currentLine < _oursLines.Count)
            {
                // Check if we're at a conflict region
                Models.ConflictRegion currentRegion = null;
                if (conflictIdx < _conflictRegions.Count)
                {
                    var region = _conflictRegions[conflictIdx];
                    if (region.StartLineInOriginal == currentLine)
                        currentRegion = region;
                }

                if (currentRegion != null)
                {
                    int regionLines = currentRegion.EndLineInOriginal - currentRegion.StartLineInOriginal + 1;
                    if (currentRegion.IsResolved)
                    {
                        var oldLineCount = resultLines.Count;
                        var resolveType = currentRegion.ResolutionType;

                        // Resolved - show resolved content with color based on resolution type
                        if (resolveType == Models.ConflictResolution.UseBothMineFirst)
                        {
                            int mineCount = currentRegion.OursContent.Count;
                            for (int i = 0; i < mineCount; i++)
                            {
                                resultLines.Add(new(Models.ConflictLineType.Ours, currentRegion.OursContent[i], resultLineNumber));
                                resultLineNumber++;
                            }

                            int theirsCount = currentRegion.TheirsContent.Count;
                            for (int i = 0; i < theirsCount; i++)
                            {
                                resultLines.Add(new(Models.ConflictLineType.Theirs, currentRegion.TheirsContent[i], resultLineNumber));
                                resultLineNumber++;
                            }
                        }
                        else if (resolveType == Models.ConflictResolution.UseBothTheirsFirst)
                        {
                            int theirsCount = currentRegion.TheirsContent.Count;
                            for (int i = 0; i < theirsCount; i++)
                            {
                                resultLines.Add(new(Models.ConflictLineType.Theirs, currentRegion.TheirsContent[i], resultLineNumber));
                                resultLineNumber++;
                            }

                            int mineCount = currentRegion.OursContent.Count;
                            for (int i = 0; i < mineCount; i++)
                            {
                                resultLines.Add(new(Models.ConflictLineType.Ours, currentRegion.OursContent[i], resultLineNumber));
                                resultLineNumber++;
                            }
                        }
                        else if (resolveType == Models.ConflictResolution.UseOurs)
                        {
                            int mineCount = currentRegion.OursContent.Count;
                            for (int i = 0; i < mineCount; i++)
                            {
                                resultLines.Add(new(Models.ConflictLineType.Ours, currentRegion.OursContent[i], resultLineNumber));
                                resultLineNumber++;
                            }
                        }
                        else if (resolveType == Models.ConflictResolution.UseTheirs)
                        {
                            int theirsCount = currentRegion.TheirsContent.Count;
                            for (int i = 0; i < theirsCount; i++)
                            {
                                resultLines.Add(new(Models.ConflictLineType.Theirs, currentRegion.TheirsContent[i], resultLineNumber));
                                resultLineNumber++;
                            }
                        }

                        // Pad with empty lines to match Mine/Theirs panel height
                        int added = resultLines.Count - oldLineCount;
                        int padding = regionLines - added;
                        for (int p = 0; p < padding; p++)
                            resultLines.Add(new());

                        int blockSize = resultLines.Count - oldLineCount - 2;
                        _lineStates.Add(Models.ConflictLineState.ResolvedBlockStart);
                        for (var i = 0; i < blockSize; i++)
                            _lineStates.Add(Models.ConflictLineState.ResolvedBlock);
                        _lineStates.Add(Models.ConflictLineState.ResolvedBlockEnd);
                    }
                    else
                    {
                        resultLines.Add(new(Models.ConflictLineType.Marker, currentRegion.StartMarker));
                        _lineStates.Add(Models.ConflictLineState.ConflictBlockStart);

                        foreach (var line in currentRegion.OursContent)
                        {
                            resultLines.Add(new(Models.ConflictLineType.Ours, line, resultLineNumber++));
                            _lineStates.Add(Models.ConflictLineState.ConflictBlock);
                        }

                        resultLines.Add(new(Models.ConflictLineType.Marker, currentRegion.SeparatorMarker));
                        _lineStates.Add(Models.ConflictLineState.ConflictBlock);

                        foreach (var line in currentRegion.TheirsContent)
                        {
                            resultLines.Add(new(Models.ConflictLineType.Theirs, line, resultLineNumber++));
                            _lineStates.Add(Models.ConflictLineState.ConflictBlock);
                        }

                        resultLines.Add(new(Models.ConflictLineType.Marker, currentRegion.EndMarker));
                        _lineStates.Add(Models.ConflictLineState.ConflictBlockEnd);
                    }

                    currentLine = currentRegion.EndLineInOriginal + 1;
                    conflictIdx++;
                }
                else
                {
                    var oursLine = _oursLines[currentLine];
                    resultLines.Add(new(oursLine.Type, oursLine.Content, resultLineNumber));
                    _lineStates.Add(Models.ConflictLineState.Normal);
                    resultLineNumber++;
                    currentLine++;
                }
            }

            SelectedChunk = null;
            ResultLines = resultLines;

            var unsolved = new List<int>();
            for (var i = 0; i < _conflictRegions.Count; i++)
            {
                var r = _conflictRegions[i];
                if (!r.IsResolved)
                    unsolved.Add(i);
            }

            UnsolvedCount = unsolved.Count;
        }

        /// <summary>対象リポジトリ</summary>
        private readonly Repository _repo;
        /// <summary>コンフリクトファイルの相対パス</summary>
        private readonly string _filePath;
        /// <summary>元のファイル内容（コンフリクトマーカー含む）</summary>
        private string _originalContent = string.Empty;
        /// <summary>未解決コンフリクト数</summary>
        private int _unsolvedCount = 0;
        /// <summary>最大行番号</summary>
        private int _maxLineNumber = 0;
        /// <summary>自分側の行データ</summary>
        private List<Models.ConflictLine> _oursLines = [];
        /// <summary>相手側の行データ</summary>
        private List<Models.ConflictLine> _theirsLines = [];
        /// <summary>結果パネルの行データ</summary>
        private List<Models.ConflictLine> _resultLines = [];
        /// <summary>コンフリクト領域のリスト</summary>
        private List<Models.ConflictRegion> _conflictRegions = [];
        /// <summary>各行のコンフリクト状態</summary>
        private List<Models.ConflictLineState> _lineStates = [];
        /// <summary>スクロールオフセット</summary>
        private Vector _scrollOffset = Vector.Zero;
        /// <summary>選択中のコンフリクトチャンク</summary>
        private Models.ConflictSelectedChunk _selectedChunk;
        /// <summary>エラーメッセージ</summary>
        private string _error = string.Empty;
    }
}
