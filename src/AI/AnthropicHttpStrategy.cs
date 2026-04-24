using System;
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
internal sealed class AnthropicHttpStrategy : IGenerationStrategy
{
    private static readonly HttpClient s_httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };
    private const string AnthropicApiVersion = "2023-06-01";
    private const int AnthropicMaxTokens = 4096;

    private readonly Service _service;

    public AnthropicHttpStrategy(Service service)
    {
        _service = service;
    }

    public async Task GenerateCommitMessageAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation)
    {
        var baseUrl = string.IsNullOrEmpty(_service.Server) ? "https://api.anthropic.com" : _service.Server.TrimEnd('/');

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
            new JsonObject { ["role"] = "user", ["content"] = Agent.BuildUserMessage(_service, repo, changeList) }
        };

        do
        {
            var requestBody = new JsonObject
            {
                ["model"] = _service.Model,
                ["max_tokens"] = AnthropicMaxTokens,
                ["tools"] = tools,
                ["messages"] = messages.DeepClone()
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/messages");
            request.Headers.Add("x-api-key", _service.ResolvedApiKey);
            request.Headers.Add("anthropic-version", AnthropicApiVersion);
            request.Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json");
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

                // ツールを実行して結果を収集
                var toolResults = new JsonArray();
                foreach (var item in content.EnumerateArray())
                {
                    if (item.GetProperty("type").GetString() != "tool_use")
                        continue;

                    var toolId = item.GetProperty("id").GetString();
                    var toolName = item.GetProperty("name").GetString();
                    var toolInput = item.GetProperty("input");
                    var toolResult = await ChatTools.ProcessAnthropicCall(toolId, toolName, toolInput, repo, onUpdate);
                    toolResults.Add(toolResult);
                }

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
