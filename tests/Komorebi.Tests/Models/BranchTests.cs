// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class BranchTests
    {
        #region FriendlyName

        #endregion

        #region HasWorktree

        #endregion

        #region TrackStatusDescription

        [Fact]
        public void TrackStatusDescription_BothAheadAndBehind()
        {
            var branch = new Branch
            {
                Ahead = new List<string> { "a", "b" },
                Behind = new List<string> { "c" }
            };
            Assert.Equal("2\u2191 1\u2193", branch.TrackStatusDescription);
        }

        [Fact]
        public void TrackStatusDescription_OnlyAhead()
        {
            var branch = new Branch
            {
                Ahead = new List<string> { "a", "b", "c" },
                Behind = new List<string>()
            };
            Assert.Equal("3\u2191", branch.TrackStatusDescription);
        }

        [Fact]
        public void TrackStatusDescription_OnlyBehind()
        {
            var branch = new Branch
            {
                Ahead = new List<string>(),
                Behind = new List<string> { "a", "b" }
            };
            Assert.Equal("2\u2193", branch.TrackStatusDescription);
        }

        [Fact]
        public void TrackStatusDescription_NeitherAheadNorBehind()
        {
            var branch = new Branch
            {
                Ahead = new List<string>(),
                Behind = new List<string>()
            };
            Assert.Equal(string.Empty, branch.TrackStatusDescription);
        }

        #endregion

        #region IsTrackStatusVisible

        #endregion

        #region IsUnpublished

        #endregion
    }
}
