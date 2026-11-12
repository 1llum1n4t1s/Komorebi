using System.Text.Json;
using Komorebi.AI;
using Komorebi.ViewModels;

namespace Komorebi.Tests.AI
{
    public class ServiceTests
    {
        [Fact]
        public void PreferencesSerialization_EncryptsApiKey()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"komorebi-ai-key-{Guid.NewGuid():N}");
            var oldOverride = ApiKeyProtector.KeyDirectoryOverride;
            try
            {
                ApiKeyProtector.KeyDirectoryOverride = dir;
                var preferences = new Preferences();
                preferences.OpenAIServices.Add(new Service { Name = "OpenAI", ApiKey = "sk-secret-token" });

                var json = JsonSerializer.Serialize(preferences, JsonCodeGen.Default.Preferences);
                Assert.DoesNotContain("sk-secret-token", json);
                Assert.Contains("komorebi:v1:aes:", json);

                var restored = JsonSerializer.Deserialize(json, JsonCodeGen.Default.Preferences);
                Assert.Equal("sk-secret-token", restored!.OpenAIServices.Single().ApiKey);
            }
            finally
            {
                ApiKeyProtector.KeyDirectoryOverride = oldOverride;
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        /// <summary>
        /// 値が変わらない限り同じ暗号文を返す（AES-GCM の乱数ノンスで毎回 preference.json が変わらないようにする）。
        /// 値を変えた・空にしたときは古い暗号文を使わない。読み込んだ暗号文もそのまま再利用する。
        /// </summary>
        [Fact]
        public void ProtectedValues_ReuseCipherUntilValueChanges()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"komorebi-ai-key-{Guid.NewGuid():N}");
            var oldOverride = ApiKeyProtector.KeyDirectoryOverride;
            try
            {
                ApiKeyProtector.KeyDirectoryOverride = dir;
                var service = new Service { ApiKey = "sk-first", ExtraHeaders = "X-Test: first" };

                var keyCipher = service.ProtectedApiKey;
                var headerCipher = service.ProtectedExtraHeaders;
                Assert.StartsWith("komorebi:v1:aes:", keyCipher);
                Assert.Equal(keyCipher, service.ProtectedApiKey);
                Assert.Equal(headerCipher, service.ProtectedExtraHeaders);

                // 同じ値の再設定では暗号文を保つ
                service.ApiKey = "sk-first";
                service.ExtraHeaders = "X-Test: first";
                Assert.Equal(keyCipher, service.ProtectedApiKey);
                Assert.Equal(headerCipher, service.ProtectedExtraHeaders);

                // 値を変えたら新しい暗号文（復号すると新しい値）になる
                service.ApiKey = "sk-second";
                service.ExtraHeaders = "X-Test: second";
                var newKeyCipher = service.ProtectedApiKey;
                var newHeaderCipher = service.ProtectedExtraHeaders;
                Assert.NotEqual(keyCipher, newKeyCipher);
                Assert.NotEqual(headerCipher, newHeaderCipher);
                var restored = new Service { ProtectedApiKey = newKeyCipher, ProtectedExtraHeaders = newHeaderCipher };
                Assert.Equal("sk-second", restored.ApiKey);
                Assert.Equal("X-Test: second", restored.ExtraHeaders);

                // 読み込んだ暗号文は再暗号化せずにそのまま書き戻す
                Assert.Equal(newKeyCipher, restored.ProtectedApiKey);
                Assert.Equal(newHeaderCipher, restored.ProtectedExtraHeaders);

                // 空にしたら古い暗号文を返さない
                service.ApiKey = string.Empty;
                service.ExtraHeaders = string.Empty;
                Assert.Equal(string.Empty, service.ProtectedApiKey);
                Assert.Equal(string.Empty, service.ProtectedExtraHeaders);
            }
            finally
            {
                ApiKeyProtector.KeyDirectoryOverride = oldOverride;
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        /// <summary>
        /// 保持している暗号文の元の平文が現在値と食い違う場合（ワーカースレッドの保存が、UI での値変更による
        /// 暗号文の消去より後に旧値の暗号文を書き戻した状態）でも、旧値の暗号文を保存せず現在値で再暗号化する。
        /// 競合のタイミングは再現せず、setter を経由しない値の書き換えで「対の不一致」の状態だけを作る。
        /// </summary>
        [Fact]
        public void ProtectedValues_StaleCipherForAnotherValue_IsReEncrypted()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"komorebi-ai-key-{Guid.NewGuid():N}");
            var oldOverride = ApiKeyProtector.KeyDirectoryOverride;
            try
            {
                ApiKeyProtector.KeyDirectoryOverride = dir;
                var service = new Service { ApiKey = "sk-old", ExtraHeaders = "X-Test: old" };
                var oldKeyCipher = service.ProtectedApiKey;
                var oldHeaderCipher = service.ProtectedExtraHeaders;

                // setter の消去を経ずに現在値だけが新しい値になった状態（暗号文は旧値のもの）
                SetPrivate(service, "_apiKey", "sk-new");
                SetPrivate(service, "_extraHeaders", "X-Test: new");

                var keyCipher = service.ProtectedApiKey;
                var headerCipher = service.ProtectedExtraHeaders;
                Assert.NotEqual(oldKeyCipher, keyCipher);
                Assert.NotEqual(oldHeaderCipher, headerCipher);
                var restored = new Service { ProtectedApiKey = keyCipher, ProtectedExtraHeaders = headerCipher };
                Assert.Equal("sk-new", restored.ApiKey);
                Assert.Equal("X-Test: new", restored.ExtraHeaders);

                // 再暗号化後は新しい対として再利用される
                Assert.Equal(keyCipher, service.ProtectedApiKey);
                Assert.Equal(headerCipher, service.ProtectedExtraHeaders);

                // 現在値が空なのに別の値の暗号文が残っている場合も、その暗号文を返さない
                // （返してよいのは読み込んだが復号できなかった暗号文だけ）
                SetPrivate(service, "_apiKey", string.Empty);
                SetPrivate(service, "_extraHeaders", string.Empty);
                Assert.Equal(string.Empty, service.ProtectedApiKey);
                Assert.Equal(string.Empty, service.ProtectedExtraHeaders);
            }
            finally
            {
                ApiKeyProtector.KeyDirectoryOverride = oldOverride;
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        private static void SetPrivate(Service service, string field, string value)
            => typeof(Service).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(service, value);
    }
}
