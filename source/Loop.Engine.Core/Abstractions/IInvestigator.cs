using Loop.Engine.Core.Model;

namespace Loop.Engine.Core.Abstractions;

/// <summary>
/// Analyses a bug and reports what it found. Deliberately has no way to change anything:
/// the separation between investigating and coding is what makes the Planner tractable,
/// and a port that could write code would erode it by the first deadline.
/// </summary>
public interface IInvestigator
{
    Task<AnalysisResult> InvestigateAsync(Issue issue, CancellationToken cancellationToken = default);
}
