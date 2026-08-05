namespace Loop.Engine.Agents.Retrieval;

/// <summary>One candidate file plus the excerpt handed to the model.</summary>
/// <param name="RelativePath">Path relative to the repository root — what the report cites.</param>
/// <param name="Excerpt">File contents, truncated to the configured line budget.</param>
/// <param name="MatchedSymbols">Which symbols put this file in the set. Useful when a report looks wrong.</param>
public sealed record RetrievedFile(
    string RelativePath,
    string Excerpt,
    IReadOnlyList<string> MatchedSymbols);
