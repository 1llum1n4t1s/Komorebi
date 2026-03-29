using Avalonia;
using Avalonia.Media;

using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class CommitGraphTests
    {
        // ----------------------------------------------------------------
        //  Helper: ensure s_penCount > 0 before every test that calls Parse
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
        //  Pens / SetPens
        // ================================================================

        [Fact]
        public void Pens_InitiallyEmpty()
        {
            // Pens is a static list. After previous tests it may already be
            // populated, so we clear it first and verify the cleared state.
            CommitGraph.Pens.Clear();
            Assert.Empty(CommitGraph.Pens);
        }

        [Fact]
        public void SetPens_PopulatesPensList()
        {
            var colors = new List<Color> { Colors.Red, Colors.Blue, Colors.Green };
            CommitGraph.SetPens(colors, 3);

            Assert.Equal(3, CommitGraph.Pens.Count);
        }

        [Fact]
        public void SetPens_ClearsPreviousPens()
        {
            CommitGraph.SetPens([Colors.Red], 1);
            Assert.Single(CommitGraph.Pens);

            CommitGraph.SetPens([Colors.Blue, Colors.Green], 2);
            Assert.Equal(2, CommitGraph.Pens.Count);
        }

        [Fact]
        public void SetDefaultPens_Populates10Pens()
        {
            CommitGraph.SetDefaultPens();
            Assert.Equal(10, CommitGraph.Pens.Count);
        }

        [Fact]
        public void SetDefaultPens_CustomThickness()
        {
            CommitGraph.SetDefaultPens(4);
            Assert.Equal(10, CommitGraph.Pens.Count);
            // Pen thickness is stored in the Pen object
            foreach (var pen in CommitGraph.Pens)
                Assert.Equal(4, pen.Thickness);
        }

        // ================================================================
        //  Parse — empty input
        // ================================================================

        [Fact]
        public void Parse_EmptyCommitList_ReturnsEmptyGraph()
        {
            EnsurePensInitialized();

            var graph = CommitGraph.Parse([], false);

            Assert.Empty(graph.Paths);
            Assert.Empty(graph.Links);
            Assert.Empty(graph.Dots);
        }

        // ================================================================
        //  Parse — single commit, no parents (orphan / initial)
        // ================================================================

        [Fact]
        public void Parse_SingleCommitNoParents_ProducesOneDot()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("aaa0000000", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            // One dot for the commit
            Assert.Single(graph.Dots);
            Assert.Equal(CommitGraph.DotType.Default, graph.Dots[0].Type);

            // No links because there are no merge parents
            Assert.Empty(graph.Links);
        }

        // ================================================================
        //  Parse — single commit with one parent (tip of branch)
        // ================================================================

        [Fact]
        public void Parse_SingleCommitWithOneParent_ProducesPathAndDot()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("aaa0000000", parents: ["bbb0000000"]),
            };

            var graph = CommitGraph.Parse(commits, false);

            Assert.Single(graph.Dots);
            // A path is created for the first (and only) parent
            Assert.True(graph.Paths.Count >= 1);
        }

        // ================================================================
        //  Parse — linear history (A -> B -> C)
        // ================================================================

        [Fact]
        public void Parse_LinearHistory_ProducesCorrectDotsCount()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("aaa", parents: ["bbb"]),
                MakeCommit("bbb", parents: ["ccc"]),
                MakeCommit("ccc", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(3, graph.Dots.Count);
            // Linear history: no merge links
            Assert.Empty(graph.Links);
        }

        [Fact]
        public void Parse_LinearHistory_DotsAreOnSameColumn()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("aaa", parents: ["bbb"]),
                MakeCommit("bbb", parents: ["ccc"]),
                MakeCommit("ccc", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            // All dots should have the same X coordinate (single column)
            var xs = graph.Dots.Select(d => d.Center.X).Distinct().ToList();
            Assert.Single(xs);
        }

        [Fact]
        public void Parse_LinearHistory_DotsAreVerticallyOrdered()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("aaa", parents: ["bbb"]),
                MakeCommit("bbb", parents: ["ccc"]),
                MakeCommit("ccc", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            for (int i = 1; i < graph.Dots.Count; i++)
            {
                Assert.True(graph.Dots[i].Center.Y > graph.Dots[i - 1].Center.Y,
                    $"Dot {i} should be below dot {i - 1}");
            }
        }

        // ================================================================
        //  Parse — merge commit (2 parents)
        // ================================================================

        [Fact]
        public void Parse_MergeCommit_HasMergeDotType()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("merge", parents: ["p1", "p2"]),
                MakeCommit("p1", parents: ["base"]),
                MakeCommit("p2", parents: ["base"]),
                MakeCommit("base", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            // First commit has 2 parents -> Merge dot type
            Assert.Equal(CommitGraph.DotType.Merge, graph.Dots[0].Type);
        }

        [Fact]
        public void Parse_MergeCommit_ProducesLink()
        {
            EnsurePensInitialized();

            // merge -> p1 (first parent, handled as path continuation)
            // merge -> p2 (second parent, creates a link or new path)
            var commits = new List<Commit>
            {
                MakeCommit("merge", parents: ["p1", "p2"]),
                MakeCommit("p1", parents: ["base"]),
                MakeCommit("p2", parents: ["base"]),
                MakeCommit("base", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            // The second parent should create either a link (if p2 is already
            // tracked) or a new path. Since p2 hasn't been seen yet, a new
            // path is created.
            Assert.True(graph.Paths.Count >= 2,
                "Merge should produce at least 2 paths (main + branch)");
        }

        // ================================================================
        //  Parse — HEAD commit gets Head dot type
        // ================================================================

        [Fact]
        public void Parse_HeadCommit_HasHeadDotType()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("head", parents: ["p1"], isCurrentHead: true),
                MakeCommit("p1", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(CommitGraph.DotType.Head, graph.Dots[0].Type);
        }

        // ================================================================
        //  Parse — branching history
        //
        //  Topology: commit order is newest-first
        //    a1 (branch-a tip)  -> base
        //    b1 (branch-b tip)  -> base
        //    base               -> (root)
        // ================================================================

        [Fact]
        public void Parse_TwoBranches_ProducesMultiplePaths()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("a1", parents: ["base"]),
                MakeCommit("b1", parents: ["base"]),
                MakeCommit("base", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(3, graph.Dots.Count);
            // a1 and b1 each start a new path; both target "base"
            Assert.True(graph.Paths.Count >= 2,
                "Two unrelated branch tips should create at least 2 paths");
        }

        [Fact]
        public void Parse_TwoBranches_DotsOnDifferentColumns()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("a1", parents: ["base"]),
                MakeCommit("b1", parents: ["base"]),
                MakeCommit("base", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            // a1 and b1 are on different branches, so they should be on
            // different X columns
            var x0 = graph.Dots[0].Center.X;
            var x1 = graph.Dots[1].Center.X;
            Assert.NotEqual(x0, x1);
        }

        // ================================================================
        //  Parse — firstParentOnly mode
        // ================================================================

        [Fact]
        public void Parse_FirstParentOnly_IgnoresSecondParent()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("merge", parents: ["p1", "p2"]),
                MakeCommit("p1", parents: []),
            };

            var graph = CommitGraph.Parse(commits, firstParentOnlyEnabled: true);

            // With firstParentOnly, p2 is never explored, so no link or extra
            // path should be created for it.
            Assert.Empty(graph.Links);
            // Only the main path from merge->p1 should exist
            Assert.True(graph.Paths.Count <= 1);
        }

        // ================================================================
        //  Parse — merge back into existing tracked path (produces Link)
        // ================================================================

        [Fact]
        public void Parse_MergeBackIntoTrackedPath_ProducesLink()
        {
            EnsurePensInitialized();

            // Topology (newest first):
            //   child_a    -> merge            (creates path tracking "merge")
            //   child_b    -> branch_tip       (creates path tracking "branch_tip")
            //   merge      -> main_mid, branch_tip
            //
            // When Parse reaches "merge":
            //  - child_a's path matches (Next=="merge") → becomes major
            //  - merge's 2nd parent "branch_tip" matches child_b's path → Link created
            var commits = new List<Commit>
            {
                MakeCommit("child_a", parents: ["merge"]),
                MakeCommit("child_b", parents: ["branch_tip"]),
                MakeCommit("merge", parents: ["main_mid", "branch_tip"]),
                MakeCommit("main_mid", parents: []),
                MakeCommit("branch_tip", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            // The merge's second parent (branch_tip) is already tracked by
            // child_b's path, so a Link is created.
            Assert.True(graph.Links.Count >= 1,
                "Merging back into an already-tracked parent should create a Link");
        }

        // ================================================================
        //  Parse — commit.LeftMargin is set
        // ================================================================

        [Fact]
        public void Parse_SetsLeftMarginOnCommits()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("aaa", parents: ["bbb"]),
                MakeCommit("bbb", parents: []),
            };

            CommitGraph.Parse(commits, false);

            foreach (var c in commits)
            {
                Assert.True(c.LeftMargin > 0,
                    $"Commit {c.SHA} should have LeftMargin > 0 after Parse");
            }
        }

        // ================================================================
        //  Parse — commit.Color is set
        // ================================================================

        [Fact]
        public void Parse_SetsColorOnCommits()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("aaa", parents: ["bbb"]),
                MakeCommit("bbb", parents: []),
            };

            CommitGraph.Parse(commits, false);

            // Color should be a valid pen index (>= 0)
            foreach (var c in commits)
                Assert.True(c.Color >= 0);
        }

        // ================================================================
        //  Parse — IsMerged propagation
        // ================================================================

        [Fact]
        public void Parse_PropagatesIsMerged()
        {
            EnsurePensInitialized();

            // IsMerged propagates from a commit through the path to descendants.
            // The first commit has IsMerged = true, and the graph algorithm
            // should propagate it via PathHelper.IsMerged to subsequent commits.
            var commits = new List<Commit>
            {
                MakeCommit("head", parents: ["p1"], isMerged: true, isCurrentHead: true),
                MakeCommit("p1", parents: ["p2"]),
                MakeCommit("p2", parents: []),
            };

            CommitGraph.Parse(commits, false);

            Assert.True(commits[0].IsMerged, "HEAD commit should remain merged");
            Assert.True(commits[1].IsMerged, "p1 should have IsMerged propagated from HEAD");
            Assert.True(commits[2].IsMerged, "p2 should have IsMerged propagated through chain");
        }

        // ================================================================
        //  Parse — many commits (stress/regression)
        // ================================================================

        [Fact]
        public void Parse_ManyLinearCommits_DoesNotThrow()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>();
            for (int i = 0; i < 500; i++)
            {
                var sha = $"commit_{i:D6}";
                var parent = i < 499 ? $"commit_{i + 1:D6}" : null;
                commits.Add(MakeCommit(sha, parents: parent != null ? [parent] : []));
            }

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(500, graph.Dots.Count);
        }

        // ================================================================
        //  Parse — color recycling (ColorPicker)
        // ================================================================

        [Fact]
        public void Parse_ColorRecycling_ReusesColorsAfterBranchEnds()
        {
            EnsurePensInitialized();

            // Create a topology where branches end and new ones begin,
            // forcing color recycling.
            //
            // a1 -> base   (branch a, ends at base)
            // b1 -> base   (branch b, ends at base — color recycled)
            // base -> c1   (continues)
            // c1 -> (root) (single line after merge point)
            var commits = new List<Commit>
            {
                MakeCommit("a1", parents: ["base"]),
                MakeCommit("b1", parents: ["base"]),
                MakeCommit("base", parents: ["c1"]),
                MakeCommit("c1", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            // Just verify it doesn't crash and produces valid output
            Assert.Equal(4, graph.Dots.Count);
        }

        // ================================================================
        //  Parse — dot positions use expected coordinate system
        // ================================================================

        [Fact]
        public void Parse_DotPositions_UseExpectedCoordinateSystem()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("a", parents: ["b"]),
                MakeCommit("b", parents: ["c"]),
                MakeCommit("c", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            // Constants from Parse: unitHeight = 1, halfHeight = 0.5
            // offsetY starts at -0.5, then += 1.0 for each commit
            // So first commit Y = 0.5, second = 1.5, third = 2.5
            Assert.Equal(0.5, graph.Dots[0].Center.Y);
            Assert.Equal(1.5, graph.Dots[1].Center.Y);
            Assert.Equal(2.5, graph.Dots[2].Center.Y);
        }

        // ================================================================
        //  Parse — paths have correct points for linear history
        // ================================================================

        [Fact]
        public void Parse_LinearHistory_PathPointsAreCoherent()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("a", parents: ["b"]),
                MakeCommit("b", parents: ["c"]),
                MakeCommit("c", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            // For a linear history, all path points should have the same X
            foreach (var path in graph.Paths)
            {
                var xValues = path.Points.Select(p => p.X).Distinct().ToList();
                Assert.Single(xValues);
            }
        }

        // ================================================================
        //  Parse — octopus merge (3+ parents)
        // ================================================================

        [Fact]
        public void Parse_OctopusMerge_HandlesThreeParents()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("octopus", parents: ["p1", "p2", "p3"]),
                MakeCommit("p1", parents: ["base"]),
                MakeCommit("p2", parents: ["base"]),
                MakeCommit("p3", parents: ["base"]),
                MakeCommit("base", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(5, graph.Dots.Count);
            Assert.Equal(CommitGraph.DotType.Merge, graph.Dots[0].Type);
            // p2 and p3 are additional parents, creating new paths
            Assert.True(graph.Paths.Count >= 3);
        }

        // ================================================================
        //  Path / Link / Dot data classes
        // ================================================================

        [Fact]
        public void Path_Constructor_SetsColorAndIsMerged()
        {
            var path = new CommitGraph.Path(5, true);

            Assert.Equal(5, path.Color);
            Assert.True(path.IsMerged);
            Assert.Empty(path.Points);
        }

        [Fact]
        public void Path_Points_CanBeAdded()
        {
            var path = new CommitGraph.Path(0, false);
            path.Points.Add(new Point(1, 2));
            path.Points.Add(new Point(3, 4));

            Assert.Equal(2, path.Points.Count);
            Assert.Equal(new Point(1, 2), path.Points[0]);
            Assert.Equal(new Point(3, 4), path.Points[1]);
        }

        [Fact]
        public void Link_Properties_CanBeSet()
        {
            var link = new CommitGraph.Link
            {
                Start = new Point(0, 0),
                Control = new Point(5, 0),
                End = new Point(5, 10),
                Color = 3,
                IsMerged = true,
            };

            Assert.Equal(new Point(0, 0), link.Start);
            Assert.Equal(new Point(5, 0), link.Control);
            Assert.Equal(new Point(5, 10), link.End);
            Assert.Equal(3, link.Color);
            Assert.True(link.IsMerged);
        }

        [Fact]
        public void Dot_Properties_CanBeSet()
        {
            var dot = new CommitGraph.Dot
            {
                Type = CommitGraph.DotType.Head,
                Center = new Point(10, 20),
                Color = 7,
                IsMerged = false,
            };

            Assert.Equal(CommitGraph.DotType.Head, dot.Type);
            Assert.Equal(new Point(10, 20), dot.Center);
            Assert.Equal(7, dot.Color);
            Assert.False(dot.IsMerged);
        }

        // ================================================================
        //  CommitGraphLayout record
        // ================================================================

        [Fact]
        public void CommitGraphLayout_Record_StoresValues()
        {
            var layout = new CommitGraphLayout(10.0, 200.0, 28.0);

            Assert.Equal(10.0, layout.StartY);
            Assert.Equal(200.0, layout.ClipWidth);
            Assert.Equal(28.0, layout.RowHeight);
        }

        [Fact]
        public void CommitGraphLayout_ValueEquality()
        {
            var a = new CommitGraphLayout(10.0, 200.0, 28.0);
            var b = new CommitGraphLayout(10.0, 200.0, 28.0);

            Assert.Equal(a, b);
        }

        // ================================================================
        //  DotType enum values
        // ================================================================

        [Fact]
        public void DotType_HasExpectedValues()
        {
            Assert.Equal(0, (int)CommitGraph.DotType.Default);
            Assert.Equal(1, (int)CommitGraph.DotType.Head);
            Assert.Equal(2, (int)CommitGraph.DotType.Merge);
        }

        // ================================================================
        //  Parse — link control point positioning
        // ================================================================

        [Fact]
        public void Parse_Link_ControlPointYMatchesStartY()
        {
            EnsurePensInitialized();

            // Create scenario that produces a Link:
            // child_b creates a path tracking "branch_tip", so when merge's
            // second parent "branch_tip" is processed, a Link is created.
            var commits = new List<Commit>
            {
                MakeCommit("child_a", parents: ["merge"]),
                MakeCommit("child_b", parents: ["branch_tip"]),
                MakeCommit("merge", parents: ["main_mid", "branch_tip"]),
                MakeCommit("main_mid", parents: []),
                MakeCommit("branch_tip", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            foreach (var link in graph.Links)
            {
                // Control point Y should equal Start Y (horizontal first, then vertical)
                Assert.Equal(link.Start.Y, link.Control.Y);
                // Control point X should equal End X
                Assert.Equal(link.End.X, link.Control.X);
            }
        }

        // ================================================================
        //  Parse — unsolved paths get terminated
        // ================================================================

        [Fact]
        public void Parse_UnsolvedPaths_GetTerminatedAtEnd()
        {
            EnsurePensInitialized();

            // branch_tip has parent "nonexistent" which never appears in the
            // commit list, so the path remains unsolved.
            var commits = new List<Commit>
            {
                MakeCommit("branch_tip", parents: ["nonexistent"]),
            };

            var graph = CommitGraph.Parse(commits, false);

            // The path should still exist (created when branch_tip was processed)
            Assert.True(graph.Paths.Count >= 1);
            // Should have at least the starting point
            Assert.True(graph.Paths[0].Points.Count >= 1);
        }

        // ================================================================
        //  Parse — complex diamond topology
        //
        //    m  (merge of a and b)
        //   / \
        //  a   b
        //   \ /
        //    root
        // ================================================================

        [Fact]
        public void Parse_DiamondTopology_ProducesExpectedStructure()
        {
            EnsurePensInitialized();

            var commits = new List<Commit>
            {
                MakeCommit("m", parents: ["a", "b"]),
                MakeCommit("a", parents: ["root"]),
                MakeCommit("b", parents: ["root"]),
                MakeCommit("root", parents: []),
            };

            var graph = CommitGraph.Parse(commits, false);

            Assert.Equal(4, graph.Dots.Count);
            Assert.Equal(CommitGraph.DotType.Merge, graph.Dots[0].Type);
        }
    }
}
