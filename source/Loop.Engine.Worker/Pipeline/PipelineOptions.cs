namespace Loop.Engine.Worker.Pipeline;

/// <summary>How the tick chooses what to work on. Bound from the <c>Pipeline</c> section.</summary>
public sealed class PipelineOptions
{
    public const string SectionName = "Pipeline";

    /// <summary>
    /// Investigate this specific issue instead of the oldest open one. Exists so a run can
    /// be aimed at a known bug — the only way to check retrieval against an answer you
    /// already know, which is what "the files are right" actually means.
    ///
    /// <c>dotnet run --project source/Loop.Engine -- --Pipeline:IssueNumber=8</c>
    /// </summary>
    public int? IssueNumber { get; set; }

    /// <summary>Stop after the first tick. Useful for a one-shot run rather than a service.</summary>
    public bool RunOnce { get; set; }

    /// <summary>
    /// Run the Planner and Coder after investigating, and write a <c>.diff</c>.
    ///
    /// Off by default: Phase 2 is the working baseline, and adding a phase should not
    /// change what happens unless it is asked for.
    /// </summary>
    public bool GenerateFix { get; set; }
}
