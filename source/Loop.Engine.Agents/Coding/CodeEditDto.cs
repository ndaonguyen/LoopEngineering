using System.Text.Json.Serialization;

namespace Loop.Engine.Agents.Coding;

/// <summary>The shape the Coder's prompt asks for.</summary>
public sealed class CodeEditSetDto
{
    [JsonPropertyName("edits")]
    public CodeEditDto[] Edits { get; set; } = [];
}

public sealed class CodeEditDto
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>The complete new file, not a patch.</summary>
    [JsonPropertyName("contents")]
    public string Contents { get; set; } = string.Empty;
}
