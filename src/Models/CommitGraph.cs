using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Media;

namespace Komorebi.Models
{
    /// <summary>
    ///     コミットグラフのレイアウト情報を保持するレコード。
    /// </summary>
    /// <param name="StartY">描画開始のY座標。</param>
    /// <param name="ClipWidth">クリッピング幅。</param>
    /// <param name="RowHeight">1行の高さ。</param>
    public record CommitGraphLayout(double StartY, double ClipWidth, double RowHeight);

    /// <summary>
    ///     コミット履歴のグラフ構造を解析・保持するクラス。
    ///     パス、リンク、ドットの描画要素を生成する。
    /// </summary>
    public class CommitGraph
    {
        /// <summary>
        ///     グラフ描画に使用するペンのリスト。
        /// </summary>
        public static List<Pen> Pens { get; } = [];

        /// <summary>
        ///     デフォルトの色でペンを初期化する。
        /// </summary>
        /// <param name="thickness">線の太さ（デフォルト: 2）。</param>
        public static void SetDefaultPens(double thickness = 2)
        {
            SetPens(s_defaultPenColors, thickness);
        }

        /// <summary>
        ///     指定した色と太さでペンを設定する。
        /// </summary>
        /// <param name="colors">使用する色のリスト。</param>
        /// <param name="thickness">線の太さ。</param>
        public static void SetPens(List<Color> colors, double thickness)
        {
            Pens.Clear();

            // 各色からペンを作成
            foreach (var c in colors)
                Pens.Add(new Pen(c.ToUInt32(), thickness));

            s_penCount = colors.Count;
        }

        /// <summary>
        ///     グラフ上のブランチパス（折れ線）を表すクラス。
        /// </summary>
        /// <param name="color">パスの色インデックス。</param>
        /// <param name="isMerged">マージ済みかどうか。</param>
        public class Path(int color, bool isMerged)
        {
            /// <summary>
            ///     パスを構成する座標点のリスト。
            /// </summary>
            public List<Point> Points { get; } = [];

            /// <summary>
            ///     パスの色インデックス。
            /// </summary>
            public int Color { get; } = color;

            /// <summary>
            ///     マージ済みパスかどうか。
            /// </summary>
            public bool IsMerged { get; } = isMerged;
        }

        /// <summary>
        ///     グラフ上のベジェ曲線リンク（マージ線）を表すクラス。
        /// </summary>
        public class Link
        {
            /// <summary>開始点。</summary>
            public Point Start;
            /// <summary>制御点。</summary>
            public Point Control;
            /// <summary>終了点。</summary>
            public Point End;
            /// <summary>色インデックス。</summary>
            public int Color;
            /// <summary>マージ済みかどうか。</summary>
            public bool IsMerged;
        }

        /// <summary>
        ///     コミットドットの種類を表す列挙型。
        /// </summary>
        public enum DotType
        {
            /// <summary>通常のコミット。</summary>
            Default,
            /// <summary>現在のHEADコミット。</summary>
            Head,
            /// <summary>マージコミット。</summary>
            Merge,
        }

        /// <summary>
        ///     グラフ上のコミットドット（点）を表すクラス。
        /// </summary>
        public class Dot
        {
            /// <summary>ドットの種類。</summary>
            public DotType Type;
            /// <summary>ドットの中心座標。</summary>
            public Point Center;
            /// <summary>色インデックス。</summary>
            public int Color;
            /// <summary>マージ済みかどうか。</summary>
            public bool IsMerged;
        }

        /// <summary>
        ///     グラフ内の全パス。
        /// </summary>
        public List<Path> Paths { get; } = [];

        /// <summary>
        ///     グラフ内の全リンク（マージ線）。
        /// </summary>
        public List<Link> Links { get; } = [];

        /// <summary>
        ///     グラフ内の全コミットドット。
        /// </summary>
        public List<Dot> Dots { get; } = [];

        /// <summary>
        ///     コミットリストからグラフ構造を解析・生成する。
        /// </summary>
        /// <param name="commits">コミットのリスト。</param>
        /// <param name="firstParentOnlyEnabled">最初の親のみ表示モードかどうか。</param>
        /// <returns>解析済みのコミットグラフ。</returns>
        public static CommitGraph Parse(List<Commit> commits, bool firstParentOnlyEnabled)
        {
            // グラフ描画の単位サイズ定数
            const double unitWidth = 12;
            const double halfWidth = 6;
            const double unitHeight = 1;
            const double halfHeight = 0.5;

            var temp = new CommitGraph();
            var unsolved = new List<PathHelper>();  // 未解決（続行中）のパス
            var ended = new List<PathHelper>();      // 終了したパス
            // 未解決パスのNext→PathHelper検索用Dictionary（旧: List.Find()のO(n) → O(1)）
            var unsolvedByNext = new Dictionary<string, PathHelper>(StringComparer.Ordinal);
            var offsetY = -halfHeight;
            var colorPicker = new ColorPicker();

            foreach (var commit in commits)
            {
                PathHelper major = null;
                var isMerged = commit.IsMerged;

                // Update current y offset
                offsetY += unitHeight;

                // Find first curves that links to this commit and marks others that links to this commit ended.
                var offsetX = 4 - halfWidth;
                var maxOffsetOld = unsolved.Count > 0 ? unsolved[^1].LastX : offsetX + unitWidth;
                foreach (var l in unsolved)
                {
                    if (l.Next.Equals(commit.SHA, StringComparison.Ordinal))
                    {
                        if (major == null)
                        {
                            offsetX += unitWidth;
                            major = l;

                            if (commit.Parents.Count > 0)
                            {
                                major.Next = commit.Parents[0];
                                major.Goto(offsetX, offsetY, halfHeight);
                            }
                            else
                            {
                                major.End(offsetX, offsetY, halfHeight);
                                ended.Add(l);
                            }
                        }
                        else
                        {
                            l.End(major.LastX, offsetY, halfHeight);
                            ended.Add(l);
                        }

                        isMerged = isMerged || l.IsMerged;
                    }
                    else
                    {
                        offsetX += unitWidth;
                        l.Pass(offsetX, offsetY, halfHeight);
                    }
                }

                // Remove ended curves from unsolved
                // HashSetでO(1)判定 → RemoveAllでO(n)一括削除（旧: List.Remove()をm回でO(n*m)）
                if (ended.Count > 0)
                {
                    foreach (var l in ended)
                        colorPicker.Recycle(l.Path.Color);

                    var endedSet = new HashSet<PathHelper>(ended);
                    unsolved.RemoveAll(endedSet.Contains);
                    ended.Clear();
                }

                // If no path found, create new curve for branch head
                // Otherwise, create new curve for new merged commit
                if (major == null)
                {
                    offsetX += unitWidth;

                    if (commit.Parents.Count > 0)
                    {
                        major = new PathHelper(commit.Parents[0], isMerged, colorPicker.Next(), new Point(offsetX, offsetY));
                        unsolved.Add(major);
                        temp.Paths.Add(major.Path);
                    }
                }
                else if (isMerged && !major.IsMerged && commit.Parents.Count > 0)
                {
                    major.ReplaceMerged();
                    temp.Paths.Add(major.Path);
                }

                // Calculate link position of this commit.
                var position = new Point(major?.LastX ?? offsetX, offsetY);
                var dotColor = major?.Path.Color ?? 0;
                var anchor = new Dot() { Center = position, Color = dotColor, IsMerged = isMerged };
                if (commit.IsCurrentHead)
                    anchor.Type = DotType.Head;
                else if (commit.Parents.Count > 1)
                    anchor.Type = DotType.Merge;
                else
                    anchor.Type = DotType.Default;
                temp.Dots.Add(anchor);

                // Deal with other parents (the first parent has been processed)
                // Dictionaryで親コミットをO(1)検索（旧: List.Find()はO(n)）
                // 親の検索前にDictionaryを再構築（unsolved変更後のため）
                if (!firstParentOnlyEnabled && commit.Parents.Count > 1)
                {
                    unsolvedByNext.Clear();
                    foreach (var u in unsolved)
                    {
                        // 同じNextを持つ複数のPathLink（複数ブランチが同じ祖先に収束するケース）を保持する
                        // Dictionaryは上書きするため、最初のマッチのみ保持（旧List.Findと同じ挙動）
                        if (!unsolvedByNext.ContainsKey(u.Next))
                            unsolvedByNext[u.Next] = u;
                    }
                }

                if (!firstParentOnlyEnabled)
                {
                    for (int j = 1; j < commit.Parents.Count; j++)
                    {
                        var parentHash = commit.Parents[j];
                        unsolvedByNext.TryGetValue(parentHash, out var parent);
                        if (parent != null)
                        {
                            if (isMerged && !parent.IsMerged)
                            {
                                parent.Goto(parent.LastX, offsetY + halfHeight, halfHeight);
                                parent.ReplaceMerged();
                                temp.Paths.Add(parent.Path);
                            }

                            temp.Links.Add(new Link
                            {
                                Start = position,
                                End = new Point(parent.LastX, offsetY + halfHeight),
                                Control = new Point(parent.LastX, position.Y),
                                Color = parent.Path.Color,
                                IsMerged = isMerged,
                            });
                        }
                        else
                        {
                            offsetX += unitWidth;

                            // Create new curve for parent commit that not includes before
                            var l = new PathHelper(parentHash, isMerged, colorPicker.Next(), position, new Point(offsetX, position.Y + halfHeight));
                            unsolved.Add(l);
                            temp.Paths.Add(l.Path);
                        }
                    }
                }

                // Margins & merge state (used by Views.Histories).
                commit.IsMerged = isMerged;
                commit.Color = dotColor;
                commit.LeftMargin = Math.Max(offsetX, maxOffsetOld) + halfWidth + 2;
            }

            // Deal with curves haven't ended yet.
            for (var i = 0; i < unsolved.Count; i++)
            {
                var path = unsolved[i];
                var endY = (commits.Count - 0.5) * unitHeight;

                if (path.Path.Points.Count == 1 && Math.Abs(path.Path.Points[0].Y - endY) < 0.0001)
                    continue;

                path.End((i + 0.5) * unitWidth + 4, endY + halfHeight, halfHeight);
            }
            unsolved.Clear();

            return temp;
        }

        /// <summary>
        ///     グラフの色をキューで管理し、色の割り当てとリサイクルを行うクラス。
        ///     HashSetで重複チェックをO(1)に改善（旧: Queue.Contains()はO(n)）。
        /// </summary>
        private class ColorPicker
        {
            /// <summary>
            ///     次の色インデックスを取得する。キューが空の場合は全色を補充する。
            /// </summary>
            /// <returns>色インデックス。</returns>
            public int Next()
            {
                // キューが空の場合、全色を再投入
                if (_colorsQueue.Count == 0)
                {
                    for (var i = 0; i < s_penCount; i++)
                    {
                        _colorsQueue.Enqueue(i);
                        _colorSet.Add(i);
                    }
                }

                var color = _colorsQueue.Dequeue();
                _colorSet.Remove(color);
                return color;
            }

            /// <summary>
            ///     使用済みの色インデックスをキューに戻す。
            ///     HashSetでO(1)の重複チェック（旧: Queue.Contains()はO(n)）。
            /// </summary>
            /// <param name="idx">リサイクルする色インデックス。</param>
            public void Recycle(int idx)
            {
                if (_colorSet.Add(idx))
                    _colorsQueue.Enqueue(idx);
            }

            private readonly Queue<int> _colorsQueue = new();
            private readonly HashSet<int> _colorSet = [];
        }

        /// <summary>
        ///     パスの構築を補助するクラス。コミット間の経路を追跡する。
        /// </summary>
        private class PathHelper
        {
            /// <summary>現在構築中のパス。</summary>
            public Path Path { get; private set; }

            /// <summary>次に到達すべきコミットのSHA。</summary>
            public string Next { get; set; }

            /// <summary>最後のX座標。</summary>
            public double LastX { get; private set; }

            /// <summary>パスがマージ済みかどうか。</summary>
            public bool IsMerged => Path.IsMerged;

            /// <summary>
            ///     開始点を指定してPathHelperを初期化する。
            /// </summary>
            /// <param name="next">次のコミットSHA。</param>
            /// <param name="isMerged">マージ済みかどうか。</param>
            /// <param name="color">色インデックス。</param>
            /// <param name="start">開始座標。</param>
            public PathHelper(string next, bool isMerged, int color, Point start)
            {
                Next = next;
                LastX = start.X;
                _lastY = start.Y;

                Path = new Path(color, isMerged);
                Path.Points.Add(start);
            }

            /// <summary>
            ///     開始点と到達点を指定してPathHelperを初期化する。
            /// </summary>
            /// <param name="next">次のコミットSHA。</param>
            /// <param name="isMerged">マージ済みかどうか。</param>
            /// <param name="color">色インデックス。</param>
            /// <param name="start">開始座標。</param>
            /// <param name="to">到達座標。</param>
            public PathHelper(string next, bool isMerged, int color, Point start, Point to)
            {
                Next = next;
                LastX = to.X;
                _lastY = to.Y;

                Path = new Path(color, isMerged);
                Path.Points.Add(start);
                Path.Points.Add(to);
            }

            /// <summary>
            ///     この行を通過するパスを更新する（コミットなし）。
            /// </summary>
            /// <param name="x">X座標。</param>
            /// <param name="y">Y座標。</param>
            /// <param name="halfHeight">行の半分の高さ。</param>
            public void Pass(double x, double y, double halfHeight)
            {
                if (x > LastX)
                {
                    Add(LastX, _lastY);
                    Add(x, y - halfHeight);
                }
                else if (x < LastX)
                {
                    Add(LastX, y - halfHeight);
                    y += halfHeight;
                    Add(x, y);
                }

                LastX = x;
                _lastY = y;
            }

            /// <summary>
            ///     この行にコミットがあるが終了しないパスを更新する。
            /// </summary>
            /// <param name="x">X座標。</param>
            /// <param name="y">Y座標。</param>
            /// <param name="halfHeight">行の半分の高さ。</param>
            public void Goto(double x, double y, double halfHeight)
            {
                if (x > LastX)
                {
                    Add(LastX, _lastY);
                    Add(x, y - halfHeight);
                }
                else if (x < LastX)
                {
                    var minY = y - halfHeight;
                    if (minY > _lastY)
                        minY -= halfHeight;

                    Add(LastX, minY);
                    Add(x, y);
                }

                LastX = x;
                _lastY = y;
            }

            /// <summary>
            ///     この行にコミットがあり終了するパスを更新する。
            /// </summary>
            /// <param name="x">X座標。</param>
            /// <param name="y">Y座標。</param>
            /// <param name="halfHeight">行の半分の高さ。</param>
            public void End(double x, double y, double halfHeight)
            {
                if (x > LastX)
                {
                    Add(LastX, _lastY);
                    Add(x, y - halfHeight);
                }
                else if (x < LastX)
                {
                    Add(LastX, y - halfHeight);
                }

                Add(x, y);

                LastX = x;
                _lastY = y;
            }

            /// <summary>
            ///     現在のパスを終了し、終端から新しいマージ済みパスを開始する。
            /// </summary>
            public void ReplaceMerged()
            {
                var color = Path.Color;
                Add(LastX, _lastY);

                Path = new Path(color, true);
                Path.Points.Add(new Point(LastX, _lastY));
                _endY = 0;
            }

            private void Add(double x, double y)
            {
                if (_endY < y)
                {
                    Path.Points.Add(new Point(x, y));
                    _endY = y;
                }
            }

            private double _lastY = 0;
            private double _endY = 0;
        }

        private static int s_penCount = 0;
        private static readonly List<Color> s_defaultPenColors = [
            Colors.Orange,
            Colors.ForestGreen,
            Colors.Turquoise,
            Colors.Olive,
            Colors.Magenta,
            Colors.Red,
            Colors.Khaki,
            Colors.Lime,
            Colors.RoyalBlue,
            Colors.Teal,
        ];
    }
}
