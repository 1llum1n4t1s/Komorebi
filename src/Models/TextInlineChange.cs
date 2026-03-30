using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// テキストのインライン変更（行内差分）を表すクラス。
/// Myersアルゴリズムを使用して、削除範囲と追加範囲を検出する。
/// </summary>
public class TextInlineChange(int dp, int dc, int ap, int ac)
{
    /// <summary>削除部分の開始位置</summary>
    public int DeletedStart { get; set; } = dp;
    /// <summary>削除部分の文字数</summary>
    public int DeletedCount { get; set; } = dc;
    /// <summary>追加部分の開始位置</summary>
    public int AddedStart { get; set; } = ap;
    /// <summary>追加部分の文字数</summary>
    public int AddedCount { get; set; } = ac;

    /// <summary>差分検出用のテキストチャンク（単語または区切り文字単位）</summary>
    private class Chunk(int hash, int start, int size)
    {
        /// <summary>テキスト内容のハッシュ値</summary>
        public readonly int Hash = hash;
        /// <summary>元テキスト内の開始位置</summary>
        public readonly int Start = start;
        /// <summary>チャンクのサイズ（文字数）</summary>
        public readonly int Size = size;
        /// <summary>変更されたチャンクかどうか</summary>
        public bool Modified;
    }

    /// <summary>Myersアルゴリズムにおける編集操作の種別</summary>
    private enum Edit
    {
        /// <summary>変更なし</summary>
        None,
        /// <summary>右方向の削除</summary>
        DeletedRight,
        /// <summary>左方向の削除</summary>
        DeletedLeft,
        /// <summary>右方向の追加</summary>
        AddedRight,
        /// <summary>左方向の追加</summary>
        AddedLeft,
    }

    /// <summary>Myersアルゴリズムの編集結果</summary>
    private class EditResult
    {
        /// <summary>編集操作の種別</summary>
        public Edit State;
        /// <summary>削除範囲の開始インデックス</summary>
        public int DeleteStart;
        /// <summary>削除範囲の終了インデックス</summary>
        public int DeleteEnd;
        /// <summary>追加範囲の開始インデックス</summary>
        public int AddStart;
        /// <summary>追加範囲の終了インデックス</summary>
        public int AddEnd;
    }

    /// <summary>
    /// 2つのテキストを比較し、インライン変更のリストを返す。
    /// テキストを単語/区切り文字単位のチャンクに分割し、Myersアルゴリズムで差分を検出する。
    /// </summary>
    /// <param name="oldValue">変更前のテキスト</param>
    /// <param name="newValue">変更後のテキスト</param>
    /// <returns>インライン変更のリスト</returns>
    public static List<TextInlineChange> Compare(string oldValue, string newValue)
    {
        var hashes = new Dictionary<string, int>();
        var chunksOld = MakeChunks(hashes, oldValue);
        var chunksNew = MakeChunks(hashes, newValue);
        var sizeOld = chunksOld.Count;
        var sizeNew = chunksNew.Count;
        var max = sizeOld + sizeNew + 2;
        var forward = new int[max];
        var reverse = new int[max];
        CheckModified(chunksOld, 0, sizeOld, chunksNew, 0, sizeNew, forward, reverse);

        var ret = new List<TextInlineChange>();
        var posOld = 0;
        var posNew = 0;
        TextInlineChange last = null;
        do
        {
            while (posOld < sizeOld && posNew < sizeNew && !chunksOld[posOld].Modified && !chunksNew[posNew].Modified)
            {
                posOld++;
                posNew++;
            }

            var beginOld = posOld;
            var beginNew = posNew;
            var countOld = 0;
            var countNew = 0;
            for (; posOld < sizeOld && chunksOld[posOld].Modified; posOld++)
                countOld += chunksOld[posOld].Size;
            for (; posNew < sizeNew && chunksNew[posNew].Modified; posNew++)
                countNew += chunksNew[posNew].Size;

            if (countOld + countNew == 0)
                continue;

            var diff = new TextInlineChange(
                countOld > 0 ? chunksOld[beginOld].Start : 0,
                countOld,
                countNew > 0 ? chunksNew[beginNew].Start : 0,
                countNew);
            if (last is not null)
            {
                var midSizeOld = diff.DeletedStart - last.DeletedStart - last.DeletedCount;
                var midSizeNew = diff.AddedStart - last.AddedStart - last.AddedCount;
                if (midSizeOld == 1 && midSizeNew == 1)
                {
                    last.DeletedCount += (1 + countOld);
                    last.AddedCount += (1 + countNew);
                    continue;
                }
            }

            last = diff;
            ret.Add(diff);
        } while (posOld < sizeOld && posNew < sizeNew);

        return ret;
    }

    /// <summary>
    /// テキストを単語と区切り文字のチャンクに分割する。
    /// 各チャンクにハッシュ値を割り当て、高速な比較を可能にする。
    /// </summary>
    /// <param name="hashes">テキスト→ハッシュ値のマップ</param>
    /// <param name="text">分割対象のテキスト</param>
    /// <returns>チャンクのリスト</returns>
    private static List<Chunk> MakeChunks(Dictionary<string, int> hashes, string text)
    {
        var start = 0;
        var size = text.Length;
        var chunks = new List<Chunk>();
        var delims = new HashSet<char>(" \t+-*/=!,:;.'\"/?|&#@%`<>()[]{}\\".ToCharArray());

        for (int i = 0; i < size; i++)
        {
            var ch = text[i];
            if (delims.Contains(ch))
            {
                if (start != i)
                    AddChunk(chunks, hashes, text[start..i], start);
                AddChunk(chunks, hashes, text[i..(i + 1)], i);
                start = i + 1;
            }
        }

        if (start < size)
            AddChunk(chunks, hashes, text[start..], start);
        return chunks;
    }

    /// <summary>
    /// Myersアルゴリズムにより、新旧チャンク間の変更を再帰的に検出する
    /// </summary>
    private static void CheckModified(List<Chunk> chunksOld, int startOld, int endOld, List<Chunk> chunksNew, int startNew, int endNew, int[] forward, int[] reverse)
    {
        while (startOld < endOld && startNew < endNew && chunksOld[startOld].Hash == chunksNew[startNew].Hash)
        {
            startOld++;
            startNew++;
        }

        while (startOld < endOld && startNew < endNew && chunksOld[endOld - 1].Hash == chunksNew[endNew - 1].Hash)
        {
            endOld--;
            endNew--;
        }

        var lenOld = endOld - startOld;
        var lenNew = endNew - startNew;
        if (lenOld > 0 && lenNew > 0)
        {
            var rs = CheckModifiedEdit(chunksOld, startOld, endOld, chunksNew, startNew, endNew, forward, reverse);
            if (rs.State == Edit.None)
                return;

            if (rs.State == Edit.DeletedRight && rs.DeleteStart - 1 > startOld)
            {
                chunksOld[--rs.DeleteStart].Modified = true;
            }
            else if (rs.State == Edit.DeletedLeft && rs.DeleteEnd < endOld)
            {
                chunksOld[rs.DeleteEnd++].Modified = true;
            }
            else if (rs.State == Edit.AddedRight && rs.AddStart - 1 > startNew)
            {
                chunksNew[--rs.AddStart].Modified = true;
            }
            else if (rs.State == Edit.AddedLeft && rs.AddEnd < endNew)
            {
                chunksNew[rs.AddEnd++].Modified = true;
            }

            CheckModified(chunksOld, startOld, rs.DeleteStart, chunksNew, startNew, rs.AddStart, forward, reverse);
            CheckModified(chunksOld, rs.DeleteEnd, endOld, chunksNew, rs.AddEnd, endNew, forward, reverse);
        }
        else if (lenOld > 0)
        {
            for (int i = startOld; i < endOld; i++)
                chunksOld[i].Modified = true;
        }
        else if (lenNew > 0)
        {
            for (int i = startNew; i < endNew; i++)
                chunksNew[i].Modified = true;
        }
    }

    /// <summary>
    /// Myersアルゴリズムの前方・後方探索により、最短編集スクリプトを求める
    /// </summary>
    private static EditResult CheckModifiedEdit(List<Chunk> chunksOld, int startOld, int endOld, List<Chunk> chunksNew, int startNew, int endNew, int[] forward, int[] reverse)
    {
        var lenOld = endOld - startOld;
        var lenNew = endNew - startNew;
        var max = lenOld + lenNew + 1;
        var half = max / 2;
        var delta = lenOld - lenNew;
        var deltaEven = delta % 2 == 0;
        var rs = new EditResult() { State = Edit.None };

        forward[1 + half] = 0;
        reverse[1 + half] = lenOld + 1;

        for (int i = 0; i <= half; i++)
        {
            for (int j = -i; j <= i; j += 2)
            {
                var idx = j + half;
                int o;
                if (j == -i || (j != i && forward[idx - 1] < forward[idx + 1]))
                {
                    o = forward[idx + 1];
                    rs.State = Edit.AddedRight;
                }
                else
                {
                    o = forward[idx - 1] + 1;
                    rs.State = Edit.DeletedRight;
                }

                var n = o - j;

                var startX = o;
                var startY = n;
                while (o < lenOld && n < lenNew && chunksOld[o + startOld].Hash == chunksNew[n + startNew].Hash)
                {
                    o++;
                    n++;
                }

                forward[idx] = o;

                if (!deltaEven && j - delta >= -i + 1 && j - delta <= i - 1)
                {
                    var revIdx = (j - delta) + half;
                    var revOld = reverse[revIdx];
                    int revNew = revOld - j;
                    if (revOld <= o && revNew <= n)
                    {
                        if (i == 0)
                        {
                            rs.State = Edit.None;
                        }
                        else
                        {
                            rs.DeleteStart = startX + startOld;
                            rs.DeleteEnd = o + startOld;
                            rs.AddStart = startY + startNew;
                            rs.AddEnd = n + startNew;
                        }
                        return rs;
                    }
                }
            }

            for (int j = -i; j <= i; j += 2)
            {
                var idx = j + half;
                int o;
                if (j == -i || (j != i && reverse[idx + 1] <= reverse[idx - 1]))
                {
                    o = reverse[idx + 1] - 1;
                    rs.State = Edit.DeletedLeft;
                }
                else
                {
                    o = reverse[idx - 1];
                    rs.State = Edit.AddedLeft;
                }

                var n = o - (j + delta);

                var endX = o;
                var endY = n;
                while (o > 0 && n > 0 && chunksOld[startOld + o - 1].Hash == chunksNew[startNew + n - 1].Hash)
                {
                    o--;
                    n--;
                }

                reverse[idx] = o;

                if (deltaEven && j + delta >= -i && j + delta <= i)
                {
                    var forIdx = (j + delta) + half;
                    var forOld = forward[forIdx];
                    int forNew = forOld - (j + delta);
                    if (forOld >= o && forNew >= n)
                    {
                        if (i == 0)
                        {
                            rs.State = Edit.None;
                        }
                        else
                        {
                            rs.DeleteStart = o + startOld;
                            rs.DeleteEnd = endX + startOld;
                            rs.AddStart = n + startNew;
                            rs.AddEnd = endY + startNew;
                        }
                        return rs;
                    }
                }
            }
        }

        rs.State = Edit.None;
        return rs;
    }

    /// <summary>チャンクリストにチャンクを追加する。ハッシュが未登録の場合は新規登録する。</summary>
    private static void AddChunk(List<Chunk> chunks, Dictionary<string, int> hashes, string data, int start)
    {
        if (!hashes.TryGetValue(data, out var hash))
        {
            hash = hashes.Count;
            hashes.Add(data, hash);
        }
        chunks.Add(new Chunk(hash, start, data.Length));
    }
}
