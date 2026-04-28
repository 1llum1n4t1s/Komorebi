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
    }
}
