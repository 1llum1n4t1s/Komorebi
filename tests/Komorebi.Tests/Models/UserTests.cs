using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class UserTests
    {
        // -----------------------------------------------------------
        // Constructor — parsing "Name±Email" format
        // -----------------------------------------------------------

        [Fact]
        public void Constructor_ParsesNameAndEmail()
        {
            var user = new User("Alice±alice@example.com");

            Assert.Equal("Alice", user.Name);
            Assert.Equal("alice@example.com", user.Email);
        }

        [Fact]
        public void Constructor_KeepsAngleBracketsInEmail()
        {
            // 全gitコマンドの出力が山括弧なしの $NAME±$EMAIL 形式に統一されたため
            // (upstream ff3f81b2)、User 側での山括弧除去は行わない
            var user = new User("Bob±<bob@example.com>");

            Assert.Equal("Bob", user.Name);
            Assert.Equal("<bob@example.com>", user.Email);
        }

        [Fact]
        public void Constructor_NoSeparator_SetsNameEmptyAndEmailToData()
        {
            var user = new User("noreply@example.com");

            Assert.Equal(string.Empty, user.Name);
            Assert.Equal("noreply@example.com", user.Email);
        }

        [Fact]
        public void Constructor_EmptyName_SetsEmptyName()
        {
            var user = new User("±email@test.com");

            Assert.Equal(string.Empty, user.Name);
            Assert.Equal("email@test.com", user.Email);
        }

        [Fact]
        public void Constructor_EmptyEmail_SetsEmptyEmail()
        {
            var user = new User("SomeName±");

            Assert.Equal("SomeName", user.Name);
            Assert.Equal(string.Empty, user.Email);
        }

        [Fact]
        public void Constructor_MultipleSeparators_SplitsOnFirstOnly()
        {
            var user = new User("Name±part1±part2");

            Assert.Equal("Name", user.Name);
            Assert.Equal("part1±part2", user.Email);
        }

        // -----------------------------------------------------------
        // User.Invalid — default state
        // -----------------------------------------------------------

        [Fact]
        public void Invalid_HasEmptyNameAndEmail()
        {
            Assert.Equal(string.Empty, User.Invalid.Name);
            Assert.Equal(string.Empty, User.Invalid.Email);
        }

        // -----------------------------------------------------------
        // Equals / GetHashCode（参照等価。upstream 39668075 以降、
        // 同一インスタンス保証は FindOrAdd キャッシュが担う）
        // -----------------------------------------------------------

        [Fact]
        public void Equals_SameContentDifferentInstances_ReturnsFalse()
        {
            var user1 = new User("Alice±alice@example.com");
            var user2 = new User("Alice±alice@example.com");

            // 参照等価のため、内容が同じでも別インスタンスは等しくない
            Assert.False(user1.Equals(user2));
        }

        [Fact]
        public void Equals_DifferentEmail_ReturnsFalse()
        {
            var user1 = new User("Alice±alice@one.com");
            var user2 = new User("Alice±alice@two.com");

            Assert.False(user1.Equals(user2));
        }

        [Fact]
        public void Equals_DifferentName_ReturnsFalse()
        {
            var user1 = new User("Alice±same@test.com");
            var user2 = new User("Bob±same@test.com");

            Assert.False(user1.Equals(user2));
        }

        [Fact]
        public void Equals_Null_ReturnsFalse()
        {
            var user = new User("Alice±alice@example.com");
            Assert.False(user.Equals(null));
        }

        [Fact]
        public void Equals_NonUserObject_ReturnsFalse()
        {
            var user = new User("Alice±alice@example.com");
            Assert.False(user.Equals("not a user"));
        }

        [Fact]
        public void GetHashCode_SameInstance_IsStable()
        {
            var user = new User("Alice±alice@example.com");

            // 同一インスタンスのハッシュコードは安定している
            Assert.Equal(user.GetHashCode(), user.GetHashCode());
        }

        // -----------------------------------------------------------
        // FindOrAdd — caching behavior
        // -----------------------------------------------------------

        [Fact]
        public void FindOrAdd_ReturnsSameInstanceForSameData()
        {
            var data = "CacheTest±cache@test.com";
            var user1 = User.FindOrAdd(data);
            var user2 = User.FindOrAdd(data);

            Assert.Same(user1, user2);
        }

        [Fact]
        public void FindOrAdd_ReturnsDifferentInstancesForDifferentData()
        {
            var user1 = User.FindOrAdd("UserA±a@test.com");
            var user2 = User.FindOrAdd("UserB±b@test.com");

            Assert.NotSame(user1, user2);
        }

        [Fact]
        public void FindOrAdd_ParsesDataCorrectly()
        {
            var user = User.FindOrAdd("FindOrAddName±findoradd@test.com");

            Assert.Equal("FindOrAddName", user.Name);
            Assert.Equal("findoradd@test.com", user.Email);
        }

        // -----------------------------------------------------------
        // ToString
        // -----------------------------------------------------------

        [Fact]
        public void ToString_ReturnsNameAndEmailFormat()
        {
            var user = new User("Alice±alice@example.com");

            Assert.Equal("Alice <alice@example.com>", user.ToString());
        }

        [Fact]
        public void ToString_InvalidUser_ReturnsEmptyFormat()
        {
            Assert.Equal(" <>", User.Invalid.ToString());
        }
    }
}
