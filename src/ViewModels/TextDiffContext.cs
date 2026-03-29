using System;
using System.Collections.Generic;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// テキスト差分ビューで選択されたチャンク（変更ブロック）の情報を保持するレコード。
/// 表示位置、行範囲、表示モード、サイド情報を含む。
/// </summary>
public record TextDiffSelectedChunk(double Y, double Height, int StartIdx, int EndIdx, bool Combined, bool IsOldSide)
{
    /// <summary>
    /// 2つのチャンク選択が異なるかどうかを判定する。
    /// </summary>
    public static bool IsChanged(TextDiffSelectedChunk oldValue, TextDiffSelectedChunk newValue)
    {
        if (newValue is null)
            return oldValue is not null;

        if (oldValue is null)
            return true;

        return Math.Abs(newValue.Y - oldValue.Y) > 0.001 ||
            Math.Abs(newValue.Height - oldValue.Height) > 0.001 ||
            newValue.StartIdx != oldValue.StartIdx ||
            newValue.EndIdx != oldValue.EndIdx ||
            newValue.Combined != oldValue.Combined ||
            newValue.IsOldSide != oldValue.IsOldSide;
    }
}

/// <summary>
/// テキスト差分表示のコンテキスト基底クラス。
/// 差分データ、スクロール位置、ブロックナビゲーション、選択チャンクを管理する。
/// </summary>
public class TextDiffContext : ObservableObject
{
    /// <summary>差分生成オプション。</summary>
    public Models.DiffOption Option => _option;
    /// <summary>テキスト差分データ。</summary>
    public Models.TextDiff Data => _data;

    /// <summary>
    /// 差分ビューのスクロールオフセット。
    /// </summary>
    public Vector ScrollOffset
    {
        get => _scrollOffset;
        set => SetProperty(ref _scrollOffset, value);
    }

    /// <summary>
    /// 変更ブロック間のナビゲーション状態。
    /// </summary>
    public BlockNavigation BlockNavigation
    {
        get => _blockNavigation;
        set => SetProperty(ref _blockNavigation, value);
    }

    /// <summary>
    /// 現在表示されている行範囲。
    /// </summary>
    public TextLineRange DisplayRange
    {
        get => _displayRange;
        set => SetProperty(ref _displayRange, value);
    }

    /// <summary>
    /// 現在選択されている変更チャンク。
    /// </summary>
    public TextDiffSelectedChunk SelectedChunk
    {
        get => _selectedChunk;
        set => SetProperty(ref _selectedChunk, value);
    }

    /// <summary>
    /// 指定行インデックスを含む変更ブロック（ハンク）の行範囲を検索する。
    /// 連続する通常行が2行以上あるか、インジケータ行がブロックの境界となる。
    /// </summary>
    public (int, int) FindRangeByIndex(List<Models.TextDiffLine> lines, int lineIdx)
    {
        var startIdx = -1;
        var endIdx = -1;

        var normalLineCount = 0;
        var modifiedLineCount = 0;

        // 上方向に走査して変更ブロックの開始位置を検索
        for (int i = lineIdx; i >= 0; i--)
        {
            var line = lines[i];
            if (line.Type == Models.TextDiffLineType.Indicator)
            {
                startIdx = i;
                break;
            }

            if (line.Type == Models.TextDiffLineType.Normal)
            {
                normalLineCount++;
                // 通常行が2行以上連続したら境界とみなす
                if (normalLineCount >= 2)
                {
                    startIdx = i;
                    break;
                }
            }
            else
            {
                normalLineCount = 0;
                modifiedLineCount++;
            }
        }

        // 下方向に走査して変更ブロックの終了位置を検索
        normalLineCount = lines[lineIdx].Type == Models.TextDiffLineType.Normal ? 1 : 0;
        for (int i = lineIdx + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Type == Models.TextDiffLineType.Indicator)
            {
                endIdx = i;
                break;
            }

            if (line.Type == Models.TextDiffLineType.Normal)
            {
                normalLineCount++;
                if (normalLineCount >= 2)
                {
                    endIdx = i;
                    break;
                }
            }
            else
            {
                normalLineCount = 0;
                modifiedLineCount++;
            }
        }

        if (endIdx == -1)
            endIdx = lines.Count - 1;

        // 変更行がない場合は無効な範囲を返す
        return modifiedLineCount > 0 ? (startIdx, endIdx) : (-1, -1);
    }

    /// <summary>
    /// サイドバイサイド表示かどうか。派生クラスでオーバーライドする。
    /// </summary>
    public virtual bool IsSideBySide()
    {
        return false;
    }

    /// <summary>
    /// 表示モードを切り替える。統合表示とサイドバイサイド表示を相互に変換する。
    /// </summary>
    public virtual TextDiffContext SwitchMode()
    {
        return null;
    }

    /// <summary>
    /// 前の差分コンテキストからスクロール位置やナビゲーション状態を引き継ぐ。
    /// 同一ファイルの差分の場合のみ状態を保持する。
    /// </summary>
    protected void TryKeepPrevState(TextDiffContext prev, List<Models.TextDiffLine> lines)
    {
        var fastTest = prev is not null &&
            prev._option.IsUnstaged == _option.IsUnstaged &&
            prev._option.Path.Equals(_option.Path, StringComparison.Ordinal) &&
            prev._option.OrgPath.Equals(_option.OrgPath, StringComparison.Ordinal) &&
            prev._option.Revisions.Count == _option.Revisions.Count;

        if (!fastTest)
        {
            _blockNavigation = new BlockNavigation(lines, 0);
            return;
        }

        for (int i = 0; i < _option.Revisions.Count; i++)
        {
            if (!prev._option.Revisions[i].Equals(_option.Revisions[i], StringComparison.Ordinal))
            {
                _blockNavigation = new BlockNavigation(lines, 0);
                return;
            }
        }

        _blockNavigation = new BlockNavigation(lines, prev._blockNavigation.GetCurrentBlockIndex());
    }

    protected Models.DiffOption _option = null;
    protected Models.TextDiff _data = null;
    protected Vector _scrollOffset = Vector.Zero;
    protected BlockNavigation _blockNavigation = null;

    private TextLineRange _displayRange = null;
    private TextDiffSelectedChunk _selectedChunk = null;
}

/// <summary>
/// 統合（コンバインド）差分表示のコンテキスト。追加・削除行を1つのビューに表示する。
/// </summary>
public class CombinedTextDiff : TextDiffContext
{
    /// <summary>
    /// コンストラクタ。差分データと前のコンテキストから状態を初期化する。
    /// </summary>
    public CombinedTextDiff(Models.DiffOption option, Models.TextDiff diff, TextDiffContext previous = null)
    {
        _option = option;
        _data = diff;

        TryKeepPrevState(previous, _data.Lines);
    }

    /// <summary>
    /// サイドバイサイド表示に切り替える。
    /// </summary>
    public override TextDiffContext SwitchMode()
    {
        return new TwoSideTextDiff(_option, _data, this);
    }
}

/// <summary>
/// サイドバイサイド（左右分割）差分表示のコンテキスト。
/// 旧版と新版を左右に並べて表示する。
/// </summary>
public class TwoSideTextDiff : TextDiffContext
{
    /// <summary>旧版（左側）の差分行リスト。</summary>
    public List<Models.TextDiffLine> Old { get; } = [];
    /// <summary>新版（右側）の差分行リスト。</summary>
    public List<Models.TextDiffLine> New { get; } = [];

    /// <summary>
    /// コンストラクタ。統合差分データを左右分割形式に変換する。
    /// </summary>
    public TwoSideTextDiff(Models.DiffOption option, Models.TextDiff diff, TextDiffContext previous = null)
    {
        _option = option;
        _data = diff;

        // 行タイプに応じて旧版/新版に振り分け
        foreach (var line in diff.Lines)
        {
            switch (line.Type)
            {
                case Models.TextDiffLineType.Added:
                    New.Add(line);
                    break;
                case Models.TextDiffLineType.Deleted:
                    Old.Add(line);
                    break;
                default:
                    // 通常行・インジケータ行は空行で行数を揃えてから両側に追加
                    FillEmptyLines();
                    Old.Add(line);
                    New.Add(line);
                    break;
            }
        }

        // 最後に行数を揃える
        FillEmptyLines();
        TryKeepPrevState(previous, Old);
    }

    /// <summary>
    /// サイドバイサイド表示であることを示す。
    /// </summary>
    public override bool IsSideBySide()
    {
        return true;
    }

    /// <summary>
    /// 統合表示に切り替える。
    /// </summary>
    public override TextDiffContext SwitchMode()
    {
        return new CombinedTextDiff(_option, _data, this);
    }

    /// <summary>
    /// 片側の行範囲を統合差分の行範囲に変換する（単一サイド選択用）。
    /// </summary>
    public void GetCombinedRangeForSingleSide(ref int startLine, ref int endLine, bool isOldSide)
    {
        endLine = Math.Min(endLine, _data.Lines.Count - 1);

        var oneSide = isOldSide ? Old : New;
        var firstContentLine = -1;
        for (int i = startLine; i <= endLine; i++)
        {
            var line = oneSide[i];
            if (line.Type != Models.TextDiffLineType.None)
            {
                firstContentLine = i;
                break;
            }
        }

        if (firstContentLine < 0)
            return;

        var endContentLine = -1;
        for (int i = Math.Min(endLine, oneSide.Count - 1); i >= startLine; i--)
        {
            var line = oneSide[i];
            if (line.Type != Models.TextDiffLineType.None)
            {
                endContentLine = i;
                break;
            }
        }

        if (endContentLine < 0)
            return;

        var firstContent = oneSide[firstContentLine];
        var endContent = oneSide[endContentLine];
        startLine = _data.Lines.IndexOf(firstContent);
        endLine = _data.Lines.IndexOf(endContent);
    }

    /// <summary>
    /// 両側の行範囲を統合差分の行範囲に変換する（自動検出ハンク用）。
    /// 最初の変更行を見つけてFindRangeByIndexでハンク範囲を取得する。
    /// </summary>
    public void GetCombinedRangeForBothSides(ref int startLine, ref int endLine, bool isOldSide)
    {
        var fromSide = isOldSide ? Old : New;
        endLine = Math.Min(endLine, fromSide.Count - 1);

        // 自動検出ハンク用: 最初の変更行を見つけてFindRangeByIndexでハンク範囲を取得
        for (int i = startLine; i <= endLine; i++)
        {
            var line = fromSide[i];
            if (line.Type == Models.TextDiffLineType.Added || line.Type == Models.TextDiffLineType.Deleted)
            {
                (startLine, endLine) = FindRangeByIndex(_data.Lines, _data.Lines.IndexOf(line));
                return;
            }

            if (line.Type == Models.TextDiffLineType.None)
            {
                var otherSide = isOldSide ? New : Old;
                var changedLine = otherSide[i]; // Find the changed line on the other side in the same position
                (startLine, endLine) = FindRangeByIndex(_data.Lines, _data.Lines.IndexOf(changedLine));
                return;
            }
        }
    }

    /// <summary>
    /// 旧版と新版の行数を揃えるために空行を追加する。
    /// サイドバイサイド表示で左右の行を対応させるために必要。
    /// </summary>
    private void FillEmptyLines()
    {
        if (Old.Count < New.Count)
        {
            int diff = New.Count - Old.Count;
            for (int i = 0; i < diff; i++)
                Old.Add(new Models.TextDiffLine());
        }
        else if (Old.Count > New.Count)
        {
            int diff = Old.Count - New.Count;
            for (int i = 0; i < diff; i++)
                New.Add(new Models.TextDiffLine());
        }
    }
}
