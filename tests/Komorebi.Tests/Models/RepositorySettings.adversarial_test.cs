using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// RepositorySettings に対する嫌がらせテスト。
    /// キャッシュ無効化欠如、Encoding.Defaultバグ、PushCommitMessage境界値、
    /// 並行アクセスを攻撃する。
    /// </summary>
    public class RepositorySettingsAdversarialTests : IDisposable
    {
        private readonly string _tempDir;

        public RepositorySettingsAdversarialTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"komorebi_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            ClearCache();
        }

        public void Dispose()
        {
            ClearCache();
            try { Directory.Delete(_tempDir, true); } catch { /* テストクリーンアップ */ }
        }

        /// <summary>
        /// staticキャッシュをリフレクションでクリアする。
        /// テスト間の分離のために必須。
        /// </summary>
        private static void ClearCache()
        {
            var cacheField = typeof(Komorebi.Models.RepositorySettings)
                .GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static)!;
            var cache = (Dictionary<string, Komorebi.Models.RepositorySettings>)cacheField.GetValue(null)!;
            cache.Clear();
        }

        /// <summary>
        /// privateフィールド _file を取得するヘルパー。
        /// </summary>
        private static string GetFilePath(Komorebi.Models.RepositorySettings settings)
        {
            var field = typeof(Komorebi.Models.RepositorySettings)
                .GetField("_file", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (string)field.GetValue(settings)!;
        }

        /// <summary>
        /// privateフィールド _orgHash を取得するヘルパー。
        /// </summary>
        private static string GetOrgHash(Komorebi.Models.RepositorySettings settings)
        {
            var field = typeof(Komorebi.Models.RepositorySettings)
                .GetField("_orgHash", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (string)field.GetValue(settings)!;
        }

        /// <summary>
        /// private static HashContent をリフレクション経由で呼び出すヘルパー。
        /// </summary>
        private static string InvokeHashContent(string source)
        {
            var method = typeof(Komorebi.Models.RepositorySettings)
                .GetMethod("HashContent", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (string)method.Invoke(null, [source])!;
        }

        // ===============================================================
        // 🌪️ Encoding.Default バグ検証（最重要）
        // ===============================================================

        /// <adversarial category="chaos" severity="critical"
        ///   description="HashContentがEncoding.Defaultを使用しており、UTF-8環境とLatin1環境でハッシュが変わるバグ"
        ///   expected="UTF-8のバイト列でハッシュすべきだがEncoding.Defaultはプラットフォーム依存" />
        /// <remarks>
        /// これは実際のバグ: Encoding.Defaultは.NET Coreではほとんどの場合UTF-8だが、
        /// Windows環境のレガシーアプリではcp932やcp1252の可能性がある。
        /// プラットフォーム間で設定ファイルを共有する場合にハッシュ不一致が発生しうる。
        /// </remarks>
        [Fact]
        public void HashContent_EncodingDefault_DiffersFromUtf8ForNonAscii()
        {
            // 非ASCII文字を含むJSON（日本語コミットメッセージ等）
            var content = "{\"DefaultRemote\":\"origin\",\"CommitMessages\":[\"修正: バグ修正\"]}";

            // 実際のHashContent（Encoding.Default使用）
            var actualHash = InvokeHashContent(content);

            // UTF-8で計算した場合のハッシュ
            var utf8Hash = Convert.ToHexString(
                MD5.HashData(Encoding.UTF8.GetBytes(content))
            ).ToLowerInvariant();

            // Encoding.DefaultがUTF-8の環境ではこれらは一致する
            // しかしEncoding.DefaultがUTF-8でない環境では不一致になる
            // .NET Core/5+ではほぼUTF-8なので一致するはずだが、
            // これはEncoding.Defaultを使うべきではない理由の記録
            if (Encoding.Default.CodePage == Encoding.UTF8.CodePage)
            {
                Assert.Equal(utf8Hash, actualHash);
            }
            else
            {
                // Encoding.DefaultがUTF-8でない環境（レガシーWindows等）
                // ここでは不一致になりうることを記録
                Assert.NotEqual(utf8Hash, actualHash);
            }
        }

        /// <adversarial category="chaos" severity="high"
        ///   description="ASCII文字のみの場合、どのエンコーディングでもハッシュが同一であること"
        ///   expected="ASCII範囲ではEncoding.DefaultとUTF-8が同一結果" />
        [Fact]
        public void HashContent_AsciiOnly_ConsistentAcrossEncodings()
        {
            var content = "{\"DefaultRemote\":\"origin\"}";

            var actualHash = InvokeHashContent(content);
            var utf8Hash = Convert.ToHexString(
                MD5.HashData(Encoding.UTF8.GetBytes(content))
            ).ToLowerInvariant();

            // ASCIIのみならどのエンコーディングでも同一バイト列
            Assert.Equal(utf8Hash, actualHash);
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

        /// <adversarial category="resource" severity="high"
        ///   description="異なるパスで大量のGet()呼び出しがメモリリークしないか確認"
        ///   expected="各パスにつき1エントリがキャッシュに残る" />
        [Fact]
        public void Get_ManyDifferentPaths_CacheGrowsUnbounded()
        {
            // 100個の異なるディレクトリを作成してGet()
            for (var i = 0; i < 100; i++)
            {
                var dir = Path.Combine(_tempDir, $"repo_{i}");
                Directory.CreateDirectory(dir);
                Komorebi.Models.RepositorySettings.Get(dir);
            }

            // キャッシュサイズを確認
            var cacheField = typeof(Komorebi.Models.RepositorySettings)
                .GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static)!;
            var cache = (Dictionary<string, Komorebi.Models.RepositorySettings>)cacheField.GetValue(null)!;

            // キャッシュはクリアされない → 100エントリ全て残る
            Assert.Equal(100, cache.Count);
        }

        // ===============================================================
        // 🗡️ PushCommitMessage 境界値テスト
        // ===============================================================

        /// <adversarial category="boundary" severity="high"
        ///   description="PushCommitMessageで10件を超えた場合、9件に切り詰められること"
        ///   expected="最大10件（9件切り詰め + 1件挿入 = 10件）" />
        [Fact]
        public void PushCommitMessage_OverflowAt10_TruncatesTo10()
        {
            var settings = new Komorebi.Models.RepositorySettings();

            // 12件追加
            for (var i = 0; i < 12; i++)
            {
                settings.PushCommitMessage($"message_{i}");
            }

            // 最大10件に制限される
            Assert.True(settings.CommitMessages.Count <= 10,
                $"コミットメッセージは最大10件のはずだが{settings.CommitMessages.Count}件");
        }

        /// <adversarial category="boundary" severity="high"
        ///   description="9件超の切り詰めロジック: 10件ある状態で新しいメッセージを追加"
        ///   expected="古いメッセージが削除されて10件を維持" />
        [Fact]
        public void PushCommitMessage_ExactlyTenThenPushNew_StaysAtTen()
        {
            var settings = new Komorebi.Models.RepositorySettings();

            // まず10件追加
            for (var i = 0; i < 10; i++)
            {
                settings.PushCommitMessage($"msg_{i}");
            }
            Assert.Equal(10, settings.CommitMessages.Count);

            // 11件目を追加
            settings.PushCommitMessage("new_message");

            // RemoveRange(9, Count - 9) → 10件目以降を削除 → 9件にしてから Insert(0, ...)
            Assert.Equal(10, settings.CommitMessages.Count);
            Assert.Equal("new_message", settings.CommitMessages[0]);
        }

        /// <adversarial category="state" severity="medium"
        ///   description="重複メッセージは先頭に移動されること"
        ///   expected="既存メッセージがindex 0に移動" />
        [Fact]
        public void PushCommitMessage_DuplicateMessage_MovesToFront()
        {
            var settings = new Komorebi.Models.RepositorySettings();
            settings.PushCommitMessage("first");
            settings.PushCommitMessage("second");
            settings.PushCommitMessage("third");

            // "first"はindex 2にある → 先頭に移動
            settings.PushCommitMessage("first");

            Assert.Equal(3, settings.CommitMessages.Count); // 件数は変わらない
            Assert.Equal("first", settings.CommitMessages[0]);
        }

        /// <adversarial category="state" severity="medium"
        ///   description="既に先頭にあるメッセージを再度pushしても何も変わらないこと"
        ///   expected="existIdx == 0で早期リターン" />
        [Fact]
        public void PushCommitMessage_AlreadyAtFront_NoChange()
        {
            var settings = new Komorebi.Models.RepositorySettings();
            settings.PushCommitMessage("only");

            settings.PushCommitMessage("only");

            Assert.Single(settings.CommitMessages);
            Assert.Equal("only", settings.CommitMessages[0]);
        }

        /// <adversarial category="boundary" severity="medium"
        ///   description="空白のみのメッセージがtrim後に空文字として扱われること"
        ///   expected="trim後の空文字が格納される" />
        [Fact]
        public void PushCommitMessage_WhitespaceOnly_StoredAsTrimmedEmpty()
        {
            var settings = new Komorebi.Models.RepositorySettings();
            settings.PushCommitMessage("   \t  \n  ");

            // Trim()後は空文字 → ReplaceLineEndings("\n") → ""
            Assert.Single(settings.CommitMessages);
            Assert.Equal("", settings.CommitMessages[0]);
        }

        /// <adversarial category="boundary" severity="medium"
        ///   description="CRLF改行がLFに正規化されること"
        ///   expected="\\r\\nが\\nに変換される" />
        [Fact]
        public void PushCommitMessage_CRLFNormalization()
        {
            var settings = new Komorebi.Models.RepositorySettings();
            settings.PushCommitMessage("line1\r\nline2\r\nline3");

            Assert.Equal("line1\nline2\nline3", settings.CommitMessages[0]);
        }

        /// <adversarial category="state" severity="high"
        ///   description="CRLF正規化後に同一内容になるメッセージが重複として検出されること"
        ///   expected="正規化後の文字列でIndexOfが一致" />
        [Fact]
        public void PushCommitMessage_NormalizedDuplicate_DetectedAsDuplicate()
        {
            var settings = new Komorebi.Models.RepositorySettings();
            settings.PushCommitMessage("line1\nline2");
            settings.PushCommitMessage("second msg");

            // CRLF版を追加 → 正規化後は "line1\nline2" と同じ
            settings.PushCommitMessage("line1\r\nline2");

            Assert.Equal(2, settings.CommitMessages.Count); // 3件にはならない
            Assert.Equal("line1\nline2", settings.CommitMessages[0]); // 先頭に移動
        }

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
        ///   description="不正なJSON（壊れた構文）でもクラッシュせずデフォルトが返ること"
        ///   expected="catch節でnew()が返る" />
        [Theory]
        [InlineData("{invalid json")]
        [InlineData("")]
        public void Get_InvalidJsonSyntax_ReturnsDefaultWithoutCrash(string content)
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

        [Theory]
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

        /// <adversarial category="chaos" severity="high"
        ///   description="SaveAsyncが例外を飲み込むこと（ファイルロック時など）"
        ///   expected="例外は外に出ない" />
        [Fact]
        public async Task SaveAsync_SwallowsAllExceptions()
        {
            var dir = Path.Combine(_tempDir, "save_error_repo");
            Directory.CreateDirectory(dir);
            var settings = Komorebi.Models.RepositorySettings.Get(dir);

            // _fileフィールドを不正なパスに書き換え
            var fileField = typeof(Komorebi.Models.RepositorySettings)
                .GetField("_file", BindingFlags.NonPublic | BindingFlags.Instance)!;
            fileField.SetValue(settings, Path.Combine(_tempDir, "nonexistent_dir", "sub", "settings"));

            // SaveAsyncはcatchで全例外を飲み込む
            var ex = await Record.ExceptionAsync(() => settings.SaveAsync());
            Assert.Null(ex);
        }

        // ===============================================================
        // 💀 並行アクセステスト
        // ===============================================================

    }
}
