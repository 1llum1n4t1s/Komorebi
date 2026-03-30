using System;
using System.ClientModel;

namespace Komorebi.AI;

public class Service
{
    public string Name { get; set; }
    public string Server { get; set; }
    public string Model { get; set; }
    public string ApiKey { get; set; }
    public bool ReadApiKeyFromEnv { get; set; }
    public string AdditionalPrompt { get; set; }
    public ApiKeyCredential Credential
    {
        get
        {
            var key = ReadApiKeyFromEnv ? Environment.GetEnvironmentVariable(ApiKey) : ApiKey;
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("API key is not configured.");

            return new ApiKeyCredential(key);
        }
    }
}
