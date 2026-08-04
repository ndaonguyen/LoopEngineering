namespace Loop.Engine.Core.Model;

/// <summary>
/// A GitHub issue, reduced to what the pipeline actually reasons about.
/// Deliberately not Octokit's <c>Issue</c> — the domain should not depend on the transport.
/// </summary>
public sealed record Issue(
    int Number,
    string Title,
    string Body,
    string Url,
    IReadOnlyList<string> Labels,
    string? Assignee,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>True when a human has taken it — the pipeline leaves those alone.</summary>
    public bool IsAssigned => !string.IsNullOrWhiteSpace(Assignee);

    public bool HasLabel(string label) =>
        Labels.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase));
}
