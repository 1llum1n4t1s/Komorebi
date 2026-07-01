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
        /// Emailに山括弧が含まれていてもそのまま保持されること
        /// （全gitコマンドの出力が山括弧なしの $NAME±$EMAIL 形式に統一されたため、
        /// upstream ff3f81b2 以降 User 側での山括弧除去は行わない）
        /// </summary>
        [Fact]
        public void Constructor_AngleBrackets_KeptVerbatim()
        {
            var user = new User("Alice±<<alice@example.com>>");
            Assert.Equal("<<alice@example.com>>", user.Email);
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
        /// Equals（参照等価）で User 以外のオブジェクトと比較して false を返すこと
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
        /// User.Invalid シングルトンが空の Name/Email を持つこと
        /// </summary>
        [Fact]
        public void Invalid_Singleton_HasEmptyNameAndEmail()
        {
            Assert.Equal(string.Empty, User.Invalid.Name);
            Assert.Equal(string.Empty, User.Invalid.Email);
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
        /// upstream 39668075 以降は参照等価: 同一内容でも別インスタンスは等しくない。
        /// 同一インスタンス保証は FindOrAdd キャッシュが担う。
        /// </summary>
        [Fact]
        public void Equals_ReferenceSemantics_DifferentInstancesNotEqual()
        {
            var user1 = new User("Alice±alice@test.com");
            var user2 = new User("Alice±alice@test.com");

            // 内容が同じでも別インスタンスは等しくない（参照等価）
            Assert.False(user1.Equals(user2));

            // 同じキーの FindOrAdd は同一インスタンスを返すため等価
            var key = $"RefTest±ref_{System.Guid.NewGuid()}@test.com";
            var cached1 = User.FindOrAdd(key);
            var cached2 = User.FindOrAdd(key);
            Assert.Same(cached1, cached2);
        }
    }
}
