// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
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
internal sealed class OpenAISdkStrategy(Service service) : IGenerationStrategy
{
    /// <summary>
    /// tool 呼び出しループの最大反復回数。AI が延々と ToolCalls を返し続ける（プロンプトインジェクション
    /// や API バグ等）場合の無限ループ防止。通常のコミットメッセージ生成では 1〜3 回で収束する。
    /// <see cref="AnthropicHttpStrategy"/> と同じ契約にそろえている。
    /// </summary>
    private const int MaxToolCallIterations = 20;

    public async Task GenerateCommitMessageAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation)
    {
        // HTTPS 強制で API key の平文流出を防ぐ (OpenAI/Azure/Gemini 共通)
        service.ValidateServerScheme();

        var client = CreateClient();
        var chatClient = client.GetChatClient(service.Model);
        var options = new ChatCompletionOptions() { Tools = { ChatTools.GetDetailChangesInFile } };

        List<ChatMessage> messages = [new UserChatMessage(Agent.BuildUserMessage(service, repo, changeList))];

        var iterations = 0;
        do
        {
            if (++iterations > MaxToolCallIterations)
                throw new InvalidOperationException(
                    $"Tool call loop exceeded {MaxToolCallIterations} iterations. " +
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
