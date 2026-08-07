using Anthropic;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Loop.Engine.Agents.Providers;

/// <summary>
/// Builds an <see cref="IChatClient"/> for a model id, choosing the provider from the id.
///
/// Both providers are reached through the same `Microsoft.Extensions.AI` abstraction, so
/// every agent above this line is provider-agnostic — swapping <c>claude-sonnet-5</c> for
/// <c>gpt-5</c> in configuration changes nothing else.
/// </summary>
public static class ChatClientFactory
{
    public static IChatClient Create(string model, AiOptions options)
    {
        var id = model.Trim();
        var provider = ModelProviders.Resolve(id);
        var key = options.ResolveKey(provider);

        return provider switch
        {
            ModelProvider.Anthropic => CreateAnthropic(id, key),
            ModelProvider.OpenAi => CreateOpenAi(id, key),
            _ => throw new InvalidOperationException($"Unsupported provider for '{id}'."),
        };
    }

    // A null key is the normal path: the SDK reads the provider's environment variable.
    private static IChatClient CreateAnthropic(string model, string? key) =>
        (key is null ? new AnthropicClient() : new AnthropicClient { ApiKey = key })
            .AsIChatClient(model);

    private static IChatClient CreateOpenAi(string model, string? key) =>
        (key is null ? new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
                     : new OpenAIClient(key))
            .GetChatClient(model)
            .AsIChatClient();
}
