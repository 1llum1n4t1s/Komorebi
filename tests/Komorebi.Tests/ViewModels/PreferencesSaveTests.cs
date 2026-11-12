using System.Reflection;
using Komorebi.ViewModels;

namespace Komorebi.Tests.ViewModels
{
    /// <summary>
    /// Preferences.Save の同一内容スキップを検証する。グローバルな Native.OS.DataDir は書き換えず、
    /// internal の SaveTo で一時ディレクトリへ保存する。
    /// </summary>
    public sealed class PreferencesSaveTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"komorebi-pref-save-{Guid.NewGuid():N}");

        public PreferencesSaveTests()
        {
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch
            {
                // 後始末の失敗はテスト結果に影響させない
            }
        }

        /// <summary>実アプリの Load 完了後と同じく、読み込み中フラグを下ろして保存を許可する。</summary>
        private static Preferences CreateWritable()
        {
            var preferences = new Preferences();
            typeof(Preferences).GetField("_isLoading", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(preferences, false);
            preferences.SetCanModify();
            return preferences;
        }

        [Fact]
        public void SaveTo_SameContent_LeavesFileAndBackupUntouched_ButRewritesAfterExternalChange()
        {
            var file = Path.Combine(_dir, "preference.json");
            var bak = file + ".bak";
            var preferences = CreateWritable();

            preferences.SaveTo(file);
            Assert.True(File.Exists(file));
            Assert.False(File.Exists(bak));

            // 外部で mtime だけ変えると「書いた直後の状態」ではなくなるので、書き直して .bak を作る
            var sentinel = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(file, sentinel);
            preferences.SaveTo(file);
            Assert.NotEqual(sentinel, File.GetLastWriteTimeUtc(file));
            Assert.True(File.Exists(bak));

            // 同じ内容での再保存は本体・.bak とも変えない
            File.SetLastWriteTimeUtc(bak, sentinel);
            var fileTime = File.GetLastWriteTimeUtc(file);
            var fileBytes = File.ReadAllBytes(file);
            var bakBytes = File.ReadAllBytes(bak);
            preferences.SaveTo(file);
            preferences.SaveTo(file);
            Assert.Equal(fileTime, File.GetLastWriteTimeUtc(file));
            Assert.Equal(fileBytes, File.ReadAllBytes(file));
            Assert.Equal(sentinel, File.GetLastWriteTimeUtc(bak));
            Assert.Equal(bakBytes, File.ReadAllBytes(bak));
            Assert.Empty(Directory.GetFiles(_dir, "*.tmp.*"));
        }

        [Fact]
        public void SaveTo_FileDeletedExternally_IsWrittenAgain()
        {
            var file = Path.Combine(_dir, "preference.json");
            var preferences = CreateWritable();

            preferences.SaveTo(file);
            File.Delete(file);
            preferences.SaveTo(file);

            Assert.True(File.Exists(file));
        }

        [Fact]
        public void SaveTo_ContentChanged_IsWrittenAgain()
        {
            var file = Path.Combine(_dir, "preference.json");
            var preferences = CreateWritable();

            preferences.SaveTo(file);
            var before = File.ReadAllText(file);
            preferences.OpenAIServices.Add(new Komorebi.AI.Service { Name = "content-change-marker" });
            preferences.SaveTo(file);

            Assert.NotEqual(before, File.ReadAllText(file));
            Assert.Contains("content-change-marker", File.ReadAllText(file));
        }
    }
}
