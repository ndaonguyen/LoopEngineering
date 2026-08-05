using System.ComponentModel.DataAnnotations;

namespace Loop.Engine.Agents.Investigation;

/// <summary>Configuration for the Investigation agent. Bound from the <c>Anthropic</c> section.</summary>
public sealed class InvestigationOptions
{
    public const string SectionName = "Anthropic";

    [Required(AllowEmptyStrings = false)]
    public string Model { get; set; } = "claude-opus-5";

    /// <summary>
    /// API key. Leave empty to let the SDK read <c>ANTHROPIC_API_KEY</c> from the
    /// environment — which is the usual path. Never commit a value here.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    // NOT YET WIRED: reasoning effort.
    //
    // `effort` is Anthropic-specific and isn't in the Microsoft.Extensions.AI abstraction.
    // It is reachable via ChatOptions.RawRepresentationFactory returning a
    // MessageCreateParams with OutputConfig.Effort — that shape compiles — but
    // MessageCreateParams has required Model/Messages/MaxTokens members, so it can only be
    // constructed with placeholders and relies on the provider overwriting them. That
    // merge behaviour cannot be verified without a live API key, and a placeholder
    // MaxTokens that ISN'T overwritten would silently truncate every investigation.
    //
    // Deliberately omitted rather than shipped unverified. The run currently uses the
    // model's default effort. Wire and verify this against a real key before tuning.

    /// <summary>Where <c>investigation.md</c> is written.</summary>
    [Required(AllowEmptyStrings = false)]
    public string OutputDirectory { get; set; } = "output/investigations";

    /// <summary>True when the key must come from the environment rather than config.</summary>
    public bool UsesEnvironmentApiKey => string.IsNullOrWhiteSpace(ApiKey);
}
