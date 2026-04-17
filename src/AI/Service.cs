using System;
using System.ClientModel;
using System.Collections.Generic;

namespace Komorebi.AI;

public class Service
{
    public static IReadOnlyList<Provider> AllProviders => [Provider.OpenAI, Provider.AzureOpenAI, Provider.Anthropic, Provider.Gemini];

    public string Name { get; set; } = string.Empty;
    public Provider Provider { get; set; } = Provider.OpenAI;
    public string Server { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool ReadApiKeyFromEnv { get; set; } = false;
    public string AdditionalPrompt { get; set; } = string.Empty;

    public ApiKeyCredential Credential
    {
        get => new ApiKeyCredential(ResolvedApiKey);
    }

    public string ResolvedApiKey
    {
        get
        {
            var key = ReadApiKeyFromEnv ? Environment.GetEnvironmentVariable(ApiKey) : ApiKey;
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("API key is not configured.");
            return key;
        }
    }
}
