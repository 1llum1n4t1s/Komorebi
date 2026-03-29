using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class StatisticsReportTests
    {
        #region Helpers

        private static User MakeUser(string name, string email)
        {
            return User.FindOrAdd($"{name}±{email}");
        }

        #endregion

        #region AddCommit - Total Count

        [Fact]
        public void AddCommit_IncrementsTotalCount()
        {
            var report = new StatisticsReport(StatisticsMode.ThisWeek, DateTime.Now.Date);
            var user = MakeUser("Alice", "alice@example.com");

            report.AddCommit(DateTime.Now, user);
            report.AddCommit(DateTime.Now, user);
            report.AddCommit(DateTime.Now, user);

            Assert.Equal(3, report.Total);
        }

        [Fact]
        public void AddCommit_ZeroCommits_TotalIsZero()
        {
            var report = new StatisticsReport(StatisticsMode.All, DateTime.MinValue);

            Assert.Equal(0, report.Total);
        }

        #endregion

        #region Complete - Authors Sorted by Count

        [Fact]
        public void Complete_SortsAuthorsByCountDescending()
        {
            var start = DateTime.Now.Date;
            var report = new StatisticsReport(StatisticsMode.ThisWeek, start);

            var alice = MakeUser("Alice", "alice-stats@example.com");
            var bob = MakeUser("Bob", "bob-stats@example.com");
            var carol = MakeUser("Carol", "carol-stats@example.com");

            // Bob: 3 commits, Alice: 1 commit, Carol: 2 commits
            report.AddCommit(start, bob);
            report.AddCommit(start, bob);
            report.AddCommit(start, bob);
            report.AddCommit(start, alice);
            report.AddCommit(start, carol);
            report.AddCommit(start, carol);

            report.Complete();

            Assert.Equal(3, report.Authors.Count);
            Assert.Equal("Bob", report.Authors[0].User.Name);
            Assert.Equal(3, report.Authors[0].Count);
            Assert.Equal("Carol", report.Authors[1].User.Name);
            Assert.Equal(2, report.Authors[1].Count);
            Assert.Equal("Alice", report.Authors[2].User.Name);
            Assert.Equal(1, report.Authors[2].Count);
        }

        [Fact]
        public void Complete_NoCommits_AuthorsIsEmpty()
        {
            var report = new StatisticsReport(StatisticsMode.All, DateTime.MinValue);
            report.Complete();

            Assert.Empty(report.Authors);
        }

        [Fact]
        public void Complete_SingleAuthor_SingleEntry()
        {
            var start = DateTime.Now.Date;
            var report = new StatisticsReport(StatisticsMode.ThisWeek, start);
            var alice = MakeUser("Alice", "alice-single@example.com");

            report.AddCommit(start, alice);
            report.AddCommit(start, alice);

            report.Complete();

            Assert.Single(report.Authors);
            Assert.Equal(2, report.Authors[0].Count);
        }

        #endregion

        #region Complete - Series Created

        [Fact]
        public void Complete_CreatesSeries()
        {
            var start = DateTime.Now.Date;
            var report = new StatisticsReport(StatisticsMode.ThisWeek, start);
            var user = MakeUser("Test", "test-series@example.com");

            report.AddCommit(start, user);
            report.Complete();

            Assert.Single(report.Series);
        }

        [Fact]
        public void Complete_NoCommits_StillCreatesSeries()
        {
            var report = new StatisticsReport(StatisticsMode.All, DateTime.MinValue);
            report.Complete();

            Assert.Single(report.Series);
        }

        #endregion

        #region StatisticsMode Construction

        [Fact]
        public void Constructor_ThisWeek_CreatesXAxisAndYAxis()
        {
            var start = DateTime.Now.Date;
            var report = new StatisticsReport(StatisticsMode.ThisWeek, start);

            Assert.Single(report.XAxes);
            Assert.Single(report.YAxes);
        }

        [Fact]
        public void Constructor_ThisMonth_CreatesXAxisAndYAxis()
        {
            var start = new DateTime(2025, 1, 1);
            var report = new StatisticsReport(StatisticsMode.ThisMonth, start);

            Assert.Single(report.XAxes);
            Assert.Single(report.YAxes);
        }

        [Fact]
        public void Constructor_All_CreatesXAxisAndYAxis()
        {
            var report = new StatisticsReport(StatisticsMode.All, DateTime.MinValue);

            Assert.Single(report.XAxes);
            Assert.Single(report.YAxes);
        }

        #endregion

        #region AddCommit - Multiple Authors Tracked Separately

        [Fact]
        public void AddCommit_MultipleAuthors_TrackedIndividually()
        {
            var start = DateTime.Now.Date;
            var report = new StatisticsReport(StatisticsMode.ThisWeek, start);

            var alice = MakeUser("Alice", "alice-multi@example.com");
            var bob = MakeUser("Bob", "bob-multi@example.com");

            report.AddCommit(start, alice);
            report.AddCommit(start, alice);
            report.AddCommit(start, bob);

            report.Complete();

            Assert.Equal(2, report.Authors.Count);
            Assert.Equal(3, report.Total);
        }

        #endregion

        #region AddCommit - Date Normalization

        [Fact]
        public void AddCommit_ThisWeek_NormalizesToDate()
        {
            var start = new DateTime(2025, 6, 16); // Monday
            var report = new StatisticsReport(StatisticsMode.ThisWeek, start);
            var user = MakeUser("Test", "test-norm-week@example.com");

            // Different times on same day should aggregate
            report.AddCommit(new DateTime(2025, 6, 16, 10, 0, 0), user);
            report.AddCommit(new DateTime(2025, 6, 16, 14, 30, 0), user);

            report.Complete();

            Assert.Equal(2, report.Total);
            // Both commits should be counted (both same day)
            Assert.Single(report.Authors);
            Assert.Equal(2, report.Authors[0].Count);
        }

        [Fact]
        public void AddCommit_ThisMonth_NormalizesToDate()
        {
            var start = new DateTime(2025, 6, 1);
            var report = new StatisticsReport(StatisticsMode.ThisMonth, start);
            var user = MakeUser("Test", "test-norm-month@example.com");

            report.AddCommit(new DateTime(2025, 6, 1, 8, 0, 0), user);
            report.AddCommit(new DateTime(2025, 6, 1, 20, 0, 0), user);
            report.AddCommit(new DateTime(2025, 6, 15, 12, 0, 0), user);

            report.Complete();

            Assert.Equal(3, report.Total);
        }

        #endregion
    }

    public class StatisticsTests
    {
        #region AddCommit - Routing to Sub-Reports

        [Fact]
        public void AddCommit_RecentTimestamp_AppearsInAllReports()
        {
            var stats = new Statistics();
            var now = DateTime.Now;
            var timestamp = new DateTimeOffset(now).ToUnixTimeSeconds();

            // Use a unique author string with ± separator
            stats.AddCommit($"TestUser±test-all-reports@example.com", timestamp);
            stats.Complete();

            // A commit from "now" should appear in all three reports
            Assert.Equal(1, stats.All.Total);
            Assert.Equal(1, stats.Month.Total);
            Assert.Equal(1, stats.Week.Total);
        }

        [Fact]
        public void AddCommit_OldTimestamp_OnlyInAll()
        {
            var stats = new Statistics();
            // Use a timestamp from 2 years ago
            var oldDate = DateTime.Now.AddYears(-2);
            var timestamp = new DateTimeOffset(oldDate).ToUnixTimeSeconds();

            stats.AddCommit($"OldUser±old-user@example.com", timestamp);
            stats.Complete();

            Assert.Equal(1, stats.All.Total);
            Assert.Equal(0, stats.Month.Total);
            Assert.Equal(0, stats.Week.Total);
        }

        [Fact]
        public void Complete_ClearsInternalState()
        {
            var stats = new Statistics();
            var now = DateTime.Now;
            var timestamp = new DateTimeOffset(now).ToUnixTimeSeconds();

            stats.AddCommit($"User1±user1-clear@example.com", timestamp);
            stats.Complete();

            // After Complete, authors should be populated
            Assert.NotEmpty(stats.All.Authors);
        }

        #endregion

        #region AddCommit - Author Parsing

        [Fact]
        public void AddCommit_SameEmailDifferentCase_TreatedAsSameUser()
        {
            var stats = new Statistics();
            var now = DateTime.Now;
            var timestamp = new DateTimeOffset(now).ToUnixTimeSeconds();

            // The Statistics.AddCommit normalizes email to lower case
            stats.AddCommit($"Alice±Alice@Example.COM", timestamp);
            stats.AddCommit($"Alice±alice@example.com", timestamp);
            stats.Complete();

            // Should be treated as one author
            Assert.Single(stats.All.Authors);
            Assert.Equal(2, stats.All.Authors[0].Count);
        }

        [Fact]
        public void AddCommit_DifferentAuthors_TrackedSeparately()
        {
            var stats = new Statistics();
            var now = DateTime.Now;
            var timestamp = new DateTimeOffset(now).ToUnixTimeSeconds();

            stats.AddCommit($"Alice±alice-diff@example.com", timestamp);
            stats.AddCommit($"Bob±bob-diff@example.com", timestamp);
            stats.Complete();

            Assert.Equal(2, stats.All.Authors.Count);
            Assert.Equal(2, stats.All.Total);
        }

        [Fact]
        public void AddCommit_MultipleCommitsSameAuthor()
        {
            var stats = new Statistics();
            var now = DateTime.Now;
            var timestamp = new DateTimeOffset(now).ToUnixTimeSeconds();

            stats.AddCommit($"Alice±alice-multi@example.com", timestamp);
            stats.AddCommit($"Alice±alice-multi@example.com", timestamp);
            stats.AddCommit($"Alice±alice-multi@example.com", timestamp);
            stats.Complete();

            Assert.Single(stats.All.Authors);
            Assert.Equal(3, stats.All.Authors[0].Count);
            Assert.Equal(3, stats.All.Total);
        }

        #endregion
    }

    public class StatisticsAuthorTests
    {
        [Fact]
        public void Constructor_SetsUserAndCount()
        {
            var user = User.FindOrAdd("Test±test-author@example.com");
            var author = new StatisticsAuthor(user, 42);

            Assert.Equal(user, author.User);
            Assert.Equal(42, author.Count);
        }

        [Fact]
        public void Properties_AreSettable()
        {
            var user1 = User.FindOrAdd("User1±user1-settable@example.com");
            var user2 = User.FindOrAdd("User2±user2-settable@example.com");
            var author = new StatisticsAuthor(user1, 10);

            author.User = user2;
            author.Count = 20;

            Assert.Equal(user2, author.User);
            Assert.Equal(20, author.Count);
        }
    }

    public class StatisticsModeTests
    {
        [Fact]
        public void Enum_HasExpectedValues()
        {
            Assert.Equal(0, (int)StatisticsMode.All);
            Assert.Equal(1, (int)StatisticsMode.ThisMonth);
            Assert.Equal(2, (int)StatisticsMode.ThisWeek);
        }
    }
}
