using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// Userクラスに対するadversarialテスト。
    /// コンストラクタのnull/極端入力、FindOrAddの並行性、Equals/HashCodeの整合性を攻撃する。
    /// </summary>
    public class UserAdversarialTests
    {
        // ================================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ================================================================

        /// <summary>
        /// @adversarial @category boundary @severity high
        /// data=null でコンストラクタがクラッシュしないこと
        /// </summary>
        [Fact]
        public void Constructor_NullData_DoesNotCrash()
        {
            var user = new User(null);
            Assert.Equal(string.Empty, user.Name);
            Assert.Equal(string.Empty, user.Email);
        }

        /// <summary>
        /// @adversarial @category boundary @severity high
        /// data="" でコンストラクタがクラッシュしないこと
        /// </summary>
        [Fact]
        public void Constructor_EmptyString_DoesNotCrash()
        {
            var user = new User("");
            Assert.Equal(string.Empty, user.Name);
            Assert.Equal(string.Empty, user.Email);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// 区切り文字「±」のみのdata でクラッシュしないこと
        /// </summary>
        [Fact]
        public void Constructor_OnlySeparator_SetsEmptyNameAndEmail()
        {
            var user = new User("±");
            Assert.Equal("", user.Name);
            Assert.Equal("", user.Email);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// 複数の「±」を含むdata で2番目以降がEmailに含まれること
        /// </summary>
        [Fact]
        public void Constructor_MultipleSeparators_SecondPartIncludesExtraSeparators()
        {
            var user = new User("Alice±alice±extra@example.com");
            Assert.Equal("Alice", user.Name);
            Assert.Equal("alice±extra@example.com", user.Email);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// 100KBのdata文字列でクラッシュしないこと
        /// </summary>
        [Fact]
        public void Constructor_HugeData_DoesNotCrash()
        {
            var huge = new string('A', 50_000) + "±" + new string('B', 50_000);
            var user = new User(huge);
            Assert.Equal(50_000, user.Name.Length);
            Assert.Equal(50_000, user.Email.Length);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// ヌルバイトを含むdata でクラッシュしないこと
        /// </summary>
        [Fact]
        public void Constructor_NullByteInData_DoesNotCrash()
        {
            var user = new User("Ali\0ce±ali\0ce@example.com");
            Assert.Contains("\0", user.Name);
            Assert.Contains("\0", user.Email);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// ゼロ幅文字・RTL制御文字を含むdata でクラッシュしないこと
        /// </summary>
        [Fact]
        public void Constructor_UnicodeControlChars_DoesNotCrash()
        {
            var user = new User("\u200BAlice\u202E±\u200Balice@example.com");
            Assert.Contains("\u200B", user.Name);
        }

        /// <summary>
        /// @adversarial @category boundary @severity medium
        /// Emailに山括弧が多重にネストされている場合のトリム動作
        /// </summary>
        [Fact]
        public void Constructor_DoubleAngleBrackets_TrimsOnlyOutermost()
        {
            var user = new User("Alice±<<alice@example.com>>");
            // TrimStart/TrimEnd は複数文字を削除する
            Assert.Equal("alice@example.com", user.Email);
        }

        // ================================================================
        // ⚡ 並行性・レースコンディション（Concurrency Chaos）
        // ================================================================

        /// <summary>
        /// @adversarial @category concurrency @severity high
        /// 1000スレッドから同じキーでFindOrAddを呼んでも全て同一インスタンスを返すこと
        /// </summary>
        [Fact]
        public void FindOrAdd_ConcurrentSameKey_ReturnsSameInstance()
        {
            var key = $"ConcTest±concurrent_{System.Guid.NewGuid()}@test.com";
            var results = new User[1000];

            Parallel.For(0, 1000, i =>
            {
                results[i] = User.FindOrAdd(key);
            });

            var first = results[0];
            for (var i = 1; i < 1000; i++)
                Assert.Same(first, results[i]);
        }

        /// <summary>
        /// @adversarial @category concurrency @severity medium
        /// 異なるキーで同時アクセスしても例外が発生しないこと
        /// </summary>
        [Fact]
        public void FindOrAdd_ConcurrentDifferentKeys_NoExceptions()
        {
            var guid = System.Guid.NewGuid();
            Parallel.For(0, 500, i =>
            {
                var user = User.FindOrAdd($"User{i}±user{i}_{guid}@test.com");
                Assert.NotNull(user);
            });
        }

        // ================================================================
        // 🎭 型パンチ・プロトコル違反（Type Punching）
        // ================================================================

        /// <summary>
        /// @adversarial @category type @severity medium
        /// Equals で User 以外のオブジェクトと比較して false を返すこと
        /// </summary>
        [Fact]
        public void Equals_NonUserObject_ReturnsFalse()
        {
            var user = new User("Test±test@test.com");
            Assert.False(user.Equals("not a user"));
            Assert.False(user.Equals(42));
            Assert.False(user.Equals(null));
        }

        /// <summary>
        /// @adversarial @category type @severity medium
        /// GetHashCode がデフォルトコンストラクタ（User.Invalid）で 0 を返すこと
        /// </summary>
        [Fact]
        public void GetHashCode_DefaultConstructor_ReturnsZero()
        {
            var user = new User();
            Assert.Equal(0, user.GetHashCode());
        }

        // ================================================================
        // 🔀 状態遷移の矛盾（State Machine Abuse）
        // ================================================================

        /// <summary>
        /// @adversarial @category state @severity medium
        /// ToString が Name/Email 変更後も最新値を反映すること
        /// </summary>
        [Fact]
        public void ToString_AfterPropertyChange_ReflectsNewValues()
        {
            var user = new User("Alice±alice@test.com");
            Assert.Equal("Alice <alice@test.com>", user.ToString());

            user.Name = "Bob";
            user.Email = "bob@test.com";
            Assert.Equal("Bob <bob@test.com>", user.ToString());
        }

        /// <summary>
        /// @adversarial @category state @severity high
        /// Equals は Name/Email で判定するが、HashCode は元のdata文字列で計算される。
        /// プロパティ変更後にEquals=trueだがHashCode不一致が起こりうることを確認。
        /// </summary>
        [Fact]
        public void EqualsHashCode_PropertyMutation_InconsistencyDetected()
        {
            var user1 = new User("Alice±alice@test.com");
            var user2 = new User("Bob±bob@test.com");

            // プロパティを変更してEquals=trueにする
            user2.Name = "Alice";
            user2.Email = "alice@test.com";

            // Equals は true だが HashCode は異なる（不整合！）
            Assert.True(user1.Equals(user2));
            // この不整合はDictionaryのキーとして使うとバグになる
            Assert.NotEqual(user1.GetHashCode(), user2.GetHashCode());
        }
    }
}
