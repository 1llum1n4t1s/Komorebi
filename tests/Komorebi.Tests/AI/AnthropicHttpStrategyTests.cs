using Komorebi.AI;

namespace Komorebi.Tests.AI
{
    public class AnthropicHttpStrategyTests
    {
        /// <summary>
        /// 追加ヘッダーの Content-Type（content header）が実際に置き換わること（.NET 11 では
        /// HttpRequestHeaders 側が例外を出さず false を返すため、以前は黙って無視されていた）。
        /// 同名ヘッダーは後勝ちで 1 つ、不正な名前・制御文字入りの値は ExtraHeadersPolicy.Parse の規則どおり無視する。
        /// </summary>
        [Fact]
        public void ApplyExtraHeaders_AppliesContentHeadersAndKeepsValidation()
        {
            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "http://127.0.0.1/");
            request.Content = new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            AnthropicHttpStrategy.ApplyExtraHeaders(request,
                "X-Fixture: first\nContent-Type: application/json\nX-Fixture: last\nBad Name: value\nX-Control: a\u0001b");

            Assert.Equal("application/json", string.Join(",", request.Content.Headers.GetValues("Content-Type")));
            Assert.Equal(new[] { "last" }, request.Headers.GetValues("X-Fixture"));
            Assert.False(request.Headers.Contains("X-Control"));
            Assert.DoesNotContain(request.Headers, h => h.Key.Contains(' '));
        }

        /// <summary>
        /// content header として置き換えるのは Content-Type だけで、Content-Length / Content-Encoding など
        /// 本文と矛盾し得るものは利用者が指定しても送らない。
        /// </summary>
        [Fact]
        public void ApplyExtraHeaders_OnlyContentTypeIsAppliedAsContentHeader()
        {
            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "http://127.0.0.1/");
            request.Content = new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            var originalLength = request.Content.Headers.ContentLength;

            AnthropicHttpStrategy.ApplyExtraHeaders(request,
                "Content-Type: application/vnd.fixture+json\nContent-Length: 99999\nContent-Encoding: gzip\nContent-Language: ja");

            Assert.Equal("application/vnd.fixture+json", request.Content.Headers.ContentType?.MediaType);
            Assert.Equal(originalLength, request.Content.Headers.ContentLength);
            Assert.Empty(request.Content.Headers.ContentEncoding);
            Assert.Empty(request.Content.Headers.ContentLanguage);
            Assert.False(request.Headers.Contains("Content-Encoding"));
        }
    }
}
