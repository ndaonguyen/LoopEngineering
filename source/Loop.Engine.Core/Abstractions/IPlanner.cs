using Loop.Engine.Core.Model;

namespace Loop.Engine.Core.Abstractions;

/// <summary>
/// Turns an investigation into an ordered set of discrete steps, before any file is
/// touched. Separate from the Coder on purpose: a model asked to plan and code at once
/// starts editing first, and the "plan" becomes a narration of edits already made.
/// </summary>
public interface IPlanner
{
    Task<FixPlan> PlanAsync(Issue issue, AnalysisResult analysis, CancellationToken cancellationToken = default);
}
