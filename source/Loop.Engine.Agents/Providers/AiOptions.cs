using System.ComponentModel.DataAnnotations;

namespace Loop.Engine.Agents.Providers;

/// <summary>
/// Which models the agents use, and the credentials to reach them. Bound from <c>Ai</c>.
///
/// The provider is inferred from each model id — set <c>Model</c> to <c>claude-sonnet-5</c>
/// or <c>gpt-5</c> and the right client is built either way. Supply only the key(s) for the
/// providers you actually name.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>
    /// The workhorse model: Coder, Reviewer, and the repair loop. These stages mostly
    /// transcribe a decision already made, so they need not be the most capable model.
    /// </summary>
    /// <remarks>
    /// Kept in step with <c>appsettings.json</c> on purpose. A default that disagrees with
    /// the shipped configuration only shows up when the section is missing — the one
    /// moment you least want a silent switch to a different provider and a different bill.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// The model for the stages that decide things — Investigation and Planning.
    ///
    /// Diagnosis is where being wrong is most expensive: every later stage inherits the
    /// file list and the plan, and no amount of coding skill recovers from investigating
    /// the wrong file. Leave empty to use <see cref="Model"/> everywhere. It may name a
    /// different provider from <see cref="Model"/> — mixing is supported.
    /// </summary>
    public string ReasoningModel { get; set; } = string.Empty;

    /// <summary>The reasoning model if configured, otherwise the workhorse.</summary>
    public string EffectiveReasoningModel =>
        string.IsNullOrWhiteSpace(ReasoningModel) ? Model.Trim() : ReasoningModel.Trim();

    /// <summary>
    /// Output token budget. Must be generous: current models think by default and the
    /// budget caps thinking <b>plus</b> the response, so a tight value is spent reasoning
    /// and the answer comes back empty or truncated.
    /// </summary>
    [Range(1000, 64000)]
    public int MaxOutputTokens { get; set; } = 16000;

    /// <summary>
    /// Anthropic key. Leave empty to let the SDK read <c>ANTHROPIC_API_KEY</c> from the
    /// environment. Never commit a value.
    /// </summary>
    public string AnthropicApiKey { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI key. Leave empty to let the SDK read <c>OPENAI_API_KEY</c> from the
    /// environment. Never commit a value.
    /// </summary>
    public string OpenAiApiKey { get; set; } = string.Empty;

    /// <summary>Where <c>investigation.md</c>, the diff, and the review are written.</summary>
    [Required(AllowEmptyStrings = false)]
    public string OutputDirectory { get; set; } = "output/investigations";

    /// <summary>Every distinct provider the configured models require.</summary>
    public IReadOnlyList<ModelProvider> RequiredProviders() =>
        new[] { ModelProviders.Resolve(Model.Trim()), ModelProviders.Resolve(EffectiveReasoningModel) }
            .Distinct()
            .ToList();

    /// <summary>
    /// The key in force for a provider: the configured value if set, otherwise the
    /// environment variable the SDK reads. Validating only the config value would let
    /// startup pass while the credential actually used is missing.
    /// </summary>
    public string? ResolveKey(ModelProvider provider)
    {
        var (configured, variable) = provider switch
        {
            ModelProvider.Anthropic => (AnthropicApiKey, "ANTHROPIC_API_KEY"),
            ModelProvider.OpenAi => (OpenAiApiKey, "OPENAI_API_KEY"),
            _ => (string.Empty, string.Empty),
        };

        var key = string.IsNullOrWhiteSpace(configured)
            ? Environment.GetEnvironmentVariable(variable)
            : configured;

        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }
}
