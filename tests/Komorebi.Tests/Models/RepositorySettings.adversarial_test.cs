// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System.Text.Json;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// RepositorySettings に対する嫌がらせテスト。
    /// キャッシュ無効化欠如と並行アクセスを攻撃する。
    /// </summary>
    public class RepositorySettingsAdversarialTests : IDisposable
    {
        private readonly string _tempDir;

        public RepositorySettingsAdversarialTests()
        {
            // GUID 付きの専用ディレクトリなので、他テストのキャッシュエントリとキーが衝突しない
            _tempDir = Path.Combine(Path.GetTempPath(), $"komorebi_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            EvictCreatedEntries();
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // テストクリーンアップは best-effort
            }
        }

        /// <summary>
        /// このテストが Get() したディレクトリ（_tempDir とその配下）のキャッシュだけを公開 API の Evict で除去する。
        /// 以前はリフレクションで static キャッシュ全体を Clear していたが、並行実行中の他テストの
        /// エントリまで消してしまい、private フィールド名にも依存していたため置き換えた。
        /// </summary>
        private void EvictCreatedEntries()
        {
            Komorebi.Models.RepositorySettings.Evict(_tempDir);
            if (!Directory.Exists(_tempDir))
                return;
            foreach (var dir in Directory.EnumerateDirectories(_tempDir, "*", SearchOption.AllDirectories))
                Komorebi.Models.RepositorySettings.Evict(dir);
        }

        // ===============================================================
        // 💀 キャッシュ無効化欠如テスト（Resource Exhaustion）
        // ===============================================================

        /// <adversarial category="resource" severity="critical"
        ///   description="静的キャッシュが無効化されないため、ファイル変更後もGet()が古い値を返すこと"
        ///   expected="キャッシュされた古いインスタンスが返る（設計上の制限）" />
        [Fact]
        public void Get_CacheNeverInvalidates_ReturnsStaleSetting()
        {
            // 初回: 設定ファイルを作成して読み込み
            var settingsPath = Path.Combine(_tempDir, "komorebi.settings");
            var initial = new Komorebi.Models.RepositorySettings
            {
                DefaultRemote = "origin"
            };
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(initial, JsonCodeGen.Default.RepositorySettings));

            var first = Komorebi.Models.RepositorySettings.Get(_tempDir);
            Assert.Equal("origin", first.DefaultRemote);

            // ファイルを外部で変更（別のプロセスやテキストエディタで編集した想定）
            var modified = new Komorebi.Models.RepositorySettings
            {
                DefaultRemote = "upstream"
            };
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(modified, JsonCodeGen.Default.RepositorySettings));

            // 2回目: キャッシュが効くので古い値が返る
            var second = Komorebi.Models.RepositorySettings.Get(_tempDir);
            Assert.Equal("origin", second.DefaultRemote); // ← 古い値！
            Assert.Same(first, second); // 同一インスタンス
        }

        // ===============================================================
        // 🗡️ PushCommitMessage 境界値テスト
        // ===============================================================

        // ===============================================================
        // 🗡️ Get / SaveAsync 境界値テスト
        // ===============================================================

        /// <adversarial category="boundary" severity="high"
        ///   description="設定ファイルが存在しない場合、デフォルトインスタンスが返ること"
        ///   expected="新規RepositorySettingsインスタンス" />
        [Fact]
        public void Get_FileDoesNotExist_ReturnsDefault()
        {
            var dir = Path.Combine(_tempDir, "nonexistent_repo");
            Directory.CreateDirectory(dir);

            var settings = Komorebi.Models.RepositorySettings.Get(dir);

            Assert.NotNull(settings);
            Assert.Equal(string.Empty, settings.DefaultRemote);
            Assert.Equal(0, settings.PreferredMergeMode);
        }

        /// <adversarial category="boundary" severity="high"
        ///   description="空のJSON（{}）で読み込んだ場合、デフォルト値が使われること"
        ///   expected="各プロパティにデフォルト値" />
        [Fact]
        public void Get_EmptyJsonObject_ReturnsDefaults()
        {
            var dir = Path.Combine(_tempDir, "empty_json_repo");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "komorebi.settings"), "{}");

            var settings = Komorebi.Models.RepositorySettings.Get(dir);

            Assert.Equal(string.Empty, settings.DefaultRemote);
            Assert.Equal(0, settings.PreferredMergeMode);
            // EnableAutoFetch / AutoFetchInterval は v2026.08 以降グローバル設定 (Preferences) へ移行済み
        }

        /// <adversarial category="boundary" severity="critical"
        ///   description="JSONの構文や型が不正でもクラッシュせずデフォルトが返ること"
        ///   expected="catch節でnew()が返る" />
        [Theory]
        [InlineData("{invalid json")]
        [InlineData("")]
        [InlineData("[1,2,3]")]
        [InlineData("<xml/>")]
        public void Get_InvalidJson_ReturnsDefaultWithoutCrash(string content)
        {
            var dir = Path.Combine(_tempDir, $"invalid_json_{content.GetHashCode():X}");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "komorebi.settings"), content);

            var ex = Record.Exception(() => Komorebi.Models.RepositorySettings.Get(dir));

            Assert.Null(ex);
        }

        /// <adversarial category="boundary" severity="high"
        ///   description="JSON 'null' をデシリアライズしても NRE を起こさず、空の設定インスタンスへフォールバックする"
        ///   expected="例外なしで新規 RepositorySettings が返る" />
        [Fact]
        public void Get_JsonNull_ReturnsFreshInstanceWithoutCrash()
        {
            var dir = Path.Combine(_tempDir, "json_null_bug");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "komorebi.settings"), "null");

            Komorebi.Models.RepositorySettings result = null;
            var ex = Record.Exception(() => { result = Komorebi.Models.RepositorySettings.Get(dir); });

            // 以前のバグ: "null" JSON を Deserialize すると null が返り、同期の `setting._file = fullpath` と
            // 非同期の Task.Run クロージャーの両方で NRE が発生した。
            // 修正後: setting ??= new() で新規インスタンスへフォールバックするため、例外なしで returns。
            Assert.Null(ex);
            Assert.NotNull(result);
        }

        /// <adversarial category="boundary" severity="medium"
        ///   description="巨大なJSON（100KB超）でもパースできること"
        ///   expected="正常にパースされる" />
        [Fact]
        public void Get_LargeJson_ParsesSuccessfully()
        {
            var dir = Path.Combine(_tempDir, "large_json_repo");
            Directory.CreateDirectory(dir);

            // 巨大なCommitMessagesリストを持つJSON
            var settings = new Komorebi.Models.RepositorySettings();
            for (var i = 0; i < 10; i++)
            {
                settings.CommitMessages.Add(new string('x', 10_000) + $"_{i}");
            }
            var json = JsonSerializer.Serialize(settings, JsonCodeGen.Default.RepositorySettings);
            File.WriteAllText(Path.Combine(dir, "komorebi.settings"), json);

            var loaded = Komorebi.Models.RepositorySettings.Get(dir);
            Assert.Equal(10, loaded.CommitMessages.Count);
        }

        // ===============================================================
        // 💀 並行アクセステスト
        // ===============================================================

    }
}
