using System;
using System.Collections.Generic;
using System.Text;
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
    public async Task GenerateCommitMessageAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation)
    {
        // HTTPS 強制で API key の平文流出を防ぐ (OpenAI/Azure/Gemini 共通)
        service.ValidateServerScheme();

        var client = CreateClient();
        var chatClient = client.GetChatClient(service.Model);
        var options = new ChatCompletionOptions() { Tools = { ChatTools.GetDetailChangesInFile } };

        // upstream 356ab729: Anthropic / Qwen 系の "thinking" モードがコミットメッセージ生成の応答に
        // 思考プロセス本文を混入させるのを抑制する (Patch API は実験的なので SCME0001 抑止)。
#pragma warning disable SCME0001
        options.Patch.Set("$.thinking"u8, Encoding.UTF8.GetBytes("""{"type": "disabled"}"""));
        options.Patch.Set("$.enable_thinking"u8, false);
#pragma warning restore SCME0001

        List<ChatMessage> messages = [new UserChatMessage(Agent.BuildUserMessage(service, repo, changeList))];

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

    private OpenAIClient CreateClient() => service.Provider switch
    {
        Provider.AzureOpenAI => new AzureOpenAIClient(new Uri(service.Server), service.Credential),
        Provider.Gemini => new OpenAIClient(
            service.Credential,
            new()
            {
                Endpoint = new Uri(string.IsNullOrEmpty(service.Server)
                    ? "https://generativelanguage.googleapis.com/v1beta/openai/"
                    : service.Server),
            }),
        // OpenAI（旧設定ファイルの Azure フォールバックを含む）
        _ when !string.IsNullOrEmpty(service.Server) &&
               service.Server.Contains("openai.azure.com", StringComparison.Ordinal)
            => new AzureOpenAIClient(new Uri(service.Server), service.Credential),
        _ when string.IsNullOrEmpty(service.Server)
            => new OpenAIClient(service.Credential),
        _ => new OpenAIClient(service.Credential, new() { Endpoint = new Uri(service.Server) }),
    };
}
