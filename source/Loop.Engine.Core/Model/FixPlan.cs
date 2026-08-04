namespace Loop.Engine.Core.Model;

/// <summary>The Planner agent's output (Phase 3): an ordered list of discrete steps.</summary>
public sealed record FixPlan(IReadOnlyList<FixPlanStep> Steps)
{
    public static FixPlan Empty { get; } = new([]);
}

/// <summary>
/// One step of the plan. <paramref name="Files"/> is the allow-list handed to the Coder —
/// it may only touch files the investigation actually identified.
/// </summary>
public sealed record FixPlanStep(
    int Order,
    string Description,
    IReadOnlyList<string> Files);
