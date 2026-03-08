using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    public class InteractiveRebaseTests
    {
        #region InteractiveRebaseAction Enum

        [Fact]
        public void InteractiveRebaseAction_HasExpectedValues()
        {
            Assert.Equal(0, (int)InteractiveRebaseAction.Pick);
            Assert.Equal(1, (int)InteractiveRebaseAction.Edit);
            Assert.Equal(2, (int)InteractiveRebaseAction.Reword);
            Assert.Equal(3, (int)InteractiveRebaseAction.Squash);
            Assert.Equal(4, (int)InteractiveRebaseAction.Fixup);
            Assert.Equal(5, (int)InteractiveRebaseAction.Drop);
        }

        [Fact]
        public void InteractiveRebaseAction_HasSixValues()
        {
            var values = Enum.GetValues<InteractiveRebaseAction>();
            Assert.Equal(6, values.Length);
        }

        #endregion

        #region InteractiveRebasePendingType Enum

        [Fact]
        public void InteractiveRebasePendingType_HasExpectedValues()
        {
            Assert.Equal(0, (int)InteractiveRebasePendingType.None);
            Assert.Equal(1, (int)InteractiveRebasePendingType.Target);
            Assert.Equal(2, (int)InteractiveRebasePendingType.Pending);
            Assert.Equal(3, (int)InteractiveRebasePendingType.Ignore);
            Assert.Equal(4, (int)InteractiveRebasePendingType.Last);
        }

        #endregion

        #region InteractiveCommit

        [Fact]
        public void InteractiveCommit_DefaultValues()
        {
            var ic = new InteractiveCommit();
            Assert.NotNull(ic.Commit);
            Assert.Equal(string.Empty, ic.Message);
        }

        [Fact]
        public void InteractiveCommit_CanSetProperties()
        {
            var commit = new Commit { SHA = "abc123" };
            var ic = new InteractiveCommit
            {
                Commit = commit,
                Message = "Test message"
            };
            Assert.Same(commit, ic.Commit);
            Assert.Equal("Test message", ic.Message);
        }

        #endregion

        #region InteractiveRebaseJob

        [Fact]
        public void InteractiveRebaseJob_DefaultValues()
        {
            var job = new InteractiveRebaseJob();
            Assert.Equal(string.Empty, job.SHA);
            Assert.Equal(InteractiveRebaseAction.Pick, job.Action);
            Assert.Equal(string.Empty, job.Message);
        }

        [Fact]
        public void InteractiveRebaseJob_CanSetProperties()
        {
            var job = new InteractiveRebaseJob
            {
                SHA = "abc123def456",
                Action = InteractiveRebaseAction.Squash,
                Message = "squash this"
            };
            Assert.Equal("abc123def456", job.SHA);
            Assert.Equal(InteractiveRebaseAction.Squash, job.Action);
            Assert.Equal("squash this", job.Message);
        }

        [Theory]
        [InlineData(InteractiveRebaseAction.Pick)]
        [InlineData(InteractiveRebaseAction.Edit)]
        [InlineData(InteractiveRebaseAction.Reword)]
        [InlineData(InteractiveRebaseAction.Squash)]
        [InlineData(InteractiveRebaseAction.Fixup)]
        [InlineData(InteractiveRebaseAction.Drop)]
        public void InteractiveRebaseJob_AllActionsCanBeSet(InteractiveRebaseAction action)
        {
            var job = new InteractiveRebaseJob { Action = action };
            Assert.Equal(action, job.Action);
        }

        #endregion

        #region InteractiveRebaseJobCollection

        [Fact]
        public void InteractiveRebaseJobCollection_DefaultValues()
        {
            var collection = new InteractiveRebaseJobCollection();
            Assert.Equal(string.Empty, collection.OrigHead);
            Assert.Equal(string.Empty, collection.Onto);
            Assert.NotNull(collection.Jobs);
            Assert.Empty(collection.Jobs);
        }

        [Fact]
        public void InteractiveRebaseJobCollection_CanAddJobs()
        {
            var collection = new InteractiveRebaseJobCollection
            {
                OrigHead = "abc123",
                Onto = "def456"
            };

            collection.Jobs.Add(new InteractiveRebaseJob
            {
                SHA = "111111",
                Action = InteractiveRebaseAction.Pick,
                Message = "first commit"
            });

            collection.Jobs.Add(new InteractiveRebaseJob
            {
                SHA = "222222",
                Action = InteractiveRebaseAction.Squash,
                Message = "second commit"
            });

            Assert.Equal("abc123", collection.OrigHead);
            Assert.Equal("def456", collection.Onto);
            Assert.Equal(2, collection.Jobs.Count);
            Assert.Equal(InteractiveRebaseAction.Pick, collection.Jobs[0].Action);
            Assert.Equal(InteractiveRebaseAction.Squash, collection.Jobs[1].Action);
        }

        #endregion
    }
}
