using Loop.Engine.Agents.Providers;
using Loop.Engine.Agents.Coding;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Planning;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Agents.Review;
using Loop.Engine.Agents.Telemetry;
using Loop.Engine.Agents.Verification;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
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
            .AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(HasResolvableProviders, UnknownModelMessage)
            .Validate(HasKeyForEveryProvider, MissingKeyMessage)
            .Validate(HasPlausibleKeyShapes, WrongKeyKindMessage)
            .ValidateOnStart();

        services
            .AddOptions<RepositoryOptions>()
            .Bind(configuration.GetSection(RepositoryOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<FileRetriever>();

        // One accumulator for the whole tick; the pipeline resets it per run.
        services.AddSingleton<RunMetrics>();

        // Two clients, because the stages have genuinely different demands. The default
        // serves the Coder, Reviewer, and repair loop; the "reasoning" key serves
        // Investigation and Planning, where a wrong answer poisons everything downstream.
        // When ReasoningModel is unset both resolve to the same model and this costs
        // nothing but an extra registration.
        services.AddSingleton<IChatClient>(sp =>
            CreateClient(sp, o => o.Model));

        services.AddKeyedSingleton<IChatClient>(ReasoningClientKey, (sp, _) =>
            CreateClient(sp, o => o.EffectiveReasoningModel));

        services
            .AddOptions<VerificationOptions>()
            .Bind(configuration.GetSection(VerificationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<DiffGenerator>();
        services.AddSingleton<FixVerifier>();

        services.AddSingleton<IInvestigator, InvestigationAgent>();
        services.AddSingleton<IPlanner, PlannerAgent>();
        services.AddSingleton<ICoder, CoderAgent>();
        services.AddSingleton<IBuildRunner, DotnetBuildRunner>();
        services.AddSingleton<IReviewer, ReviewerAgent>();

        return services;
    }

    /// <summary>Service key for the client used by Investigation and Planning.</summary>
    public const string ReasoningClientKey = "reasoning";

    /// <summary>
    /// Both clients are metered. Wrapping here rather than in each agent means a new agent
    /// is measured the moment it is written, without anyone remembering to add it.
    /// </summary>
    private static IChatClient CreateClient(
        IServiceProvider services, Func<AiOptions, string> selectModel)
    {
        var options = services.GetRequiredService<IOptions<AiOptions>>().Value;
        var client = ChatClientFactory.Create(selectModel(options), options);

        return new MeteredChatClient(client, services.GetRequiredService<RunMetrics>());
    }

    /// <summary>Both configured ids must belong to a provider we can build a client for.</summary>
    private static bool HasResolvableProviders(AiOptions options) =>
        ModelProviders.TryResolve(options.Model, out _)
        && ModelProviders.TryResolve(options.EffectiveReasoningModel, out _);

    /// <summary>
    /// Every provider named by a configured model needs a key — the configured value or
    /// its environment variable. Checking only the config value would let startup pass
    /// while the credential actually used is missing, surfacing later as a puzzling 401.
    /// </summary>
    private static bool HasKeyForEveryProvider(AiOptions options) =>
        !HasResolvableProviders(options)
        || options.RequiredProviders().All(p => options.ResolveKey(p) is not null);

    /// <summary>
    /// Catch a wrong-vendor key at startup rather than after a network round trip. An
    /// OpenAI key in the Anthropic slot otherwise fails as an opaque "invalid x-api-key"
    /// buried in a poll failure, several layers from the typo that caused it — which is
    /// exactly how an afternoon gets lost.
    /// </summary>
    private static bool HasPlausibleKeyShapes(AiOptions options)
    {
        if (!HasResolvableProviders(options))
        {
            return true;
        }

        foreach (var provider in options.RequiredProviders())
        {
            if (options.ResolveKey(provider) is not { } key)
            {
                continue;
            }

            var valid = provider switch
            {
                // Anthropic is specific; OpenAI issues several shapes (sk-, sk-proj-, …)
                // so only the family prefix is worth asserting.
                ModelProvider.Anthropic => key.StartsWith("sk-ant-", StringComparison.Ordinal),
                ModelProvider.OpenAi => key.StartsWith("sk-", StringComparison.Ordinal)
                                        && !key.StartsWith("sk-ant-", StringComparison.Ordinal),
                _ => true,
            };

            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    private const string UnknownModelMessage =
        "Ai:Model or Ai:ReasoningModel names a model whose provider cannot be determined. " +
        "Anthropic ids start with 'claude-'; OpenAI ids start with 'gpt-', 'o1', 'o3', " +
        "'o4', or 'chatgpt'.";

    private const string MissingKeyMessage =
        "No credential found for a configured provider. Set ANTHROPIC_API_KEY / " +
        "OPENAI_API_KEY in the environment, or supply Ai:AnthropicApiKey / Ai:OpenAiApiKey " +
        "via user-secrets. Never commit a key.";

    private const string WrongKeyKindMessage =
        "A configured key does not match its provider. Anthropic keys start with " +
        "'sk-ant-api03-…'; OpenAI keys start with 'sk-' (commonly 'sk-proj-…'). " +
        "Using one in the other's slot fails as an opaque authentication error at runtime.";
}
