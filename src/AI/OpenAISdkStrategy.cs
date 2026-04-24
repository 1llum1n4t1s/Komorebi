using System;
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
internal sealed class OpenAISdkStrategy : IGenerationStrategy
{
    private readonly Service _service;

    public OpenAISdkStrategy(Service service)
    {
        _service = service;
    }

    public async Task GenerateCommitMessageAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation)
    {
        var client = CreateClient();
        var chatClient = client.GetChatClient(_service.Model);
        var options = new ChatCompletionOptions() { Tools = { ChatTools.GetDetailChangesInFile } };

        List<ChatMessage> messages = [new UserChatMessage(Agent.BuildUserMessage(_service, repo, changeList))];

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

    private OpenAIClient CreateClient()
    {
        switch (_service.Provider)
        {
            case Provider.AzureOpenAI:
                return new AzureOpenAIClient(new Uri(_service.Server), _service.Credential);
            case Provider.Gemini:
                var geminiEndpoint = string.IsNullOrEmpty(_service.Server)
                    ? "https://generativelanguage.googleapis.com/v1beta/openai/"
                    : _service.Server;
                return new OpenAIClient(_service.Credential, new() { Endpoint = new Uri(geminiEndpoint) });
            default:
                // OpenAI（旧設定ファイルの Azure フォールバックを含む）
                if (!string.IsNullOrEmpty(_service.Server) &&
                    _service.Server.Contains("openai.azure.com", StringComparison.Ordinal))
                    return new AzureOpenAIClient(new Uri(_service.Server), _service.Credential);
                if (string.IsNullOrEmpty(_service.Server))
                    return new OpenAIClient(_service.Credential);
                return new OpenAIClient(_service.Credential, new() { Endpoint = new Uri(_service.Server) });
        }
    }
}
