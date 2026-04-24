using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class InstalledFontTests
    {
        // ResolveDefaultFont / ResolveMonospaceFont は設定画面の ComboBox に
        // そのまま食わせる前提なので、必ずカンマを含まない単一フォント名を返さなければならない。
        // テスト環境では FontManager.Current が初期化されていないため、
        // PickFirstInstalled は候補リスト先頭にフォールバックする経路を通る。
        // この経路でも単一名が返ることを保証する。
        [Theory]
        [InlineData("ja_JP")]
        [InlineData("zh_CN")]
        [InlineData("zh_TW")]
        [InlineData("ko_KR")]
        [InlineData("en_US")]
        [InlineData("de_DE")]
        [InlineData("unknown_locale")]
        public void ResolveDefaultFont_ReturnsSingleName(string locale)
        {
            var result = InstalledFont.ResolveDefaultFont(locale);

            Assert.False(string.IsNullOrEmpty(result));
            Assert.DoesNotContain(',', result);
            Assert.Equal(result.Trim(), result);
        }

        [Theory]
        [InlineData("ja_JP")]
        [InlineData("zh_CN")]
        [InlineData("zh_TW")]
        [InlineData("ko_KR")]
        [InlineData("en_US")]
        [InlineData("de_DE")]
        [InlineData("unknown_locale")]
        public void ResolveMonospaceFont_ReturnsSingleName(string locale)
        {
            var result = InstalledFont.ResolveMonospaceFont(locale);

            Assert.False(string.IsNullOrEmpty(result));
            Assert.DoesNotContain(',', result);
            Assert.Equal(result.Trim(), result);
        }

        // GetLocaleDefaults はフォールバックチェーン用にカンマ区切り文字列を返す既存仕様を維持する。
        [Fact]
        public void GetLocaleDefaults_KeepsCommaSeparatedCandidates()
        {
            var (def, mono) = InstalledFont.GetLocaleDefaults("ja_JP");

            Assert.Contains(',', def);
            Assert.Contains(',', mono);
        }
    }
}
