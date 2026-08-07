using System.Text.Json.Serialization;
using Loop.Engine.Agents.Coding;

namespace Loop.Engine.Agents.Verification;

/// <summary>The shape a repair attempt must return.</summary>
public sealed class RepairDto
{
    /// <summary>
    /// Required by the prompt and recorded so the next attempt can be told not to repeat
    /// it. This is the mechanism that stops five attempts sharing one wrong diagnosis.
    /// </summary>
    [JsonPropertyName("hypothesis")]
    public string Hypothesis { get; set; } = string.Empty;

    [JsonPropertyName("edits")]
    public CodeEditDto[] Edits { get; set; } = [];
}
