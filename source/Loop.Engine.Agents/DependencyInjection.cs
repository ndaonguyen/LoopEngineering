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
            .Validate(ValidateApiKeyIsReachable, ApiKeyMissingMessage)
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
                : new AnthropicClient { ApiKey = options.ApiKey };

            return client.AsIChatClient(options.Model);
        });

        services.AddSingleton<IInvestigator, InvestigationAgent>();

        return services;
    }

    /// <summary>
    /// Assert on whichever credential is actually in force. Validating only the config
    /// value would let startup pass while the environment variable the client really uses
    /// is missing — the failure would then surface as a puzzling runtime 401.
    /// </summary>
    private static bool ValidateApiKeyIsReachable(InvestigationOptions options) =>
        !options.UsesEnvironmentApiKey
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

    private const string ApiKeyMissingMessage =
        "No Anthropic credential found. Set the ANTHROPIC_API_KEY environment variable, " +
        "or supply Anthropic:ApiKey via user-secrets. Never commit the key.";
}
