using System.Text.Json.Serialization;

namespace Loop.Engine.Agents.Investigation;

/// <summary>
/// The shape the model fills in via <c>GetResponseAsync&lt;InvestigationReport&gt;()</c>.
///
/// This is a schema rather than prose because <see cref="AffectedFiles"/> is the real
/// deliverable of Phase 2 — it becomes the allow-list the Coder is restricted to in
/// Phase 3, and parsing that out of free text would make the contract fragile.
/// </summary>
public sealed class InvestigationReport
{
    [JsonPropertyName("symptoms")]
    public string Symptoms { get; set; } = string.Empty;

    [JsonPropertyName("possible_root_causes")]
    public string[] PossibleRootCauses { get; set; } = [];

    [JsonPropertyName("affected_files")]
    public string[] AffectedFiles { get; set; } = [];

    /// <summary>
    /// The model's self-reported confidence, 0–1. Recorded for a human reader and
    /// <b>gates nothing</b> — it is uncalibrated, and branching on it would be a bug.
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("recommended_investigation")]
    public string RecommendedInvestigation { get; set; } = string.Empty;
}
