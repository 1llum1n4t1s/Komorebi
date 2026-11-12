// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Azure.AI.OpenAI;

using OpenAI;
using OpenAI.Chat;

namespace Komorebi.AI;

/// <summary>
/// OpenAI / Azure OpenAI / Gemini を共通の OpenAI SDK 経路で扱う生成戦略。
/// Provider 別の <see cref="OpenAIClient"/> 構築だけが分岐し、
/// 以降のチャット呼び出し / ツール処理は共通フローとなる。
/// </summary>
internal sealed class OpenAISdkStrategy(Service service) : IGenerationStrategy
{
    public async Task GenerateCommitMessageAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation, string? amendParent = null, string? currentBranch = null)
    {
        // HTTPS 強制で API key の平文流出を防ぐ (OpenAI/Azure/Gemini 共通)
        service.ValidateServerScheme();

        var client = CreateClient();
        var chatClient = client.GetChatClient(service.Model);
        var options = new ChatCompletionOptions() { Tools = { ChatTools.GetDetailChangesInFile } };
        // SDK 2.13 の推論量プロパティは試験的 API として公開されている。
#pragma warning disable OPENAI001
        if (!string.IsNullOrEmpty(service.ReasoningEffortLevel) && service.ReasoningEffortLevel != "unspecified")
            options.ReasoningEffortLevel = new ChatReasoningEffortLevel(service.ReasoningEffortLevel);
#pragma warning restore OPENAI001

        List<ChatMessage> messages = [new UserChatMessage(Agent.BuildUserMessage(service, repo, changeList, currentBranch))];

        var iterations = 0;
        do
        {
            if (++iterations > Agent.MaxToolCallIterations)
                throw new InvalidOperationException(
                    $"Tool call loop exceeded {Agent.MaxToolCallIterations} iterations. " +
                    "This may indicate a prompt injection or model malfunction.");

            ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options, cancellation);
            var inProgress = false;

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    onUpdate?.Invoke(string.Empty);
                    onUpdate?.Invoke("# Assistant");
                    if (completion.Content.Count > 0)
                    {
                        // upstream 39fdc1af: 応答全体を囲むコードフェンスを除去してから通知する
                        var text = Agent.TrimCodeFence(completion.Content[0].Text);
                        onUpdate?.Invoke(text.Length > 0 ? text : "[No content was generated.]");
                    }
                    else
                    {
                        onUpdate?.Invoke("[No content was generated.]");
                    }

                    onUpdate?.Invoke(string.Empty);
                    onUpdate?.Invoke("# Token Usage");
                    onUpdate?.Invoke($"Total: {completion.Usage.TotalTokenCount}. Input: {completion.Usage.InputTokenCount}. Output: {completion.Usage.OutputTokenCount}");
                    break;
                case ChatFinishReason.Length:
                    throw new InvalidOperationException("The response was cut off because it reached the maximum length. Consider increasing the max tokens limit.");
                case ChatFinishReason.ToolCalls:
                    {
                        var message = new AssistantChatMessage(completion);

                        // upstream d3acc780/838c5d1c: thinking モードを無効化する代わりに、
                        // 応答に含まれる reasoning_content をそのまま次のリクエストへ送り返す。
                        // これにより Anthropic / Qwen 系プロバイダの思考プロセスをモデルに保持させたまま
                        // ツール呼び出しの往復を継続できる (Patch API は実験的なので SCME0001 抑止)。
#pragma warning disable SCME0001
                        var hasReasoningContent = completion.Patch.TryGetValue("$.choices[0].message.reasoning_content"u8, out string reasoning);
                        if (hasReasoningContent)
                            message.Patch.Set("$.reasoning_content"u8, reasoning);
#pragma warning restore SCME0001

                        messages.Add(message);

                        foreach (var call in completion.ToolCalls)
                        {
                            var result = await ChatTools.ProcessAsync(call, repo, onUpdate, amendParent, cancellation);
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

    private OpenAIClient CreateClient()
    {
        if (service.Provider == Provider.AzureOpenAI ||
            (service.Provider == Provider.OpenAI && !string.IsNullOrEmpty(service.Server) &&
             service.Server.Contains("openai.azure.com", StringComparison.Ordinal)))
        {
            var azureOptions = new AzureOpenAIClientOptions();
            ApplyExtraHeaders(azureOptions);
            return new AzureOpenAIClient(new Uri(service.Server), service.Credential, azureOptions);
        }

        var options = new OpenAIClientOptions();
        if (!string.IsNullOrEmpty(service.Server))
            options.Endpoint = new Uri(service.Server);
        else if (service.Provider == Provider.Gemini)
            options.Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/");
        ApplyExtraHeaders(options);
        return new OpenAIClient(service.Credential, options);
    }

    private void ApplyExtraHeaders(ClientPipelineOptions options)
    {
        if (!string.IsNullOrWhiteSpace(service.ExtraHeaders))
            options.AddPolicy(new ExtraHeadersPolicy(service.ExtraHeaders), PipelinePosition.PerCall);
    }
}
