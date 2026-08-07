using System.Text.Json.Serialization;

namespace Loop.Engine.Agents.Review;

public sealed class ReviewDto
{
    [JsonPropertyName("findings")]
    public ReviewFindingDto[] Findings { get; set; } = [];
}

public sealed class ReviewFindingDto
{
    /// <summary>security · performance · naming · architecture · clean-code</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>low · medium · high</summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;
}
