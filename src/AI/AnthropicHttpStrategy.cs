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
    /// tool_use ループの最大反復回数。AI が永続的に tool_use を返し続ける（プロンプトインジェクション
    /// や API バグ等）場合の無限ループ防止。通常のコミットメッセージ生成では 1〜3 回で収束する。
    /// </summary>
    private const int MaxToolCallIterations = 20;

    public async Task GenerateCommitMessageAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation)
    {
        // HTTPS 強制で API key の平文流出を防ぐ
        service.ValidateServerScheme();

        var baseUrl = string.IsNullOrEmpty(service.Server) ? "https://api.anthropic.com" : service.Server.TrimEnd('/');

        var tools = new JsonArray
        {
            new JsonObject
            {
                ["name"] = "GetDetailChangesInFile",
                ["description"] = "Get the detailed changes in the specified file in the specified repository.",
                ["input_schema"] = JsonNode.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "repo": {"type": "string", "description": "The path to the repository."},
                        "file": {"type": "string", "description": "The path to the file."},
                        "originalFile": {"type": "string", "description": "The path to the original file when it has been renamed or copied."}
                    },
                    "required": ["repo", "file"]
                }
                """)
            }
        };

        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "user", ["content"] = Agent.BuildUserMessage(service, repo, changeList) }
        };

        var iterations = 0;
        do
        {
            if (++iterations > MaxToolCallIterations)
                throw new InvalidOperationException(
                    $"Anthropic tool_use loop exceeded {MaxToolCallIterations} iterations. " +
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
            using var response = await s_httpClient.SendAsync(request, cancellation);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync(cancellation);
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;
            var stopReason = root.GetProperty("stop_reason").GetString();
            var content = root.GetProperty("content");

            // end_turn: 正常終了、max_tokens: トークン上限で打ち切り（部分出力のまま終了）。
            // max_tokens を未処理にすると InvalidOperationException で落ちるため、
            // OpenAI 側の ChatFinishReason.Length と同等に扱って部分結果を返す。
            if (stopReason == "end_turn" || stopReason == "max_tokens")
            {
                onUpdate?.Invoke(string.Empty);
                onUpdate?.Invoke("# Assistant");
                foreach (var item in content.EnumerateArray())
                {
                    if (item.GetProperty("type").GetString() == "text")
                        onUpdate?.Invoke(item.GetProperty("text").GetString());
                }

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
                messages.Add(new JsonObject
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
                    ChatTools.ProcessAnthropicCall(c.Id, c.Name, c.Input, repo, onUpdate))).ConfigureAwait(false);

                var toolResults = new JsonArray();
                foreach (var r in toolResultObjects)
                    toolResults.Add(r);

                messages.Add(new JsonObject { ["role"] = "user", ["content"] = toolResults });
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported Anthropic stop_reason: {stopReason ?? "<null>"}");
            }
        } while (true);
    }
}
