using System;
using System.ClientModel;
using System.Collections.Generic;

namespace Komorebi.AI;

public class Service
{
    public static IReadOnlyList<Provider> AllProviders => [Provider.OpenAI, Provider.AzureOpenAI, Provider.Anthropic, Provider.Gemini];

    public string Name { get; set; }
    public Provider Provider { get; set; } = Provider.OpenAI;
    public string Server { get; set; }
    public string Model { get; set; }
    public string ApiKey { get; set; }
    public bool ReadApiKeyFromEnv { get; set; }
    public string AdditionalPrompt { get; set; }

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
