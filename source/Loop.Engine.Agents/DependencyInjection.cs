using Anthropic;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Core.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Loop.Engine.Agents;

public static class DependencyInjection
{
    public static IServiceCollection AddLoopEngineAgents(
        this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<InvestigationOptions>()
            .Bind(configuration.GetSection(InvestigationOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => ResolveApiKey(o) is not null, ApiKeyMissingMessage)
            .Validate(HasAnthropicKeyPrefix, WrongKeyKindMessage)
            .ValidateOnStart();

        services
            .AddOptions<RepositoryOptions>()
            .Bind(configuration.GetSection(RepositoryOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<FileRetriever>();

        services.AddSingleton<IChatClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<InvestigationOptions>>().Value;

            // An empty ApiKey is the normal path: the SDK reads ANTHROPIC_API_KEY itself.
            var client = options.UsesEnvironmentApiKey
                ? new AnthropicClient()
                : new AnthropicClient { ApiKey = options.ApiKey.Trim() };

            return client.AsIChatClient(options.Model);
        });

        services.AddSingleton<IInvestigator, InvestigationAgent>();

        return services;
    }

    /// <summary>
    /// The credential actually in force — config value if set, otherwise the environment
    /// variable the SDK reads. Validating only the config value would let startup pass
    /// while the key the client really uses is missing, surfacing later as a puzzling 401.
    /// </summary>
    private static string? ResolveApiKey(InvestigationOptions options)
    {
        var key = options.UsesEnvironmentApiKey
            ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            : options.ApiKey;

        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    /// <summary>
    /// Catch a wrong-vendor key at startup rather than after a network round trip. An
    /// OpenAI key (<c>sk-proj-…</c>) in this slot otherwise fails as an opaque
    /// "invalid x-api-key" buried in a poll failure, several layers from the real cause.
    /// </summary>
    private static bool HasAnthropicKeyPrefix(InvestigationOptions options) =>
        ResolveApiKey(options) is not { } key || key.StartsWith("sk-ant-", StringComparison.Ordinal);

    private const string ApiKeyMissingMessage =
        "No Anthropic credential found. Set the ANTHROPIC_API_KEY environment variable, " +
        "or supply Anthropic:ApiKey via user-secrets. Never commit the key.";

    private const string WrongKeyKindMessage =
        "The configured Anthropic key does not start with 'sk-ant-'. Anthropic keys look " +
        "like 'sk-ant-api03-…' — an OpenAI key ('sk-proj-…') will not work. Get one from " +
        "console.anthropic.com -> Settings -> API keys.";
}
