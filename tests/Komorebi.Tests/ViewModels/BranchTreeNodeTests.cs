namespace Komorebi.Tests.ViewModels
{
    public class BranchTreeNodeTests
    {
        #region Helpers

        private static Komorebi.Models.Branch MakeLocalBranch(
            string name,
            ulong committerDate = 0,
            bool isCurrent = false,
            bool isDetachedHead = false)
        {
            return new Komorebi.Models.Branch
            {
                Name = name,
                FullName = $"refs/heads/{name}",
                IsLocal = true,
                IsCurrent = isCurrent,
                IsDetachedHead = isDetachedHead,
                CommitterDate = committerDate,
                Remote = string.Empty,
            };
        }

        private static Komorebi.Models.Branch MakeRemoteBranch(
            string name,
            string remote,
            ulong committerDate = 0)
        {
            return new Komorebi.Models.Branch
            {
                Name = name,
                FullName = $"refs/remotes/{remote}/{name}",
                IsLocal = false,
                IsCurrent = false,
                Remote = remote,
                CommitterDate = committerDate,
            };
        }

        private static Komorebi.Models.Remote MakeRemote(string name)
        {
            return new Komorebi.Models.Remote { Name = name, URL = $"https://example.com/{name}.git" };
        }

        #endregion

        // ------------------------------------------------------------------
        // Properties and initial state
        // ------------------------------------------------------------------

        [Fact]
        public void BranchTreeNode_DefaultProperties()
        {
            var node = new Komorebi.ViewModels.BranchTreeNode();

            Assert.Equal(string.Empty, node.Name);
            Assert.Equal(string.Empty, node.Path);
            Assert.Null(node.Backend);
            Assert.Equal(0, node.Depth);
            Assert.False(node.IsSelected);
            Assert.False(node.IsExpanded);
            Assert.NotNull(node.Children);
            Assert.Empty(node.Children);
            Assert.Equal(0, node.Counter);
        }

        [Fact]
        public void IsBranch_ReturnsTrueForBranchBackend()
        {
            // We cannot set Backend via public API on a node directly (it has a private setter).
            // Instead, run the builder and check the generated leaf node.
            var branches = new List<Komorebi.Models.Branch> { MakeLocalBranch("main") };
            var remotes = new List<Komorebi.Models.Remote>();
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, remotes, false);

            Assert.Single(builder.Locals);
            Assert.True(builder.Locals[0].IsBranch);
        }

        [Fact]
        public void IsCurrent_ReturnsTrueForCurrentBranch()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("main", isCurrent: true),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.True(builder.Locals[0].IsCurrent);
        }

        [Fact]
        public void BranchesCount_ReturnsFormattedString_WhenCounterPositive()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("feature/a"),
                MakeLocalBranch("feature/b"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            // "feature" folder node should be the only root
            Assert.Single(builder.Locals);
            var folder = builder.Locals[0];
            Assert.Equal(2, folder.Counter);
            Assert.Equal("(2)", folder.BranchesCount);
        }

        [Fact]
        public void BranchesCount_ReturnsEmpty_WhenCounterIsZero()
        {
            var node = new Komorebi.ViewModels.BranchTreeNode();
            Assert.Equal(string.Empty, node.BranchesCount);
        }

        // ------------------------------------------------------------------
        // Builder: Simple local branches (no nesting)
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_FlatLocalBranches_CreatesFlatList()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("main"),
                MakeLocalBranch("develop"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.Equal(2, builder.Locals.Count);
            // Sorted by name: "develop" < "main"
            Assert.Equal("develop", builder.Locals[0].Name);
            Assert.Equal("main", builder.Locals[1].Name);
        }

        // ------------------------------------------------------------------
        // Builder: Nested local branches (path separator)
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_NestedLocalBranches_CreatesTreeStructure()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("feature/login"),
                MakeLocalBranch("feature/signup"),
                MakeLocalBranch("main"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            // Roots: "feature" folder + "main" leaf
            Assert.Equal(2, builder.Locals.Count);

            // Folders sort before branch leaves in name sort (folder has no Branch backend)
            var featureFolder = builder.Locals[0];
            Assert.Equal("feature", featureFolder.Name);
            Assert.False(featureFolder.IsBranch);
            Assert.Equal(2, featureFolder.Counter);
            Assert.Equal(2, featureFolder.Children.Count);

            // Children sorted by name
            Assert.Equal("login", featureFolder.Children[0].Name);
            Assert.True(featureFolder.Children[0].IsBranch);
            Assert.Equal("signup", featureFolder.Children[1].Name);
            Assert.True(featureFolder.Children[1].IsBranch);

            var mainNode = builder.Locals[1];
            Assert.Equal("main", mainNode.Name);
            Assert.True(mainNode.IsBranch);
        }

        [Fact]
        public void Builder_DeeplyNestedBranch_CreatesMultipleFolderLevels()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("a/b/c"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.Single(builder.Locals);
            var folderA = builder.Locals[0];
            Assert.Equal("a", folderA.Name);
            Assert.Equal("refs/heads/a", folderA.Path);

            Assert.Single(folderA.Children);
            var folderB = folderA.Children[0];
            Assert.Equal("b", folderB.Name);
            Assert.Equal("refs/heads/a/b", folderB.Path);

            Assert.Single(folderB.Children);
            var leafC = folderB.Children[0];
            Assert.Equal("c", leafC.Name);
            Assert.Equal("refs/heads/a/b/c", leafC.Path);
            Assert.True(leafC.IsBranch);
        }

        // ------------------------------------------------------------------
        // Builder: Remote branches
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_RemoteBranches_GroupedUnderRemoteNode()
        {
            var remotes = new List<Komorebi.Models.Remote> { MakeRemote("origin") };
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeRemoteBranch("main", "origin"),
                MakeRemoteBranch("develop", "origin"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, remotes, false);

            Assert.Empty(builder.Locals);
            Assert.Single(builder.Remotes);

            var originNode = builder.Remotes[0];
            Assert.Equal("origin", originNode.Name);
            Assert.Equal(2, originNode.Counter);
            Assert.Equal(2, originNode.Children.Count);

            // Children sorted by name
            Assert.Equal("develop", originNode.Children[0].Name);
            Assert.Equal("main", originNode.Children[1].Name);
        }

        [Fact]
        public void Builder_MultipleRemotes_CreatesSeparateNodes()
        {
            var remotes = new List<Komorebi.Models.Remote>
            {
                MakeRemote("origin"),
                MakeRemote("upstream"),
            };
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeRemoteBranch("main", "origin"),
                MakeRemoteBranch("main", "upstream"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, remotes, false);

            Assert.Equal(2, builder.Remotes.Count);
            Assert.Equal("origin", builder.Remotes[0].Name);
            Assert.Equal("upstream", builder.Remotes[1].Name);

            Assert.Single(builder.Remotes[0].Children);
            Assert.Single(builder.Remotes[1].Children);
        }

        // ------------------------------------------------------------------
        // Builder: Expanded nodes
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_ExpandedNodes_AreExpanded()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("feature/login"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);
            builder.SetExpandedNodes(new List<string> { "refs/heads/feature" });

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            var folder = builder.Locals[0];
            Assert.Equal("feature", folder.Name);
            Assert.True(folder.IsExpanded);
        }

        [Fact]
        public void Builder_UnexpandedNodes_AreNotExpanded()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("feature/login"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            var folder = builder.Locals[0];
            Assert.False(folder.IsExpanded);
        }

        [Fact]
        public void Builder_ForceExpanded_ExpandsAllNodes()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("feature/login"),
                MakeLocalBranch("bugfix/crash"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), bForceExpanded: true);

            foreach (var node in builder.Locals)
            {
                Assert.True(node.IsExpanded);
            }
        }

        [Fact]
        public void Builder_NonexistentExpandedNodes_AreNotCollected()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("main"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);
            builder.SetExpandedNodes(new List<string> { "refs/heads/nonexistent" });

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            // 存在しないパスはツリーに含まれないため、CollectExpandedPathsで収集されない
            var collected = new List<string>();
            Komorebi.ViewModels.BranchTreeNode.Builder.CollectExpandedPaths(builder.Locals, collected);
            Assert.Empty(collected);
        }

        // ------------------------------------------------------------------
        // Builder: Current branch forces parent expansion
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_CurrentBranch_ExpandsParentFolder()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("feature/login", isCurrent: true),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            var folder = builder.Locals[0];
            Assert.Equal("feature", folder.Name);
            Assert.True(folder.IsExpanded, "Folder containing the current branch should be auto-expanded");
        }

        // ------------------------------------------------------------------
        // Builder: DetachedHead handling
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_DetachedHead_TreatedAsFlatEvenWithSlash()
        {
            // A detached HEAD with a slash in the name should NOT create a folder hierarchy.
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("HEAD/detached", isDetachedHead: true),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.Single(builder.Locals);
            // The node should be a leaf (branch), not a folder
            Assert.True(builder.Locals[0].IsBranch);
            Assert.Equal("HEAD/detached", builder.Locals[0].Name);
        }

        // ------------------------------------------------------------------
        // Builder: Sort by name
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_SortByName_FoldersBeforeBranches()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("main"),
                MakeLocalBranch("feature/login"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.Equal(2, builder.Locals.Count);
            // Folder ("feature") should come before branch ("main")
            Assert.False(builder.Locals[0].IsBranch);
            Assert.True(builder.Locals[1].IsBranch);
        }

        [Fact]
        public void Builder_SortByName_DetachedHeadComesFirst()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("main"),
                MakeLocalBranch("(HEAD detached)", isDetachedHead: true),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.Equal(2, builder.Locals.Count);
            Assert.Equal("(HEAD detached)", builder.Locals[0].Name);
            Assert.Equal("main", builder.Locals[1].Name);
        }

        [Fact]
        public void Builder_SortByName_NumericOrdering()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("branch-2"),
                MakeLocalBranch("branch-10"),
                MakeLocalBranch("branch-1"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.Equal("branch-1", builder.Locals[0].Name);
            Assert.Equal("branch-2", builder.Locals[1].Name);
            Assert.Equal("branch-10", builder.Locals[2].Name);
        }

        // ------------------------------------------------------------------
        // Builder: Sort by time (CommitterDate)
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_SortByTime_NewestFirst()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("old-branch", committerDate: 100),
                MakeLocalBranch("new-branch", committerDate: 300),
                MakeLocalBranch("mid-branch", committerDate: 200),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.CommitterDate,
                Komorebi.Models.BranchSortMode.CommitterDate);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.Equal("new-branch", builder.Locals[0].Name);
            Assert.Equal("mid-branch", builder.Locals[1].Name);
            Assert.Equal("old-branch", builder.Locals[2].Name);
        }

        [Fact]
        public void Builder_SortByTime_FoldersBeforeBranches()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("main", committerDate: 300),
                MakeLocalBranch("feature/login", committerDate: 100),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.CommitterDate,
                Komorebi.Models.BranchSortMode.CommitterDate);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            // Folder (feature) should come before branch (main) even when sorting by time
            Assert.False(builder.Locals[0].IsBranch);
            Assert.True(builder.Locals[1].IsBranch);
        }

        [Fact]
        public void Builder_SortByTime_EqualTimes_FallsBackToName()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("zeta", committerDate: 100),
                MakeLocalBranch("alpha", committerDate: 100),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.CommitterDate,
                Komorebi.Models.BranchSortMode.CommitterDate);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.Equal("alpha", builder.Locals[0].Name);
            Assert.Equal("zeta", builder.Locals[1].Name);
        }

        // ------------------------------------------------------------------
        // Builder: Mixed local and remote sort modes
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_DifferentSortModes_LocalByNameRemoteByTime()
        {
            var remotes = new List<Komorebi.Models.Remote> { MakeRemote("origin") };
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("beta"),
                MakeLocalBranch("alpha"),
                MakeRemoteBranch("old-branch", "origin", committerDate: 100),
                MakeRemoteBranch("new-branch", "origin", committerDate: 300),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.CommitterDate);

            builder.Run(branches, remotes, false);

            // Locals sorted by name
            Assert.Equal("alpha", builder.Locals[0].Name);
            Assert.Equal("beta", builder.Locals[1].Name);

            // Remote children sorted by time (newest first)
            var originChildren = builder.Remotes[0].Children;
            Assert.Equal("new-branch", originChildren[0].Name);
            Assert.Equal("old-branch", originChildren[1].Name);
        }

        // ------------------------------------------------------------------
        // Builder: Path generation
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_LocalBranch_PathIsCorrect()
        {
            var branches = new List<Komorebi.Models.Branch> { MakeLocalBranch("main") };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.Equal("refs/heads/main", builder.Locals[0].Path);
        }

        [Fact]
        public void Builder_RemoteBranch_PathIsCorrect()
        {
            var remotes = new List<Komorebi.Models.Remote> { MakeRemote("origin") };
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeRemoteBranch("main", "origin"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, remotes, false);

            Assert.Equal("refs/remotes/origin", builder.Remotes[0].Path);
            Assert.Equal("refs/remotes/origin/main", builder.Remotes[0].Children[0].Path);
        }

        [Fact]
        public void Builder_NestedRemoteBranch_PathIsCorrect()
        {
            var remotes = new List<Komorebi.Models.Remote> { MakeRemote("origin") };
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeRemoteBranch("feature/login", "origin"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, remotes, false);

            var originNode = builder.Remotes[0];
            // Under origin, there should be a "feature" folder with a "login" leaf
            Assert.Single(originNode.Children);
            var featureFolder = originNode.Children[0];
            Assert.Equal("feature", featureFolder.Name);
            Assert.Equal("refs/remotes/origin/feature", featureFolder.Path);

            Assert.Single(featureFolder.Children);
            Assert.Equal("login", featureFolder.Children[0].Name);
            Assert.Equal("refs/remotes/origin/feature/login", featureFolder.Children[0].Path);
        }

        // ------------------------------------------------------------------
        // Builder: Empty inputs
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_NoBranches_ProducesEmptyLists()
        {
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(new List<Komorebi.Models.Branch>(), new List<Komorebi.Models.Remote>(), false);

            Assert.Empty(builder.Locals);
            Assert.Empty(builder.Remotes);
        }

        [Fact]
        public void Builder_RemoteWithNoBranches_CreatesEmptyRemoteNode()
        {
            var remotes = new List<Komorebi.Models.Remote> { MakeRemote("origin") };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(new List<Komorebi.Models.Branch>(), remotes, false);

            Assert.Single(builder.Remotes);
            Assert.Equal("origin", builder.Remotes[0].Name);
            Assert.Empty(builder.Remotes[0].Children);
            Assert.Equal(0, builder.Remotes[0].Counter);
        }

        // ------------------------------------------------------------------
        // Builder: TimeToSort propagation for folders
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_FolderTimeToSort_IsMaxOfChildren()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("feature/old", committerDate: 100),
                MakeLocalBranch("feature/new", committerDate: 500),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.CommitterDate,
                Komorebi.Models.BranchSortMode.CommitterDate);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            var folder = builder.Locals[0];
            Assert.Equal("feature", folder.Name);
            Assert.Equal(500UL, folder.TimeToSort);
        }

        // ------------------------------------------------------------------
        // Builder: Shared folder across multiple branches
        // ------------------------------------------------------------------

        [Fact]
        public void Builder_SharedFolder_CountsAllBranches()
        {
            var branches = new List<Komorebi.Models.Branch>
            {
                MakeLocalBranch("feature/a"),
                MakeLocalBranch("feature/b"),
                MakeLocalBranch("feature/c"),
            };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.Single(builder.Locals);
            Assert.Equal(3, builder.Locals[0].Counter);
            Assert.Equal(3, builder.Locals[0].Children.Count);
        }

        // ------------------------------------------------------------------
        // Builder: ShowUpstreamGoneTip
        // ------------------------------------------------------------------

        [Fact]
        public void ShowUpstreamGoneTip_TrueForUpstreamGoneBranch()
        {
            var branch = MakeLocalBranch("stale");
            branch.IsUpstreamGone = true;

            var branches = new List<Komorebi.Models.Branch> { branch };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.True(builder.Locals[0].ShowUpstreamGoneTip);
        }

        [Fact]
        public void ShowUpstreamGoneTip_FalseForNormalBranch()
        {
            var branches = new List<Komorebi.Models.Branch> { MakeLocalBranch("main") };
            var builder = new Komorebi.ViewModels.BranchTreeNode.Builder(
                Komorebi.Models.BranchSortMode.Name,
                Komorebi.Models.BranchSortMode.Name);

            builder.Run(branches, new List<Komorebi.Models.Remote>(), false);

            Assert.False(builder.Locals[0].ShowUpstreamGoneTip);
        }
    }
}
