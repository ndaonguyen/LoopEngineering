using System.Text.Json.Serialization;

namespace Loop.Engine.Agents.Planning;

/// <summary>The shape the Planner's prompt asks for.</summary>
public sealed class PlanDto
{
    [JsonPropertyName("steps")]
    public PlanStepDto[] Steps { get; set; } = [];
}

public sealed class PlanStepDto
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Files this step touches. Must be a subset of the investigation's findings.</summary>
    [JsonPropertyName("files")]
    public string[] Files { get; set; } = [];
}
