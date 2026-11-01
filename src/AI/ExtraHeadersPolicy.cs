using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.AI;

/// <summary>OpenAI互換サービスの追加HTTPヘッダーを適用する。</summary>
internal sealed class ExtraHeadersPolicy(string headers) : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Apply(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Apply(message);
        await ProcessNextAsync(message, pipeline, currentIndex);
    }

    internal static IEnumerable<KeyValuePair<string, string>> Parse(string text)
    {
        foreach (var line in (text ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (string.IsNullOrEmpty(value) || !IsValidName(name))
                continue;

            var hasControl = false;
            foreach (var ch in value)
                hasControl |= char.IsControl(ch) && ch != '\t';
            if (!hasControl)
                yield return new KeyValuePair<string, string>(name, value);
        }
    }

    private static bool IsValidName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        foreach (var ch in name)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && !"!#$%&'*+-.^_`|~".Contains(ch))
                return false;
        }
        return true;
    }

    private void Apply(PipelineMessage message)
    {
        foreach (var header in Parse(headers))
            message.Request.Headers.Set(header.Key, header.Value);
    }
}
