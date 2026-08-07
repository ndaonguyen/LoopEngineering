namespace Loop.Engine.Agents.Providers;

public enum ModelProvider
{
    Anthropic,
    OpenAi,
}

/// <summary>
/// Works out which provider a model id belongs to.
///
/// Inference rather than a second setting: a <c>Provider</c> field that could disagree with
/// the model id gives you two ways to say one thing and one new way to be wrong. The id
/// already carries the answer.
/// </summary>
public static class ModelProviders
{
    private static readonly string[] AnthropicPrefixes = ["claude-"];

    private static readonly string[] OpenAiPrefixes = ["gpt-", "o1", "o3", "o4", "chatgpt"];

    public static bool TryResolve(string model, out ModelProvider provider)
    {
        provider = default;

        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        var id = model.Trim();

        if (AnthropicPrefixes.Any(p => id.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            provider = ModelProvider.Anthropic;
            return true;
        }

        if (OpenAiPrefixes.Any(p => id.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            provider = ModelProvider.OpenAi;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves or throws. An unrecognised id must fail at startup with the list of
    /// prefixes — silently defaulting to a provider would send an OpenAI key to Anthropic
    /// and surface as an opaque 401 several layers away from the typo that caused it.
    /// </summary>
    public static ModelProvider Resolve(string model)
    {
        if (TryResolve(model, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException(
            $"Cannot tell which provider '{model}' belongs to. Anthropic model ids start " +
            $"with '{string.Join("', '", AnthropicPrefixes)}'; OpenAI ids start with " +
            $"'{string.Join("', '", OpenAiPrefixes)}'. Check Ai:Model and Ai:ReasoningModel.");
    }
}
