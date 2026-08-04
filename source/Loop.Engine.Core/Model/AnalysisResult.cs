namespace Loop.Engine.Core.Model;

/// <summary>
/// The Investigation agent's output (Phase 2). Analysis only — it never contains code.
/// </summary>
public sealed record AnalysisResult(
    string Symptoms,
    IReadOnlyList<string> PossibleRootCauses,
    IReadOnlyList<string> AffectedFiles,
    string RecommendedInvestigation)
{
    /// <summary>
    /// The model's self-reported confidence, 0–1. Treat it as a weak signal: it is not
    /// calibrated, so gate on evidence (a failing test) rather than on this number.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>The rendered <c>investigation.md</c>.</summary>
    public string Markdown { get; init; } = string.Empty;
}
