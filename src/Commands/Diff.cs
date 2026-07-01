using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// git diffコマンドを実行し、テキスト差分・バイナリ差分・LFS差分を解析するクラス。
/// </summary>
public partial class Diff : Command
{
    /// <summary>
    /// 差分のハンクヘッダー（@@ -old,count +new,count @@）を解析する正規表現。
    /// </summary>
    [GeneratedRegex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@")]
    private static partial Regex REG_INDICATOR();

    /// <summary>
    /// indexヘッダー（旧ハッシュ..新ハッシュ）を解析する正規表現。
    /// </summary>
    [GeneratedRegex(@"^index\s([0-9a-f]{6,64})\.\.([0-9a-f]{6,64})(\s[1-9]{6})?")]
    private static partial Regex REG_HASH_CHANGE();

    /// <summary>LFSポインタファイルの識別子</summary>
    private const string LFS_SPECIFIER = "version https://git-lfs.github.com/spec/";
    /// <summary>LFSのoid行プレフィックス（行頭文字を除く）</summary>
    private const string LFS_OID_PREFIX = "oid sha256:";
    /// <summary>LFSのsize行プレフィックス（行頭文字を除く）</summary>
    private const string LFS_SIZE_PREFIX = "size ";

    /// <summary>
    /// Diffコマンドのコンストラクタ。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="opt">差分オプション（比較対象の指定）</param>
    /// <param name="unified">コンテキスト行数</param>
    /// <param name="ignoreWhitespace">空白の変更を無視するかどうか</param>
    public Diff(string repo, Models.DiffOption opt, int unified, bool ignoreWhitespace)
    {
        WorkingDirectory = repo;
        Context = repo;

        var builder = new StringBuilder(256);
        // 色なし・外部diffツールなし・完全長ハッシュ・パッチ形式で出力
        builder.Append("diff --no-color --no-ext-diff --full-index --patch ");
        if (Models.DiffOption.IgnoreCRAtEOL)
            builder.Append("--ignore-cr-at-eol ");
        if (ignoreWhitespace)
            builder.Append("--ignore-space-change --ignore-blank-lines ");
        builder.Append("--unified=").Append(unified).Append(' ');
        builder.Append(opt.ToString());

        Args = builder.ToString();
    }

    /// <summary>
    /// diffコマンドを非同期で実行し、差分結果を返す。
    /// </summary>
    /// <returns>差分解析結果</returns>
    public async Task<Models.DiffResult> ReadAsync()
    {
        var result = new Models.DiffResult() { TextDiff = new Models.TextDiff() };

        try
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);
            proc.Start();

            var parser = new DiffParser();
            var stderrDrain = DrainReaderAsync(proc.StandardError);
            while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
                parser.Parse(line);

            result = parser.Complete();

            await proc.WaitForExitAsync().ConfigureAwait(false);
            await stderrDrain.ConfigureAwait(false);
        }
        catch
        {
            // 例外は無視する
        }

        return result;
    }

    /// <summary>
    /// diff出力文字列を解析して、DiffResultモデルに変換する。
    /// </summary>
    /// <param name="text">git diffの標準出力</param>
    /// <returns>解析された差分結果</returns>
    internal static Models.DiffResult ParseDiffOutput(string text)
    {
        var parser = new DiffParser();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
            parser.Parse(line);
        return parser.Complete();
    }

    /// <summary>
    /// diff出力を1行ずつ受け取って結果モデルを組み立てる状態オブジェクト。
    /// type-changedファイル（例: 通常ファイル→シンボリックリンク）では「削除+新規」の
    /// 2セットのdiffヘッダーが同一出力内に現れるため、チャンク内外を _isInChunk で追跡し
    /// ヘッダー行をどの位置でも解析できるようにしている（upstream a30cf17a 相当）。
    /// </summary>
    private sealed class DiffParser
    {
        /// <summary>
        /// diff出力の1行を解析し、結果モデルに追加する。
        /// </summary>
        /// <param name="line">diff出力の1行</param>
        public void Parse(string line)
        {
            if (_result.IsBinary)
                return;

            if (line.Length == 0)
            {
                // チャンク内の空行は空のコンテキスト行として扱う（チャンク外は無視）
                if (_isInChunk)
                {
                    FlushInlineHighlights();
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Normal, "", _oldLine, _newLine);
                    _result.TextDiff.Lines.Add(_last);
                    _oldLine++;
                    _newLine++;
                }
                return;
            }

            if (ParseChunkStartLine(line))
                return;

            if (ParseChunkBodyLine(line[0], line[1..]))
                return;

            ParseDiffHeaderLine(line);
        }

        /// <summary>
        /// 解析を完了し、結果モデルを確定して返す。
        /// </summary>
        /// <returns>解析された差分結果</returns>
        public Models.DiffResult Complete()
        {
            if (_result.IsBinary || _result.IsLFS || _result.TextDiff.Lines.Count == 0)
            {
                _result.TextDiff = null;
            }
            else
            {
                if (_isInChunk)
                {
                    FlushInlineHighlights();
                    _isInChunk = false;
                }

                _result.TextDiff.MaxLineNumber = Math.Max(_newLine, _oldLine);
            }

            return _result;
        }

        /// <summary>
        /// チャンク開始行（@@ -old,count +new,count @@）を解析する。
        /// </summary>
        /// <param name="line">diff出力の1行</param>
        /// <returns>チャンク開始行として処理した場合はtrue</returns>
        private bool ParseChunkStartLine(string line)
        {
            if (line[0] != '@')
                return false;

            if (_isInChunk)
            {
                FlushInlineHighlights();
                _isInChunk = false;
            }

            var match = REG_INDICATOR().Match(line);
            if (!match.Success)
                return false;

            _oldLine = int.Parse(match.Groups[1].Value);
            _newLine = int.Parse(match.Groups[2].Value);
            _last = new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, 0, 0);
            _result.TextDiff.Lines.Add(_last);

            _isInChunk = true;
            return true;
        }

        /// <summary>
        /// チャンク本体行（追加/削除/コンテキスト/特殊行）を解析する。
        /// チャンク本体行でない場合はチャンクを終了してfalseを返す（ヘッダー解析へフォールバック）。
        /// </summary>
        /// <param name="ch">行頭の1文字</param>
        /// <param name="content">行頭を除いた内容</param>
        /// <returns>チャンク本体行として処理した場合はtrue</returns>
        private bool ParseChunkBodyLine(char ch, string content)
        {
            if (_isInChunk)
            {
                if (ParseLFSChange(ch, content))
                    return true;

                if (ch == '-')
                {
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Deleted, content, _oldLine, 0);
                    _deleted.Add(_last);
                    _oldLine++;
                    return true;
                }

                if (ch == '+')
                {
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Added, content, 0, _newLine);
                    _added.Add(_last);
                    _newLine++;
                    return true;
                }

                if (ch == ' ')
                {
                    FlushInlineHighlights();
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Normal, content, _oldLine, _newLine);
                    _result.TextDiff.Lines.Add(_last);
                    _oldLine++;
                    _newLine++;
                    return true;
                }

                if (ch == '\\')
                {
                    if (content.Equals(" No newline at end of file", StringComparison.Ordinal))
                        _last.NoNewLineEndOfFile = true;
                    return true;
                }
            }

            // チャンク本体行でなければチャンクを終了し、ヘッダー解析へフォールバックする
            FlushInlineHighlights();
            _isInChunk = false;
            return false;
        }

        /// <summary>
        /// diffヘッダー行（diff/mode/index/Binary）を解析する。
        /// </summary>
        /// <param name="line">diff出力の1行</param>
        private void ParseDiffHeaderLine(string line)
        {
            if (line.StartsWith("diff", StringComparison.Ordinal))
                return;

            if (ParseFileModeChange(line))
                return;

            if (line.StartsWith("index", StringComparison.Ordinal))
            {
                var match = REG_HASH_CHANGE().Match(line);
                if (match.Success)
                {
                    // type-changedファイルでは「削除+新規」の2セットのdiffヘッダーが
                    // 同一出力内に現れるため、最古の旧ハッシュと最新の新ハッシュを保持する
                    if (string.IsNullOrEmpty(_result.OldHash))
                        _result.OldHash = match.Groups[1].Value;
                    _result.NewHash = match.Groups[2].Value;
                }
                return;
            }

            if (line.StartsWith("Binary", StringComparison.Ordinal))
                _result.IsBinary = true;
        }

        /// <summary>
        /// ファイルモード変更行を解析する。
        /// </summary>
        /// <param name="line">diff出力の1行</param>
        /// <returns>モード変更行として処理した場合はtrue</returns>
        private bool ParseFileModeChange(string line)
        {
            if (line.StartsWith("old mode ", StringComparison.Ordinal))
            {
                _result.OldMode = line[9..];
                return true;
            }

            if (line.StartsWith("new mode ", StringComparison.Ordinal))
            {
                _result.NewMode = line[9..];
                return true;
            }

            if (line.StartsWith("deleted file mode ", StringComparison.Ordinal))
            {
                _result.OldMode = line[18..];
                return true;
            }

            if (line.StartsWith("new file mode ", StringComparison.Ordinal))
            {
                _result.NewMode = line[14..];
                return true;
            }

            return false;
        }

        /// <summary>
        /// LFSポインタファイルの差分行を解析する。
        /// </summary>
        /// <param name="ch">行頭の1文字</param>
        /// <param name="content">行頭を除いた内容</param>
        /// <returns>LFS関連行として処理した場合はtrue</returns>
        private bool ParseLFSChange(char ch, string content)
        {
            if (_result.IsLFS)
            {
                if (ch == '-')
                {
                    if (content.StartsWith(LFS_OID_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.Old.Oid = content[11..];
                    else if (content.StartsWith(LFS_SIZE_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.Old.Size = long.Parse(content.AsSpan(5));
                }
                else if (ch == '+')
                {
                    if (content.StartsWith(LFS_OID_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.New.Oid = content[11..];
                    else if (content.StartsWith(LFS_SIZE_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.New.Size = long.Parse(content.AsSpan(5));
                }
                else if (ch == ' ')
                {
                    if (content.StartsWith(LFS_SIZE_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.New.Size = _result.LFSDiff.Old.Size = long.Parse(content.AsSpan(5));
                }
                return true;
            }

            // LFSポインタの判定はチャンク先頭行（ハンクヘッダー直後）のみ行う
            if (_result.TextDiff.Lines.Count != 1)
                return false;

            if ((_oldLine == 1 && _newLine == 1 && ch == ' ') ||
                (_oldLine == 1 && _newLine == 0 && ch == '-') ||
                (_oldLine == 0 && _newLine == 1 && ch == '+'))
            {
                if (content.StartsWith(LFS_SPECIFIER, StringComparison.Ordinal))
                {
                    _result.IsLFS = true;
                    _result.LFSDiff = new Models.LFSDiff();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// バッファに蓄積された削除行と追加行のインラインハイライトを計算し、結果に追加する。
        /// 削除行と追加行が同数の場合、対応する行同士でインライン差分を検出する。
        /// </summary>
        private void FlushInlineHighlights()
        {
            if (_deleted.Count > 0)
            {
                if (_added.Count == _deleted.Count)
                {
                    for (int i = _added.Count - 1; i >= 0; i--)
                    {
                        var left = _deleted[i];
                        var right = _added[i];

                        if (left.Content.Length > MaxLineLengthForInlineDiff || right.Content.Length > MaxLineLengthForInlineDiff)
                            continue;

                        var chunks = Models.TextInlineChange.Compare(left.Content, right.Content);
                        if (chunks.Count > MaxInlineDiffChunks)
                            continue;

                        foreach (var chunk in chunks)
                        {
                            if (chunk.DeletedCount > 0)
                                left.Highlights.Add(new Models.TextRange(chunk.DeletedStart, chunk.DeletedCount));

                            if (chunk.AddedCount > 0)
                                right.Highlights.Add(new Models.TextRange(chunk.AddedStart, chunk.AddedCount));
                        }
                    }
                }

                _result.TextDiff.Lines.AddRange(_deleted);
                _deleted.Clear();
            }

            if (_added.Count > 0)
            {
                _result.TextDiff.Lines.AddRange(_added);
                _added.Clear();
            }
        }

        /// <summary>解析結果の蓄積先</summary>
        private readonly Models.DiffResult _result = new Models.DiffResult() { TextDiff = new Models.TextDiff() };
        /// <summary>削除行のバッファ（インラインハイライト用）</summary>
        private readonly List<Models.TextDiffLine> _deleted = [];
        /// <summary>追加行のバッファ（インラインハイライト用）</summary>
        private readonly List<Models.TextDiffLine> _added = [];
        /// <summary>直前に処理した差分行</summary>
        private Models.TextDiffLine _last = null;
        /// <summary>旧ファイルの現在の行番号</summary>
        private int _oldLine = 0;
        /// <summary>新ファイルの現在の行番号</summary>
        private int _newLine = 0;
        /// <summary>チャンク（@@〜）内を解析中かどうか</summary>
        private bool _isInChunk = false;
    }

    /// <summary>インラインハイライト計算対象の最大行長</summary>
    private const int MaxLineLengthForInlineDiff = 1024;
    /// <summary>インラインハイライトとして扱う最大チャンク数</summary>
    private const int MaxInlineDiffChunks = 4;
}
