using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;

namespace Komorebi.AI;

public class Agent
{
    private static readonly HttpClient s_httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };
    private const string AnthropicApiVersion = "2023-06-01";
    private const int AnthropicMaxTokens = 4096;

    public Agent(Service service)
    {
        _service = service;
    }

    public Task GenerateCommitMessageAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation)
    {
        return _service.Provider switch
        {
            Provider.Anthropic => GenerateWithAnthropicAsync(repo, changeList, onUpdate, cancellation),
            _ => GenerateWithOpenAISdkAsync(repo, changeList, onUpdate, cancellation),
        };
    }

    private async Task GenerateWithOpenAISdkAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation)
    {
        OpenAIClient client;
        switch (_service.Provider)
        {
            case Provider.AzureOpenAI:
                client = new AzureOpenAIClient(new Uri(_service.Server), _service.Credential);
                break;
            case Provider.Gemini:
                var geminiEndpoint = string.IsNullOrEmpty(_service.Server)
                    ? "https://generativelanguage.googleapis.com/v1beta/openai/"
                    : _service.Server;
                client = new OpenAIClient(_service.Credential, new() { Endpoint = new Uri(geminiEndpoint) });
                break;
            default: // OpenAI（旧設定ファイルの Azure フォールバックを含む）
                if (!string.IsNullOrEmpty(_service.Server) &&
                    _service.Server.Contains("openai.azure.com", StringComparison.Ordinal))
                    client = new AzureOpenAIClient(new Uri(_service.Server), _service.Credential);
                else if (string.IsNullOrEmpty(_service.Server))
                    client = new OpenAIClient(_service.Credential);
                else
                    client = new OpenAIClient(_service.Credential, new() { Endpoint = new Uri(_service.Server) });
                break;
        }

        var chatClient = client.GetChatClient(_service.Model);
        var options = new ChatCompletionOptions() { Tools = { ChatTools.GetDetailChangesInFile } };

        List<ChatMessage> messages = [new UserChatMessage(BuildUserMessage(repo, changeList))];

        do
        {
            ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options, cancellation);
            var inProgress = false;

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    onUpdate?.Invoke(string.Empty);
                    onUpdate?.Invoke("# Assistant");
                    if (completion.Content.Count > 0)
                        onUpdate?.Invoke(completion.Content[0].Text);
                    else
                        onUpdate?.Invoke("[No content was generated.]");

                    onUpdate?.Invoke(string.Empty);
                    onUpdate?.Invoke("# Token Usage");
                    onUpdate?.Invoke($"Total: {completion.Usage.TotalTokenCount}. Input: {completion.Usage.InputTokenCount}. Output: {completion.Usage.OutputTokenCount}");
                    break;
                case ChatFinishReason.Length:
                    throw new InvalidOperationException("The response was cut off because it reached the maximum length. Consider increasing the max tokens limit.");
                case ChatFinishReason.ToolCalls:
                    {
                        messages.Add(new AssistantChatMessage(completion));

                        foreach (var call in completion.ToolCalls)
                        {
                            var result = await ChatTools.ProcessAsync(call, repo, onUpdate);
                            messages.Add(result);
                        }

                        inProgress = true;
                        break;
                    }
                case ChatFinishReason.ContentFilter:
                    throw new InvalidOperationException("Omitted content due to a content filter flag");
                default:
                    break;
            }

            if (!inProgress)
                break;
        } while (true);
    }

    private async Task GenerateWithAnthropicAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation)
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
            new JsonObject { ["role"] = "user", ["content"] = BuildUserMessage(repo, changeList) }
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

    private string BuildUserMessage(string repo, string changeList)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Generate a commit message (follow the rule of conventional commit message) for given git repository.")
          .AppendLine("- Read all given changed files before generating. Only binary files (such as images, audios ...) can be skipped.")
          .AppendLine("- Output the conventional commit message (with detail changes in list) directly. Do not explain your output nor introduce your answer.");

        if (!string.IsNullOrEmpty(_service.AdditionalPrompt))
            sb.AppendLine(_service.AdditionalPrompt);

        sb.Append("Repository path: ").AppendLine(repo.Quoted())
          .AppendLine("Changed files ('A' means added, 'M' means modified, 'D' means deleted, 'T' means type changed, 'R' means renamed, 'C' means copied): ")
          .Append(changeList);

        return sb.ToString();
    }

    private readonly Service _service;
}
