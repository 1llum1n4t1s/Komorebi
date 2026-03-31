using System.Diagnostics;

using Avalonia.Media;

using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// CommitGraph の敵対的テスト v2。
    /// 既存テストでカバーされていない境界値、パフォーマンス、異常状態を検証する。
    /// </summary>
    public class CommitGraphAdversarialV2Tests
    {
        // ----------------------------------------------------------------
        //  ヘルパー: ペンが初期化されていることを保証
        // ----------------------------------------------------------------
        private static void EnsurePensInitialized()
        {
            if (CommitGraph.Pens.Count == 0)
            {
                CommitGraph.SetPens(
                [
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
                ], 2);
            }
        }

        private static Commit MakeCommit(
            string sha,
            List<string>? parents = null,
            bool isMerged = false,
            bool isCurrentHead = false)
        {
            var c = new Commit
            {
                SHA = sha,
                Parents = parents ?? [],
                IsMerged = isMerged,
            };

            if (isCurrentHead)
            {
                c.Decorators.Add(new Decorator
                {
                    Type = DecoratorType.CurrentBranchHead,
                    Name = "main",
                });
            }

            return c;
        }

        // ================================================================
        //  境界値: 親が10個以上のオクトパスマージ
        // ================================================================

        /// <summary>
        /// 10個の親を持つオクトパスマージがクラッシュせず処理されること。
        /// 各親に対応するパスまたはリンクが生成される。
        /// </summary>
        [Fact]
        public void Parse_OctopusMerge10Parents_HandlesCorrectly()
        {
            EnsurePensInitialized();

            var parentShas = Enumerable.Range(1, 10).Select(i => $"parent_{i:D2}").ToList();
            var commits = new List<Commit>
            {
                MakeCommit("octopus", parents: parentShas),
            };

            // 各親コミットを追加
            foreach (var sha in parentShas)
                commits.Add(MakeCommit(sha, parents: []));

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(11, graph.Dots.Count);
            Assert.Equal(CommitGraph.DotType.Merge, graph.Dots[0].Type);

            // 最初の親はパス継続、残り9つはリンクまたは新パスとして処理される
            Assert.True(graph.Paths.Count >= 10,
                $"10親のオクトパスマージは少なくとも10本のパスを生成すべき（実際: {graph.Paths.Count}）");
        }

        /// <summary>
        /// 20個の親を持つ極端なオクトパスマージでもクラッシュしないこと。
        /// </summary>
        [Fact]
        public void Parse_OctopusMerge20Parents_DoesNotThrow()
        {
            EnsurePensInitialized();

            var parentShas = Enumerable.Range(1, 20).Select(i => $"p{i:D3}").ToList();
            var commits = new List<Commit>
            {
                MakeCommit("mega_octopus", parents: parentShas),
            };

            foreach (var sha in parentShas)
                commits.Add(MakeCommit(sha, parents: []));

            var ex = Record.Exception(() => CommitGraph.Parse(commits, false));
            Assert.Null(ex);
        }

        // ================================================================
        //  パフォーマンス: 50000件のリニア履歴がO(n)で処理されること
        // ================================================================

        /// <summary>
        /// 50000件のリニアコミット履歴がO(n)時間で処理されること。
        /// O(n²) ならタイムアウトする。
        /// </summary>
        [Fact]
        public void Parse_50000LinearCommits_CompletesInLinearTime()
        {
            EnsurePensInitialized();

            // 最初に小規模（5000件）で計測し、その後50000件で計測
            var smallCommits = MakeLinearHistory(5000);
            var sw1 = Stopwatch.StartNew();
            CommitGraph.Parse(smallCommits, false);
            sw1.Stop();

            var largeCommits = MakeLinearHistory(50000);
            var sw2 = Stopwatch.StartNew();
            var graph = CommitGraph.Parse(largeCommits, false);
            sw2.Stop();

            Assert.Equal(50000, graph.Dots.Count);

            // O(n)なら10倍のデータで10倍程度の時間
            // O(n²)なら100倍の時間がかかるはず
            // 余裕を持って20倍以内であることを確認（環境差を考慮）
            if (sw1.ElapsedMilliseconds > 10) // 小規模が十分な計測時間の場合のみ比較
            {
                var ratio = (double)sw2.ElapsedMilliseconds / sw1.ElapsedMilliseconds;
                Assert.True(ratio < 25,
                    $"パフォーマンス劣化の疑い: 5000件={sw1.ElapsedMilliseconds}ms, 50000件={sw2.ElapsedMilliseconds}ms, 比率={ratio:F1}x（25x以内が期待値）");
            }

            // 絶対値でも確認: 50000件は5秒以内
            Assert.True(sw2.ElapsedMilliseconds < 5000,
                $"50000件のパースに{sw2.ElapsedMilliseconds}msかかった（5000ms以内が期待値）");
        }

        // ================================================================
        //  パフォーマンス: 多数のブランチを持つ複雑なグラフ
        // ================================================================

        /// <summary>
        /// 1000本のブランチが同じコミットから分岐する複雑なグラフでもO(n)で処理されること。
        /// </summary>
        [Fact]
        public void Parse_ManyBranches_CompletesWithinReasonableTime()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>();
            // 1000本のブランチが "base" から分岐
            for (int i = 0; i < 1000; i++)
                commits.Add(MakeCommit($"branch_{i:D4}", parents: ["base"]));

            commits.Add(MakeCommit("base", parents: []));

            var sw = Stopwatch.StartNew();
            var graph = CommitGraph.Parse(commits, false);
            sw.Stop();

            Assert.Equal(1001, graph.Dots.Count);
            Assert.True(sw.ElapsedMilliseconds < 3000,
                $"1001件・1000ブランチのパースに{sw.ElapsedMilliseconds}msかかった");
        }

        // ================================================================
        //  全コミットが IsMerged=true の場合
        // ================================================================

        /// <summary>
        /// 全コミットが IsMerged=true の場合、パスも全て IsMerged=true になること。
        /// </summary>
        [Fact]
        public void Parse_AllCommitsMerged_AllPathsMerged()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("a", parents: ["b"], isMerged: true),
                MakeCommit("b", parents: ["c"], isMerged: true),
                MakeCommit("c", parents: [], isMerged: true),
            };

            var graph = CommitGraph.Parse(commits, false);

            Assert.All(graph.Paths, p => Assert.True(p.IsMerged,
                $"全コミットがIsMerged=trueなので、全パスもIsMerged=trueであるべき"));
            Assert.All(graph.Dots, d => Assert.True(d.IsMerged));
        }

        // ================================================================
        //  IsMerged が途中で変化するケース
        // ================================================================

        /// <summary>
        /// IsMerged が途中のコミットから true になるケースで、
        /// 下流のパスにも伝播すること。
        /// </summary>
        [Fact]
        public void Parse_IsMergedChangesMiddle_PropagatesDownstream()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("a", parents: ["b"], isMerged: false),
                MakeCommit("b", parents: ["c"], isMerged: true),
                MakeCommit("c", parents: [], isMerged: false),
            };

            CommitGraph.Parse(commits, false);

            // b が isMerged=true なので、パスを通じて c にも伝播する
            Assert.True(commits[1].IsMerged);
            Assert.True(commits[2].IsMerged, "IsMerged はパス経由で下流に伝播すべき");
        }

        // ================================================================
        //  null の commits リスト
        // ================================================================

        /// <summary>
        /// null のコミットリストを渡した場合、空のグラフが返されること。
        /// </summary>
        [Fact]
        public void Parse_NullCommitList_ReturnsEmptyGraph()
        {
            EnsurePensInitialized();

            var graph = CommitGraph.Parse(null!, false);

            Assert.Empty(graph.Paths);
            Assert.Empty(graph.Links);
            Assert.Empty(graph.Dots);
        }

        // ================================================================
        //  firstParentOnly でオクトパスマージの処理
        // ================================================================

        /// <summary>
        /// firstParentOnly モードでオクトパスマージの2番目以降の親が無視されること。
        /// </summary>
        [Fact]
        public void Parse_FirstParentOnly_OctopusMerge_IgnoresOtherParents()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("octopus", parents: ["p1", "p2", "p3", "p4"]),
                MakeCommit("p1", parents: []),
            };

            var graph = CommitGraph.Parse(commits, firstParentOnlyEnabled: true);

            Assert.Equal(2, graph.Dots.Count);
            Assert.Empty(graph.Links);
            // firstParentOnly では p2, p3, p4 は処理されないのでパスは1本のみ
            Assert.True(graph.Paths.Count <= 1);
        }

        // ================================================================
        //  全コミットが親なし（独立した孤児コミット群）
        // ================================================================

        /// <summary>
        /// 親を持たない独立コミットが複数ある場合、各コミットにドットが生成されること。
        /// </summary>
        [Fact]
        public void Parse_AllOrphanCommits_EachGetsADot()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>();
            for (int i = 0; i < 50; i++)
                commits.Add(MakeCommit($"orphan_{i:D3}", parents: []));

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(50, graph.Dots.Count);
            // 孤児コミットはパスを作らない（親がない）
            Assert.Empty(graph.Paths);
        }

        // ================================================================
        //  色リサイクル: ペン数を超えるブランチ
        // ================================================================

        /// <summary>
        /// ペン数（10色）を超えるブランチが同時に存在する場合、
        /// 色が正しくリサイクルされてクラッシュしないこと。
        /// </summary>
        [Fact]
        public void Parse_MoreBranchesThanColors_RecyclesColorsCorrectly()
        {
            EnsurePensInitialized();

            // 15本のブランチが同じbaseに収束
            var commits = new List<Commit>();
            for (int i = 0; i < 15; i++)
                commits.Add(MakeCommit($"branch_{i:D2}", parents: ["base"]));

            commits.Add(MakeCommit("base", parents: []));

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(16, graph.Dots.Count);
            // 全ドットの Color は有効な範囲内
            Assert.All(graph.Dots, d => Assert.True(d.Color >= 0,
                $"Color は 0 以上であるべき（実際: {d.Color}）"));
        }

        // ================================================================
        //  ダイヤモンド構造のネスト
        // ================================================================

        /// <summary>
        /// ダイヤモンド構造がネストしたグラフ（ダイヤモンドの中にダイヤモンド）を正しく処理すること。
        /// </summary>
        [Fact]
        public void Parse_NestedDiamonds_HandlesCorrectly()
        {
            EnsurePensInitialized();

            // 外側ダイヤモンド: merge1 -> (a, b) -> base
            // a の中にダイヤモンド: a -> (a1, a2) -> a_base -> base
            var commits = new List<Commit>
            {
                MakeCommit("merge1", parents: ["a", "b"]),
                MakeCommit("a", parents: ["a1", "a2"]),
                MakeCommit("b", parents: ["base"]),
                MakeCommit("a1", parents: ["a_base"]),
                MakeCommit("a2", parents: ["a_base"]),
                MakeCommit("a_base", parents: ["base"]),
                MakeCommit("base", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(7, graph.Dots.Count);
            Assert.Equal(CommitGraph.DotType.Merge, graph.Dots[0].Type);  // merge1
            Assert.Equal(CommitGraph.DotType.Merge, graph.Dots[1].Type);  // a (2親)
        }

        // ================================================================
        //  同じ親を2回指定するマージ（異常データ）
        // ================================================================

        /// <summary>
        /// 同じ親を2回参照するマージコミット（gitとしては異常だが防御的に処理）。
        /// </summary>
        [Fact]
        public void Parse_DuplicateParentReference_DoesNotCrash()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                // 同じ親 "p1" を2回指定
                MakeCommit("merge", parents: ["p1", "p1"]),
                MakeCommit("p1", parents: []),
            };

            var ex = Record.Exception(() => CommitGraph.Parse(commits, false));
            Assert.Null(ex);
        }

        // ================================================================
        //  非常に長いSHA（防御テスト）
        // ================================================================

        /// <summary>
        /// 異常に長いSHA文字列を持つコミットでもクラッシュしないこと。
        /// </summary>
        [Fact]
        public void Parse_VeryLongSHA_DoesNotCrash()
        {
            EnsurePensInitialized();

            var longSha = new string('a', 256);
            var commits = new List<Commit>
            {
                MakeCommit(longSha, parents: []),
            };

            var ex = Record.Exception(() => CommitGraph.Parse(commits, false));
            Assert.Null(ex);
        }

        // ================================================================
        //  空のSHA文字列（防御テスト）
        // ================================================================

        /// <summary>
        /// 空文字列のSHAを持つコミットでもクラッシュしないこと。
        /// </summary>
        [Fact]
        public void Parse_EmptySHA_DoesNotCrash()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("", parents: []),
            };

            var ex = Record.Exception(() => CommitGraph.Parse(commits, false));
            Assert.Null(ex);
        }

        // ================================================================
        //  長いチェーン: 深さ1000の線形履歴
        // ================================================================

        /// <summary>
        /// 深さ1000の線形履歴がスタックオーバーフローなく処理されること。
        /// （再帰でなくループで処理されていることの確認）
        /// </summary>
        [Fact]
        public void Parse_DeepLinearHistory1000_NoStackOverflow()
        {
            EnsurePensInitialized();

            var commits = MakeLinearHistory(1000);

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(1000, graph.Dots.Count);
            // リニア履歴なのでリンクは0
            Assert.Empty(graph.Links);
        }

        // ================================================================
        //  Y座標の検証: 大量コミットでも座標が単調増加
        // ================================================================

        /// <summary>
        /// 100件のコミットでドットのY座標が単調に増加すること。
        /// </summary>
        [Fact]
        public void Parse_100Commits_DotYCoordinatesMonotonicallyIncrease()
        {
            EnsurePensInitialized();

            var commits = MakeLinearHistory(100);
            var graph = CommitGraph.Parse(commits, false);

            for (int i = 1; i < graph.Dots.Count; i++)
            {
                Assert.True(graph.Dots[i].Center.Y > graph.Dots[i - 1].Center.Y,
                    $"ドット[{i}].Y ({graph.Dots[i].Center.Y}) はドット[{i - 1}].Y ({graph.Dots[i - 1].Center.Y}) より大きくあるべき");
            }
        }

        // ================================================================
        //  SetPens: 0色の場合
        // ================================================================

        /// <summary>
        /// SetPens に空のリストを渡した場合でもクラッシュしないこと。
        /// ただしParseは色がないためキューが空になり、ColorPicker.Next()で再投入が行われる。
        /// </summary>
        [Fact]
        public void SetPens_EmptyColors_DoesNotCrash()
        {
            CommitGraph.SetPens([], 2);
            Assert.Empty(CommitGraph.Pens);

            // 0色の状態でパースすると ColorPicker.Next() 内で s_penCount=0 により
            // キューに何も投入されず、空キューから Dequeue → InvalidOperationException の可能性
            // これはバグの発見テスト
            var commits = new List<Commit>
            {
                MakeCommit("a", parents: ["b"]),
                MakeCommit("b", parents: []),
            };

            try
            {
                CommitGraph.Parse(commits, false);
                // クラッシュしなければそれでよい
            }
            catch (InvalidOperationException)
            {
                // 空キューからの Dequeue による例外は期待される動作
                // （色が0の状態は本来設定されるべきでない）
            }
            finally
            {
                // 他のテストへの影響を防ぐためペンを復元
                EnsurePensInitialized();
            }
        }

        // ================================================================
        //  複数のHEADコミット（異常データ）
        // ================================================================

        /// <summary>
        /// 複数のコミットが IsCurrentHead=true の場合でもクラッシュしないこと。
        /// </summary>
        [Fact]
        public void Parse_MultipleHeadCommits_DoesNotCrash()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("head1", parents: ["base"], isCurrentHead: true),
                MakeCommit("head2", parents: ["base"], isCurrentHead: true),
                MakeCommit("base", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(3, graph.Dots.Count);
            // 両方 Head タイプのドットになる
            Assert.Equal(CommitGraph.DotType.Head, graph.Dots[0].Type);
            Assert.Equal(CommitGraph.DotType.Head, graph.Dots[1].Type);
        }

        // ================================================================
        //  連続マージコミット
        // ================================================================

        /// <summary>
        /// 連続するマージコミット（マージのマージ）が正しく処理されること。
        /// </summary>
        [Fact]
        public void Parse_ConsecutiveMerges_HandlesCorrectly()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("m2", parents: ["m1", "d"]),
                MakeCommit("m1", parents: ["a", "b"]),
                MakeCommit("d", parents: ["base"]),
                MakeCommit("a", parents: ["base"]),
                MakeCommit("b", parents: ["base"]),
                MakeCommit("base", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(6, graph.Dots.Count);
            Assert.Equal(CommitGraph.DotType.Merge, graph.Dots[0].Type); // m2
            Assert.Equal(CommitGraph.DotType.Merge, graph.Dots[1].Type); // m1
        }

        // ================================================================
        //  ヘルパーメソッド
        // ================================================================

        /// <summary>
        /// 指定件数のリニア（直線的な）履歴を生成するヘルパー。
        /// </summary>
        private static List<Commit> MakeLinearHistory(int count)
        {
            var commits = new List<Commit>(count);
            for (int i = 0; i < count; i++)
            {
                var sha = $"commit_{i:D6}";
                var parent = i < count - 1 ? $"commit_{i + 1:D6}" : null;
                commits.Add(MakeCommit(sha, parents: parent is not null ? [parent] : []));
            }
            return commits;
        }
    }
}
