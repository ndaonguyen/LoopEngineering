namespace Loop.Engine.Core.Model;

/// <summary>A pull request the pipeline opened. It stops here; a human merges.</summary>
public sealed record PublishedPullRequest(int Number, string Url);
