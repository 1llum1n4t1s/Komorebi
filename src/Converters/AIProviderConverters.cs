using Avalonia.Data.Converters;

namespace Komorebi.Converters;

public static class AIProviderConverters
{
    public static readonly FuncValueConverter<AI.Provider, bool> SupportsReasoningEffort =
        new(provider => provider != AI.Provider.Anthropic);
}
