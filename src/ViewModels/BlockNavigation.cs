using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
///     差分ブロックのナビゲーション方向を定義する列挙型。
/// </summary>
public enum BlockNavigationDirection
{
    /// <summary>最初のブロックへ移動</summary>
    First = 0,
    /// <summary>前のブロックへ移動</summary>
    Prev,
    /// <summary>次のブロックへ移動</summary>
    Next,
    /// <summary>最後のブロックへ移動</summary>
    Last
}

/// <summary>
///     差分ビューにおけるブロック（変更箇所のまとまり）間のナビゲーションを管理するViewModel。
///     追加・削除行をブロックとしてグループ化し、ブロック間の移動をサポートする。
/// </summary>
public class BlockNavigation : ObservableObject
{
    /// <summary>
    ///     差分ブロックの開始行と終了行を表すレコード。
    /// </summary>
    /// <param name="Start">ブロックの開始行番号</param>
    /// <param name="End">ブロックの終了行番号</param>
    public record Block(int Start, int End)
    {
        /// <summary>
        ///     指定した行番号がこのブロック内に含まれるか判定する。
        /// </summary>
        /// <param name="line">判定する行番号</param>
        /// <returns>ブロック内であればtrue</returns>
        public bool Contains(int line)
        {
            return line >= Start && line <= End;
        }
    }

    /// <summary>
    ///     現在のブロック位置を「N/M」形式で表示するインジケータ文字列。
    /// </summary>
    public string Indicator
    {
        get
        {
            if (_blocks.Count == 0)
                return "-/-";

            if (_current >= 0 && _current < _blocks.Count)
                return $"{_current + 1}/{_blocks.Count}";

            return $"-/{_blocks.Count}";
        }
    }

    /// <summary>
    ///     コンストラクタ。差分行リストから変更ブロックを構築する。
    /// </summary>
    /// <param name="lines">テキスト差分の行リスト</param>
    /// <param name="cur">初期カーソル位置</param>
    public BlockNavigation(List<Models.TextDiffLine> lines, int cur)
    {
        _blocks.Clear();

        if (lines.Count == 0)
        {
            _current = -1;
            return;
        }

        // 差分行を走査して、追加・削除・変更なし行の連続をブロックとして検出する
        var lineIdx = 0;
        var blockStartIdx = 0;
        var isReadingBlock = false;
        var blocks = new List<Block>();

        foreach (var line in lines)
        {
            lineIdx++;
            if (line.Type is Models.TextDiffLineType.Added or Models.TextDiffLineType.Deleted or Models.TextDiffLineType.None)
            {
                // 変更行の開始を記録する
                if (!isReadingBlock)
                {
                    isReadingBlock = true;
                    blockStartIdx = lineIdx;
                }
            }
            else
            {
                // 変更行の終了を検出してブロックを追加する
                if (isReadingBlock)
                {
                    blocks.Add(new Block(blockStartIdx, lineIdx - 1));
                    isReadingBlock = false;
                }
            }
        }

        // 最後のブロックが未終了の場合は追加する
        if (isReadingBlock)
            blocks.Add(new Block(blockStartIdx, lines.Count));

        _blocks.AddRange(blocks);
        _current = Math.Min(_blocks.Count - 1, cur);
    }

    /// <summary>
    ///     現在のブロックインデックスを取得する。
    /// </summary>
    /// <returns>現在のブロックインデックス</returns>
    public int GetCurrentBlockIndex()
    {
        return _current;
    }

    /// <summary>
    ///     現在のブロックを取得する。
    /// </summary>
    /// <returns>現在のBlockオブジェクト。ブロックがない場合はnull</returns>
    public Block GetCurrentBlock()
    {
        if (_current >= 0 && _current < _blocks.Count)
            return _blocks[_current];

        return null;
    }

    /// <summary>
    ///     指定方向にブロックを移動し、移動先のブロックを返す。
    /// </summary>
    /// <param name="direction">移動方向</param>
    /// <returns>移動先のBlockオブジェクト。ブロックがない場合はnull</returns>
    public Block Goto(BlockNavigationDirection direction)
    {
        if (_blocks.Count == 0)
            return null;

        // 方向に応じて現在位置を更新する
        _current = direction switch
        {
            BlockNavigationDirection.First => 0,
            BlockNavigationDirection.Prev => _current <= 0 ? 0 : _current - 1,
            BlockNavigationDirection.Next => _current >= _blocks.Count - 1 ? _blocks.Count - 1 : _current + 1,
            BlockNavigationDirection.Last => _blocks.Count - 1,
            _ => _current
        };

        OnPropertyChanged(nameof(Indicator));
        return _blocks[_current];
    }

    /// <summary>
    ///     選択されたチャンクに基づいて現在位置を更新する。
    /// </summary>
    /// <param name="chunk">選択された差分チャンク</param>
    public void UpdateByChunk(TextDiffSelectedChunk chunk)
    {
        _current = -1;

        // チャンクの範囲をブロックと照合する
        var chunkStart = chunk.StartIdx + 1;
        var chunkEnd = chunk.EndIdx + 1;

        for (var i = 0; i < _blocks.Count; i++)
        {
            var block = _blocks[i];
            // チャンクがブロックより前にある場合はスキップする
            if (chunkStart > block.End)
                continue;

            // チャンクがブロックより後にある場合は1つ前のブロックを選択する
            if (chunkEnd < block.Start)
            {
                _current = i - 1;
                break;
            }

            _current = i;
        }
    }

    /// <summary>
    ///     キャレット位置に基づいて現在のブロックを更新する。
    /// </summary>
    /// <param name="caretLine">キャレットの行番号</param>
    public void UpdateByCaretPosition(int caretLine)
    {
        // 現在のブロック内にキャレットがあれば更新不要
        if (_current >= 0 && _current < _blocks.Count)
        {
            var block = _blocks[_current];
            if (block.Contains(caretLine))
                return;
        }

        _current = -1;

        // キャレット位置に最も近いブロックを探す
        for (var i = 0; i < _blocks.Count; i++)
        {
            var block = _blocks[i];
            if (block.Start > caretLine)
                break;

            _current = i;
            if (block.End >= caretLine)
                break;
        }

        OnPropertyChanged(nameof(Indicator));
    }

    /// <summary>現在のブロックインデックス</summary>
    private int _current;
    /// <summary>検出された差分ブロックのリスト</summary>
    private readonly List<Block> _blocks = [];
}
