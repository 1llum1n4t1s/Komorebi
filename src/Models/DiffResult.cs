using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;

namespace Komorebi.Models;

/// <summary>
/// テキストdiffの行タイプを表すenum
/// </summary>
public enum TextDiffLineType
{
    /// <summary>なし（未設定）</summary>
    None,
    /// <summary>変更なしの行</summary>
    Normal,
    /// <summary>ハンクヘッダー行（@@で始まる行）</summary>
    Indicator,
    /// <summary>追加された行</summary>
    Added,
    /// <summary>削除された行</summary>
    Deleted,
}

/// <summary>
/// テキスト内の文字範囲を表すクラス（ハイライト用）
/// </summary>
/// <param name="p">開始位置</param>
/// <param name="n">文字数</param>
public class TextRange(int p, int n)
{
    /// <summary>範囲の開始位置</summary>
    public int Start { get; set; } = p;
    /// <summary>範囲の終了位置</summary>
    public int End { get; set; } = p + n - 1;
}

/// <summary>
/// テキストdiffの1行分のデータを保持するクラス
/// </summary>
public class TextDiffLine
{
    /// <summary>行の種別（追加/削除/通常等）</summary>
    public TextDiffLineType Type { get; set; } = TextDiffLineType.None;
    /// <summary>行の内容テキスト</summary>
    public string Content { get; set; } = "";
    /// <summary>変更前ファイルの行番号（0は未設定）</summary>
    public int OldLineNumber { get; set; } = 0;
    /// <summary>変更後ファイルの行番号（0は未設定）</summary>
    public int NewLineNumber { get; set; } = 0;
    /// <summary>行内のハイライト範囲リスト（差分ハイライト用）</summary>
    public List<TextRange> Highlights { get; set; } = [];
    /// <summary>ファイル末尾に改行がないことを示すフラグ</summary>
    public bool NoNewLineEndOfFile { get; set; } = false;

    /// <summary>変更前の行番号表示文字列（未設定時は空文字）</summary>
    public string OldLine => OldLineNumber == 0 ? string.Empty : OldLineNumber.ToString();
    /// <summary>変更後の行番号表示文字列（未設定時は空文字）</summary>
    public string NewLine => NewLineNumber == 0 ? string.Empty : NewLineNumber.ToString();

    /// <summary>デフォルトコンストラクタ</summary>
    public TextDiffLine() { }
    /// <summary>
    /// 全プロパティを指定してdiff行を生成する
    /// </summary>
    /// <param name="type">行の種別</param>
    /// <param name="content">行の内容</param>
    /// <param name="oldLine">変更前の行番号</param>
    /// <param name="newLine">変更後の行番号</param>
    public TextDiffLine(TextDiffLineType type, string content, int oldLine, int newLine)
    {
        Type = type;
        Content = content;
        OldLineNumber = oldLine;
        NewLineNumber = newLine;
    }
}

/// <summary>
/// テキストdiffの選択範囲を保持するクラス。パッチ生成時に使用。
/// </summary>
public class TextDiffSelection
{
    /// <summary>選択範囲の開始行</summary>
    public int StartLine { get; set; } = 0;
    /// <summary>選択範囲の終了行</summary>
    public int EndLine { get; set; } = 0;
    /// <summary>選択範囲内に変更があるかどうか</summary>
    public bool HasChanges { get; set; } = false;
    /// <summary>選択範囲外の無視された追加行数</summary>
    public int IgnoredAdds { get; set; } = 0;
    /// <summary>選択範囲外の無視された削除行数</summary>
    public int IgnoredDeletes { get; set; } = 0;
}

/// <summary>
/// テキスト形式のdiff結果。行単位のdiffデータとパッチ生成機能を提供する。
/// </summary>
public partial class TextDiff
{
    /// <summary>diff結果の全行リスト</summary>
    public List<TextDiffLine> Lines { get; set; } = [];
    /// <summary>最大行番号（UI表示の桁数計算用）</summary>
    public int MaxLineNumber = 0;
    /// <summary>追加行数</summary>
    public int AddedLines { get; set; } = 0;
    /// <summary>削除行数</summary>
    public int DeletedLines { get; set; } = 0;

    /// <summary>
    /// 指定範囲の選択情報を生成する。パッチ生成時に選択行の統計を計算する。
    /// </summary>
    /// <param name="startLine">選択開始行（1始まり）</param>
    /// <param name="endLine">選択終了行（1始まり）</param>
    /// <param name="isCombined">結合表示モードかどうか</param>
    /// <param name="isOldSide">変更前（左側）の選択かどうか</param>
    /// <returns>選択範囲情報</returns>
    public TextDiffSelection MakeSelection(int startLine, int endLine, bool isCombined, bool isOldSide)
    {
        var rs = new TextDiffSelection();
        rs.StartLine = startLine;
        rs.EndLine = endLine;

        for (int i = 0; i < startLine - 1; i++)
        {
            var line = Lines[i];
            if (line.Type == TextDiffLineType.Added)
                rs.IgnoredAdds++;
            else if (line.Type == TextDiffLineType.Deleted)
                rs.IgnoredDeletes++;
        }

        for (int i = startLine - 1; i < endLine; i++)
        {
            var line = Lines[i];
            if (line.Type == TextDiffLineType.Added)
            {
                if (isCombined || !isOldSide)
                {
                    rs.HasChanges = true;
                    break;
                }
            }
            else if (line.Type == TextDiffLineType.Deleted)
            {
                if (isCombined || isOldSide)
                {
                    rs.HasChanges = true;
                    break;
                }
            }
        }

        return rs;
    }

    /// <summary>
    /// 新規ファイルの選択範囲からパッチを生成する。
    /// リネーム/コピーされたファイルでも apply が壊れないよう、`--- a/` 側も常に変更後のパスを使う（upstream 5a35c415）。
    /// </summary>
    /// <param name="file">対象ファイルのパス（変更後のパス）</param>
    /// <param name="fileBlobGuid">ファイルのBlobハッシュ</param>
    /// <param name="selection">選択範囲</param>
    /// <param name="revert">取り消しパッチかどうか</param>
    /// <param name="output">出力ファイルパス</param>
    public void GenerateNewPatchFromSelection(string file, string fileBlobGuid, TextDiffSelection selection, bool revert, string output)
    {
        var isTracked = !string.IsNullOrEmpty(fileBlobGuid);
        var fileGuid = isTracked ? fileBlobGuid : "00000000";

        using var writer = new StreamWriter(output);
        writer.NewLine = "\n";
        writer.WriteLine($"diff --git a/{file} b/{file}");
        if (!revert && !isTracked)
            writer.WriteLine("new file mode 100644");
        writer.WriteLine($"index 00000000...{fileGuid}");
        writer.WriteLine($"--- {(revert || isTracked ? $"a/{file}" : "/dev/null")}");
        writer.WriteLine($"+++ b/{file}");

        var additions = selection.EndLine - selection.StartLine;
        if (selection.StartLine != 1)
            additions++;

        if (revert)
        {
            var totalLines = Lines.Count - 1;
            writer.WriteLine($"@@ -0,{totalLines - additions} +0,{totalLines} @@");
            for (int i = 1; i <= totalLines; i++)
            {
                var line = Lines[i];
                if (line.Type != TextDiffLineType.Added)
                    continue;

                if (i >= selection.StartLine - 1 && i < selection.EndLine)
                    WriteLine(writer, '+', line);
                else
                    WriteLine(writer, ' ', line);
            }
        }
        else
        {
            writer.WriteLine($"@@ -0,0 +0,{additions} @@");
            for (int i = selection.StartLine - 1; i < selection.EndLine; i++)
            {
                var line = Lines[i];
                if (line.Type != TextDiffLineType.Added)
                    continue;
                WriteLine(writer, '+', line);
            }
        }

        writer.Flush();
    }

    /// <summary>
    /// 既存ファイルの選択範囲からパッチを生成する（統合diff表示用）。
    /// リネーム/コピーされたファイルでも apply が壊れないよう、`--- a/` 側も常に変更後のパスを使う（upstream 5a35c415）。
    /// </summary>
    /// <param name="file">対象ファイルのパス（変更後のパス）</param>
    /// <param name="fileTreeGuid">ファイルのツリーハッシュ</param>
    /// <param name="selection">選択範囲</param>
    /// <param name="revert">取り消しパッチかどうか</param>
    /// <param name="output">出力ファイルパス</param>
    public void GeneratePatchFromSelection(string file, string fileTreeGuid, TextDiffSelection selection, bool revert, string output)
    {
        using var writer = new StreamWriter(output);
        writer.NewLine = "\n";
        writer.WriteLine($"diff --git a/{file} b/{file}");
        writer.WriteLine($"index 00000000...{fileTreeGuid} 100644");
        writer.WriteLine($"--- a/{file}");
        writer.WriteLine($"+++ b/{file}");

        // If last line of selection is a change. Find one more line.
        string tail = null;
        if (selection.EndLine < Lines.Count)
        {
            var lastLine = Lines[selection.EndLine - 1];
            if (lastLine.Type == TextDiffLineType.Added || lastLine.Type == TextDiffLineType.Deleted)
            {
                for (int i = selection.EndLine; i < Lines.Count; i++)
                {
                    var line = Lines[i];
                    if (line.Type == TextDiffLineType.Indicator)
                        break;
                    if (line.Type == TextDiffLineType.Normal ||
                        (revert && line.Type == TextDiffLineType.Added) ||
                        (!revert && line.Type == TextDiffLineType.Deleted))
                    {
                        tail = line.Content;
                        break;
                    }
                }
            }
        }

        // If the first line is not indicator.
        if (Lines[selection.StartLine - 1].Type != TextDiffLineType.Indicator)
        {
            var indicator = selection.StartLine - 1;
            for (int i = selection.StartLine - 2; i >= 0; i--)
            {
                var line = Lines[i];
                if (line.Type == TextDiffLineType.Indicator)
                {
                    indicator = i;
                    break;
                }
            }

            var ignoreAdds = 0;
            var ignoreRemoves = 0;
            for (int i = 0; i < indicator; i++)
            {
                var line = Lines[i];
                if (line.Type == TextDiffLineType.Added)
                {
                    ignoreAdds++;
                }
                else if (line.Type == TextDiffLineType.Deleted)
                {
                    ignoreRemoves++;
                }
            }

            for (int i = indicator; i < selection.StartLine - 1; i++)
            {
                var line = Lines[i];
                if (line.Type == TextDiffLineType.Indicator)
                {
                    ProcessIndicatorForPatch(writer, line, i, selection.StartLine, selection.EndLine, ignoreRemoves, ignoreAdds, revert, tail is not null);
                }
                else if (line.Type == TextDiffLineType.Added)
                {
                    if (revert)
                        WriteLine(writer, ' ', line);
                }
                else if (line.Type == TextDiffLineType.Deleted)
                {
                    if (!revert)
                        WriteLine(writer, ' ', line);
                }
                else if (line.Type == TextDiffLineType.Normal)
                {
                    WriteLine(writer, ' ', line);
                }
            }
        }

        // Outputs the selected lines.
        for (int i = selection.StartLine - 1; i < selection.EndLine; i++)
        {
            var line = Lines[i];
            if (line.Type == TextDiffLineType.Indicator)
            {
                if (!ProcessIndicatorForPatch(writer, line, i, selection.StartLine, selection.EndLine, selection.IgnoredDeletes, selection.IgnoredAdds, revert, tail is not null))
                    break;
            }
            else if (line.Type == TextDiffLineType.Normal)
            {
                WriteLine(writer, ' ', line);
            }
            else if (line.Type == TextDiffLineType.Added)
            {
                WriteLine(writer, '+', line);
            }
            else if (line.Type == TextDiffLineType.Deleted)
            {
                WriteLine(writer, '-', line);
            }
        }

        if (!string.IsNullOrEmpty(tail))
            writer.WriteLine($" {tail}");
        writer.Flush();
    }

    /// <summary>
    /// 既存ファイルの選択範囲からパッチを生成する（左右分割diff表示用）。
    /// リネーム/コピーされたファイルでも apply が壊れないよう、`--- a/` 側も常に変更後のパスを使う（upstream 5a35c415）。
    /// </summary>
    /// <param name="file">対象ファイルのパス（変更後のパス）</param>
    /// <param name="fileTreeGuid">ファイルのツリーハッシュ</param>
    /// <param name="selection">選択範囲</param>
    /// <param name="revert">取り消しパッチかどうか</param>
    /// <param name="isOldSide">変更前（左側）からの選択かどうか</param>
    /// <param name="output">出力ファイルパス</param>
    public void GeneratePatchFromSelectionSingleSide(string file, string fileTreeGuid, TextDiffSelection selection, bool revert, bool isOldSide, string output)
    {
        using var writer = new StreamWriter(output);
        writer.NewLine = "\n";
        writer.WriteLine($"diff --git a/{file} b/{file}");
        writer.WriteLine($"index 00000000...{fileTreeGuid} 100644");
        writer.WriteLine($"--- a/{file}");
        writer.WriteLine($"+++ b/{file}");

        // If last line of selection is a change. Find one more line.
        string tail = null;
        if (selection.EndLine < Lines.Count)
        {
            var lastLine = Lines[selection.EndLine - 1];
            if (lastLine.Type == TextDiffLineType.Added || lastLine.Type == TextDiffLineType.Deleted)
            {
                for (int i = selection.EndLine; i < Lines.Count; i++)
                {
                    var line = Lines[i];
                    if (line.Type == TextDiffLineType.Indicator)
                        break;
                    if (revert)
                    {
                        if (line.Type == TextDiffLineType.Normal || line.Type == TextDiffLineType.Added)
                        {
                            tail = line.Content;
                            break;
                        }
                    }
                    else
                    {
                        if (line.Type == TextDiffLineType.Normal || line.Type == TextDiffLineType.Deleted)
                        {
                            tail = line.Content;
                            break;
                        }
                    }
                }
            }
        }

        // If the first line is not indicator.
        if (Lines[selection.StartLine - 1].Type != TextDiffLineType.Indicator)
        {
            var indicator = selection.StartLine - 1;
            for (int i = selection.StartLine - 2; i >= 0; i--)
            {
                var line = Lines[i];
                if (line.Type == TextDiffLineType.Indicator)
                {
                    indicator = i;
                    break;
                }
            }

            var ignoreAdds = 0;
            var ignoreRemoves = 0;
            for (int i = 0; i < indicator; i++)
            {
                var line = Lines[i];
                if (line.Type == TextDiffLineType.Added)
                {
                    ignoreAdds++;
                }
                else if (line.Type == TextDiffLineType.Deleted)
                {
                    ignoreRemoves++;
                }
            }

            for (int i = indicator; i < selection.StartLine - 1; i++)
            {
                var line = Lines[i];
                if (line.Type == TextDiffLineType.Indicator)
                {
                    ProcessIndicatorForPatchSingleSide(writer, line, i, selection.StartLine, selection.EndLine, ignoreRemoves, ignoreAdds, revert, isOldSide, tail is not null);
                }
                else if (line.Type == TextDiffLineType.Added)
                {
                    if (revert)
                        WriteLine(writer, ' ', line);
                }
                else if (line.Type == TextDiffLineType.Deleted)
                {
                    if (!revert)
                        WriteLine(writer, ' ', line);
                }
                else if (line.Type == TextDiffLineType.Normal)
                {
                    WriteLine(writer, ' ', line);
                }
            }
        }

        // Outputs the selected lines.
        for (int i = selection.StartLine - 1; i < selection.EndLine; i++)
        {
            var line = Lines[i];
            if (line.Type == TextDiffLineType.Indicator)
            {
                if (!ProcessIndicatorForPatchSingleSide(writer, line, i, selection.StartLine, selection.EndLine, selection.IgnoredDeletes, selection.IgnoredAdds, revert, isOldSide, tail is not null))
                    break;
            }
            else if (line.Type == TextDiffLineType.Normal)
            {
                WriteLine(writer, ' ', line);
            }
            else if (line.Type == TextDiffLineType.Added)
            {
                if (isOldSide)
                {
                    if (revert)
                    {
                        WriteLine(writer, ' ', line);
                    }
                    else
                    {
                        selection.IgnoredAdds++;
                    }
                }
                else
                {
                    WriteLine(writer, '+', line);
                }
            }
            else if (line.Type == TextDiffLineType.Deleted)
            {
                if (isOldSide)
                {
                    WriteLine(writer, '-', line);
                }
                else
                {
                    if (!revert)
                    {
                        WriteLine(writer, ' ', line);
                    }
                    else
                    {
                        selection.IgnoredDeletes++;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(tail))
            writer.WriteLine($" {tail}");
        writer.Flush();
    }

    /// <summary>
    /// ハンクヘッダー（@@行）を処理し、行数を再計算してパッチに書き込む（統合表示用）
    /// </summary>
    /// <returns>パッチに出力する行がある場合はtrue</returns>
    private bool ProcessIndicatorForPatch(StreamWriter writer, TextDiffLine indicator, int idx, int start, int end, int ignoreRemoves, int ignoreAdds, bool revert, bool tailed)
    {
        var match = REG_INDICATOR().Match(indicator.Content);
        var oldStart = int.Parse(match.Groups[1].Value);
        var newStart = int.Parse(match.Groups[2].Value) + ignoreRemoves - ignoreAdds;
        var oldCount = 0;
        var newCount = 0;
        for (int i = idx + 1; i < end; i++)
        {
            var test = Lines[i];
            if (test.Type == TextDiffLineType.Indicator)
                break;

            if (test.Type == TextDiffLineType.Normal)
            {
                oldCount++;
                newCount++;
            }
            else if (test.Type == TextDiffLineType.Added)
            {
                if (i < start - 1)
                {
                    if (revert)
                    {
                        newCount++;
                        oldCount++;
                    }
                }
                else
                {
                    newCount++;
                }

                if (i == end - 1 && tailed)
                {
                    newCount++;
                    oldCount++;
                }
            }
            else if (test.Type == TextDiffLineType.Deleted)
            {
                if (i < start - 1)
                {
                    if (!revert)
                    {
                        newCount++;
                        oldCount++;
                    }
                }
                else
                {
                    oldCount++;
                }

                if (i == end - 1 && tailed)
                {
                    newCount++;
                    oldCount++;
                }
            }
        }

        if (oldCount == 0 && newCount == 0)
            return false;

        writer.WriteLine($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");
        return true;
    }

    /// <summary>
    /// ハンクヘッダー（@@行）を処理し、行数を再計算してパッチに書き込む（分割表示用）
    /// </summary>
    /// <returns>パッチに出力する行がある場合はtrue</returns>
    private bool ProcessIndicatorForPatchSingleSide(StreamWriter writer, TextDiffLine indicator, int idx, int start, int end, int ignoreRemoves, int ignoreAdds, bool revert, bool isOldSide, bool tailed)
    {
        var match = REG_INDICATOR().Match(indicator.Content);
        var oldStart = int.Parse(match.Groups[1].Value);
        var newStart = int.Parse(match.Groups[2].Value) + ignoreRemoves - ignoreAdds;
        var oldCount = 0;
        var newCount = 0;
        for (int i = idx + 1; i < end; i++)
        {
            var test = Lines[i];
            if (test.Type == TextDiffLineType.Indicator)
                break;

            if (test.Type == TextDiffLineType.Normal)
            {
                oldCount++;
                newCount++;
            }
            else if (test.Type == TextDiffLineType.Added)
            {
                if (i < start - 1 || isOldSide)
                {
                    if (revert)
                    {
                        newCount++;
                        oldCount++;
                    }
                }
                else
                {
                    newCount++;
                }

                if (i == end - 1 && tailed)
                {
                    newCount++;
                    oldCount++;
                }
            }
            else if (test.Type == TextDiffLineType.Deleted)
            {
                if (i < start - 1)
                {
                    if (!revert)
                    {
                        newCount++;
                        oldCount++;
                    }
                }
                else
                {
                    if (isOldSide)
                    {
                        oldCount++;
                    }
                    else
                    {
                        if (!revert)
                        {
                            newCount++;
                            oldCount++;
                        }
                    }
                }

                if (i == end - 1 && tailed)
                {
                    newCount++;
                    oldCount++;
                }
            }
        }

        if (oldCount == 0 && newCount == 0)
            return false;

        writer.WriteLine($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");
        return true;
    }

    /// <summary>
    /// パッチファイルに1行を書き込む（プレフィックス + 内容 + 改行なし通知）
    /// </summary>
    /// <param name="writer">出力先のStreamWriter</param>
    /// <param name="prefix">行頭文字（' ', '+', '-'）</param>
    /// <param name="line">diff行データ</param>
    private static void WriteLine(StreamWriter writer, char prefix, TextDiffLine line)
    {
        writer.WriteLine($"{prefix}{line.Content}");

        if (line.NoNewLineEndOfFile)
            writer.WriteLine("\\ No newline at end of file");
    }

    /// <summary>ハンクヘッダーから開始行番号を抽出する正規表現</summary>
    [GeneratedRegex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@")]
    private static partial Regex REG_INDICATOR();
}

/// <summary>
/// Git LFSオブジェクトのdiff結果
/// </summary>
public class LFSDiff
{
    /// <summary>変更前のLFSオブジェクト</summary>
    public LFSObject Old { get; set; } = new LFSObject();
    /// <summary>変更後のLFSオブジェクト</summary>
    public LFSObject New { get; set; } = new LFSObject();
}

/// <summary>
/// バイナリファイルのdiff結果（サイズ情報のみ）
/// </summary>
public class BinaryDiff
{
    /// <summary>変更前のファイルサイズ（バイト）</summary>
    public long OldSize { get; set; } = 0;
    /// <summary>変更後のファイルサイズ（バイト）</summary>
    public long NewSize { get; set; } = 0;
}

/// <summary>
/// 画像ファイルのdiff結果。変更前後の画像データとサイズ情報を保持する。
/// </summary>
public class ImageDiff
{
    /// <summary>変更前の画像データ</summary>
    public Bitmap Old { get; set; } = null;
    /// <summary>変更後の画像データ</summary>
    public Bitmap New { get; set; } = null;

    /// <summary>変更前のファイルサイズ（バイト）</summary>
    public long OldFileSize { get; set; } = 0;
    /// <summary>変更後のファイルサイズ（バイト）</summary>
    public long NewFileSize { get; set; } = 0;

    /// <summary>変更前の画像サイズ表示文字列（"幅 x 高さ"形式）</summary>
    public string OldImageSize => Old is not null ? $"{Old.PixelSize.Width} x {Old.PixelSize.Height}" : "0 x 0";
    /// <summary>変更後の画像サイズ表示文字列（"幅 x 高さ"形式）</summary>
    public string NewImageSize => New is not null ? $"{New.PixelSize.Width} x {New.PixelSize.Height}" : "0 x 0";
}

/// <summary>
/// 変更なしまたは改行コードのみの変更を表すマーカークラス
/// </summary>
public class NoOrEOLChange;

/// <summary>
/// サブモジュールのdiff結果。変更前後のサブモジュール情報を保持する。
/// </summary>
public class SubmoduleDiff
{
    /// <summary>サブモジュールの作業ディレクトリの絶対パス</summary>
    public string FullPath { get; set; } = string.Empty;
    /// <summary>変更前のサブモジュール情報</summary>
    public RevisionSubmodule Old { get; set; } = null;
    /// <summary>変更後のサブモジュール情報</summary>
    public RevisionSubmodule New { get; set; } = null;

    /// <summary>
    /// サブモジュール詳細ダイアログを開ける条件を満たすか。
    /// 初期化済みで Old/New 両方があり、かつ両リビジョンが有効（lost submodule revision でない）な場合のみ true。
    /// </summary>
    public bool CanOpenDetails => File.Exists(Path.Combine(FullPath, ".git")) &&
        Old != null && Old.Commit.Author != User.Invalid &&
        New != null && New.Commit.Author != User.Invalid;
}

/// <summary>
/// diff操作の総合結果を保持するクラス。テキスト、バイナリ、LFS各種diffの結果を含む。
/// </summary>
public class DiffResult
{
    /// <summary>バイナリファイルかどうか</summary>
    public bool IsBinary { get; set; } = false;
    /// <summary>Git LFSオブジェクトかどうか</summary>
    public bool IsLFS { get; set; } = false;
    /// <summary>変更前のBlobハッシュ</summary>
    public string OldHash { get; set; } = string.Empty;
    /// <summary>変更後のBlobハッシュ</summary>
    public string NewHash { get; set; } = string.Empty;
    /// <summary>変更前のファイルモード（パーミッション）</summary>
    public string OldMode { get; set; } = string.Empty;
    /// <summary>変更後のファイルモード（パーミッション）</summary>
    public string NewMode { get; set; } = string.Empty;
    /// <summary>テキストdiffの結果（バイナリの場合はnull）</summary>
    public TextDiff TextDiff { get; set; } = null;
    /// <summary>LFS diffの結果（LFSでない場合はnull）</summary>
    public LFSDiff LFSDiff { get; set; } = null;

    /// <summary>
    /// ファイルモードの変更を表す文字列（例: "100644 → 100755"）。変更なしの場合は空文字。
    /// </summary>
    public string FileModeChange
    {
        get
        {
            if (string.IsNullOrEmpty(OldMode) && string.IsNullOrEmpty(NewMode))
                return string.Empty;

            var oldDisplay = string.IsNullOrEmpty(OldMode) ? "0" : OldMode;
            var newDisplay = string.IsNullOrEmpty(NewMode) ? "0" : NewMode;

            return $"{oldDisplay} → {newDisplay}";
        }
    }
}
