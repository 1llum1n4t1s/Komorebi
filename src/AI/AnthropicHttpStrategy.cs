// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Komorebi.AI;

/// <summary>
/// Anthropic Messages API を raw HTTP で呼び出す生成戦略。
/// OpenAI SDK 互換でないため、独自に <see cref="HttpClient"/> を持ち、
/// JSON ペイロードと tool_use ループを手書きする。
/// 将来 Anthropic 用の公式 .NET SDK が安定したら、この戦略は SDK 経路に置換可能。
/// </summary>
internal sealed class AnthropicHttpStrategy(Service service) : IGenerationStrategy
{
    private static readonly HttpClient s_httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60),
        // 異常応答（巨大 thinking ブロック等）が無限に流れ込む OOM を防ぐ。AvatarManager の HttpClient と同方針。
        MaxResponseContentBufferSize = 8 * 1024 * 1024,
    };
    private const string AnthropicApiVersion = "2023-06-01";
    private const int AnthropicMaxTokens = 4096;

    /// <summary>
    /// 追加ヘッダーをリクエストへ適用する。名前・値の検証（不正な名前・制御文字を含む値の除外）は
    /// OpenAI 経路と同じ <see cref="ExtraHeadersPolicy.Parse"/> に任せ、同名ヘッダーは後勝ちで 1 つにする
    /// （OpenAI 経路の PipelineRequestHeaders.Set と同じ結果）。
    /// Content-Type などの content header は HttpRequestHeaders に入れられない。.NET 11 では
    /// HttpRequestHeaders.Remove / TryAddWithoutValidation が例外を投げずに false を返すだけなので、
    /// 以前の「例外なら content header へ」という分岐には到達せず、指定が黙って無視されていた。
    /// 追加できなかった Content-Type だけは content header として置き換える（旧ランタイムの例外経路も残す）。
    /// それ以外の content header（Content-Length / Content-Encoding など）は本文と矛盾し得るので送らない。
    /// </summary>
    internal static void ApplyExtraHeaders(HttpRequestMessage request, string extraHeaders)
    {
        foreach (var header in ExtraHeadersPolicy.Parse(extraHeaders))
        {
            bool added;
            try
            {
                request.Headers.Remove(header.Key);
                added = request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            catch (InvalidOperationException)
            {
                added = false;
            }

            // content header として置き換えるのは Content-Type だけにする。
            // Content-Length・Content-Encoding・Content-MD5 などは本文（JSON）の実体と食い違うと
            // サーバーが本文を誤って解釈する・リクエストが壊れるため、利用者の指定でも送らずに無視する。
            if (!added && request.Content is not null &&
                string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                // content 側でも受け付けられない値（.NET のヘッダー分類で例外になるもの）は、
                // 利用者の追加ヘッダー 1 件の不備で AI リクエスト全体を失敗させないよう無視する。
                try
                {
                    request.Content.Headers.Remove(header.Key);
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                catch (InvalidOperationException)
                {
                    // 追加できないヘッダーは送らない
                }
                catch (FormatException)
                {
                    // ヘッダー名として不正な文字列も送らない
                }
            }
        }
    }

    /// <summary>
    /// Anthropic Messages API の tools 配列を組み立てる。
    /// ツールの名前・説明・入力スキーマは OpenAI 経路と同じ <see cref="ChatTools.GetDetailChangesInFile"/> を正本とし、
    /// 以前のような JSON の手書き複製を持たない（片方だけ更新されて両経路のツール定義がずれるのを防ぐ）。
    /// スキーマのキー順・値は旧手書き定義と同一なので、送信される JSON 文字列は変わらない（AnthropicHttpStrategyTests で固定）。
    /// </summary>
    internal static JsonArray BuildTools()
    {
        var tool = ChatTools.GetDetailChangesInFile;

        // コレクション初期化子は JsonArray.Add<T>(T) を呼ぶが、これは RequiresDynamicCode 属性付きで
        // Native AOT 警告 (IL3050) になる。JsonNode を直接受けるコンストラクタ / Add(JsonNode?) を使えば
        // JsonValue の動的生成を経由しないので AOT 安全。string からの暗黙変換 (JsonValue.Create(string)) も AOT 安全。
        return new JsonArray(
            new JsonObject
            {
                ["name"] = tool.FunctionName,
                ["description"] = tool.FunctionDescription,
                ["input_schema"] = JsonNode.Parse(tool.FunctionParameters.ToString())
            });
    }

    public async Task GenerateCommitMessageAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation, string? amendParent = null, string? currentBranch = null)
    {
        // HTTPS 強制で API key の平文流出を防ぐ
        service.ValidateServerScheme();

        var baseUrl = string.IsNullOrEmpty(service.Server) ? "https://api.anthropic.com" : service.Server.TrimEnd('/');

        // JsonArray/JsonObject は JsonNode を直接受けるコンストラクタで組み立てる（IL3050 回避。BuildTools 参照）。
        var tools = BuildTools();

        var messages = new JsonArray(
            new JsonObject { ["role"] = "user", ["content"] = Agent.BuildUserMessage(service, repo, changeList, currentBranch) });

        var iterations = 0;
        do
        {
            if (++iterations > Agent.MaxToolCallIterations)
                throw new InvalidOperationException(
                    $"Anthropic tool_use loop exceeded {Agent.MaxToolCallIterations} iterations. " +
                    "This may indicate a prompt injection or model malfunction.");

            // messages.DeepClone() は tool_use ループの反復ごとに
            // O(n) のコピーが走るため、メッセージ列が伸びるたびに合計 O(n²) になっていた。
            // JsonNode は親が 1 つしか持てない制約があるため DeepClone していたが、
            // 「一旦親に attach → ToJsonString で serialize → Remove で detach」の
            // パターンに置き換えることで clone を省略できる。Remove は子の Parent を null
            // に戻すので、次の反復で再 attach できる。
            var requestBody = new JsonObject
            {
                ["model"] = service.Model,
                ["max_tokens"] = AnthropicMaxTokens,
                ["tools"] = tools,
                ["messages"] = messages
            };
            var requestBodyJson = requestBody.ToJsonString();
            // 次の反復で tools / messages を再利用できるよう、明示的に detach する
            requestBody.Remove("tools");
            requestBody.Remove("messages");

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/messages");
            request.Headers.Add("x-api-key", service.ResolvedApiKey);
            request.Headers.Add("anthropic-version", AnthropicApiVersion);
            request.Content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");
            ApplyExtraHeaders(request, service.ExtraHeaders);
            using var response = await s_httpClient.SendAsync(request, cancellation);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync(cancellation);
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;
            var stopReason = root.GetProperty("stop_reason").GetString();
            var content = root.GetProperty("content");

            // end_turn: 正常終了、max_tokens: トークン上限で打ち切り（部分出力のまま終了）。
            // max_tokens では部分結果に打ち切りの注記を添えて返す。OpenAI 経路の ChatFinishReason.Length は
            // 例外で失敗させるため、上限到達時の挙動は両経路で意図的に異なる（Anthropic 経路は部分結果を残す）。
            if (stopReason == "end_turn" || stopReason == "max_tokens")
            {
                onUpdate?.Invoke(string.Empty);
                onUpdate?.Invoke("# Assistant");
                var hasContent = false;
                foreach (var item in content.EnumerateArray())
                {
                    if (item.GetProperty("type").GetString() != "text")
                        continue;

                    // upstream 39fdc1af 相当: Anthropic 経路でも応答を囲むコードフェンスを除去する
                    var text = Agent.TrimCodeFence(item.GetProperty("text").GetString() ?? string.Empty);
                    if (text.Length > 0)
                    {
                        onUpdate?.Invoke(text);
                        hasContent = true;
                    }
                }

                // TrimCodeFence の契約 (空になったら呼び出し側でフォールバック表示する) に従い、
                // OpenAISdkStrategy と同じメッセージで無言終了を防ぐ
                if (!hasContent)
                    onUpdate?.Invoke("[No content was generated.]");

                if (stopReason == "max_tokens")
                    onUpdate?.Invoke("\n(note: output was truncated because it reached the maximum token limit)");

                var usage = root.GetProperty("usage");
                var inputTokens = usage.GetProperty("input_tokens").GetInt32();
                var outputTokens = usage.GetProperty("output_tokens").GetInt32();
                onUpdate?.Invoke(string.Empty);
                onUpdate?.Invoke("# Token Usage");
                onUpdate?.Invoke($"Total: {inputTokens + outputTokens}. Input: {inputTokens}. Output: {outputTokens}");
                break;
            }
            else if (stopReason == "tool_use")
            {
                // アシスタントの tool_use メッセージを履歴に追加
                messages.Add((JsonNode)new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = JsonNode.Parse(content.GetRawText())
                });

                // tool_use を抽出して並列実行する。
                // ChatTools.ProcessAnthropicCall は read-only な git diff 取得なので副作用がなく、
                // 1 ターンで複数 tool_use が来ても Task.WhenAll で並列化可能（5〜15 ファイル diff で
                // 0.5〜1.5 秒短縮、tool_use ループが回る分倍増する）。CommitDetail.cs の SHA 検証経路と
                // 同じ並列パターン。
                var toolCalls = new List<(string Id, string Name, JsonElement Input)>();
                foreach (var item in content.EnumerateArray())
                {
                    if (item.GetProperty("type").GetString() != "tool_use")
                        continue;
                    toolCalls.Add((
                        item.GetProperty("id").GetString(),
                        item.GetProperty("name").GetString(),
                        item.GetProperty("input")));
                }

                var toolResultObjects = await Task.WhenAll(toolCalls.Select(c =>
                    ChatTools.ProcessAnthropicCall(c.Id, c.Name, c.Input, repo, onUpdate, amendParent, cancellation))).ConfigureAwait(false);

                var toolResults = new JsonArray();
                foreach (var r in toolResultObjects)
                    toolResults.Add((JsonNode)r);

                messages.Add((JsonNode)new JsonObject { ["role"] = "user", ["content"] = toolResults });
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported Anthropic stop_reason: {stopReason ?? "<null>"}");
            }
        } while (true);
    }
}
