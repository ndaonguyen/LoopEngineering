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
    /// Only issues carrying this label are eligible. The engine fixes bugs; a feature
    /// request handed to a bug-fixing pipeline produces a confident, plausible, wrong PR.
    ///
    /// Matches the <c>bug-loop</c> skill, which is driven by the same label.
    ///
    /// Set to empty to disable the check — which means accepting that the oldest open
    /// issue, whatever it is, gets treated as a bug.
    /// </summary>
    public string RequiredLabel { get; set; } = "bug";

    /// <summary>
    /// Run the Planner and Coder after investigating, and write a <c>.diff</c>.
    ///
    /// Off by default: Phase 2 is the working baseline, and adding a phase should not
    /// change what happens unless it is asked for.
    /// </summary>
    public bool GenerateFix { get; set; }

    /// <summary>
    /// Build and test the generated fix, repairing it on failure, then review the result.
    /// Requires <see cref="GenerateFix"/>. Off by default, on the same principle: each
    /// phase stays dormant until asked for, and the previous one remains the baseline.
    /// </summary>
    public bool VerifyFix { get; set; }

    /// <summary>
    /// Push the verified fix to a branch and open a pull request. Requires
    /// <see cref="VerifyFix"/> — publishing an unbuilt fix would be worse than publishing
    /// nothing, because a PR carries the appearance of review.
    ///
    /// Off by default, like every phase before it. It never merges.
    /// </summary>
    public bool PublishPr { get; set; }
}
