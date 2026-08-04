namespace Loop.Engine.Core.Model;

/// <summary>
/// One unit of work travelling through the pipeline: an issue plus everything
/// later stages have learned about it. Stages add to it; none of them mutate the issue.
/// </summary>
public sealed record IssueTask(Issue Issue)
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset PickedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Set by the Investigation agent (Phase 2).</summary>
    public AnalysisResult? Analysis { get; init; }

    /// <summary>Set by the Planner agent (Phase 3).</summary>
    public FixPlan? Plan { get; init; }

    /// <summary>How many build/test repair cycles have been spent (Phase 4).</summary>
    public int Attempts { get; init; }
}
