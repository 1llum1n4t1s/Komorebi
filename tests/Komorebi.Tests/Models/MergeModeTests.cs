using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class MergeModeTests
    {
        #region Supported Array

        [Fact]
        public void Supported_IsNotNull()
        {
            Assert.NotNull(MergeMode.Supported);
        }

        [Fact]
        public void Supported_HasFiveEntries()
        {
            Assert.Equal(5, MergeMode.Supported.Length);
        }

        [Fact]
        public void Supported_AllHaveNonEmptyName()
        {
            foreach (var mode in MergeMode.Supported)
            {
                Assert.False(string.IsNullOrWhiteSpace(mode.Name),
                    $"MergeMode with Arg='{mode.Arg}' has empty Name");
            }
        }

        [Fact]
        public void Supported_AllHaveNonEmptyDesc()
        {
            foreach (var mode in MergeMode.Supported)
            {
                Assert.False(string.IsNullOrWhiteSpace(mode.Desc),
                    $"MergeMode '{mode.Name}' has empty Desc");
            }
        }

        [Fact]
        public void Supported_AllNamesAreUnique()
        {
            var names = new HashSet<string>();
            foreach (var mode in MergeMode.Supported)
            {
                Assert.True(names.Add(mode.Name),
                    $"Duplicate MergeMode name: '{mode.Name}'");
            }
        }

        #endregion

        #region Static Instances

        [Fact]
        public void Default_HasEmptyArg()
        {
            Assert.Equal("", MergeMode.Default.Arg);
        }

        [Fact]
        public void FastForward_HasCorrectArg()
        {
            Assert.Equal("--ff-only", MergeMode.FastForward.Arg);
        }

        [Fact]
        public void NoFastForward_HasCorrectArg()
        {
            Assert.Equal("--no-ff", MergeMode.NoFastForward.Arg);
        }

        [Fact]
        public void Squash_HasCorrectArg()
        {
            Assert.Equal("--squash", MergeMode.Squash.Arg);
        }

        [Fact]
        public void DontCommit_HasCorrectArg()
        {
            Assert.Equal("--no-ff --no-commit", MergeMode.DontCommit.Arg);
        }

        #endregion

        #region Supported Contains Static Instances

        [Fact]
        public void Supported_ContainsDefault()
        {
            Assert.Contains(MergeMode.Default, MergeMode.Supported);
        }

        [Fact]
        public void Supported_ContainsFastForward()
        {
            Assert.Contains(MergeMode.FastForward, MergeMode.Supported);
        }

        [Fact]
        public void Supported_ContainsNoFastForward()
        {
            Assert.Contains(MergeMode.NoFastForward, MergeMode.Supported);
        }

        [Fact]
        public void Supported_ContainsSquash()
        {
            Assert.Contains(MergeMode.Squash, MergeMode.Supported);
        }

        [Fact]
        public void Supported_ContainsDontCommit()
        {
            Assert.Contains(MergeMode.DontCommit, MergeMode.Supported);
        }

        [Fact]
        public void Supported_DefaultIsFirst()
        {
            Assert.Same(MergeMode.Default, MergeMode.Supported[0]);
        }

        #endregion
    }
}
