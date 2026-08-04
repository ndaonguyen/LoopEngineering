namespace Loop.Engine.Core.Model;

/// <summary>
/// Everything needed to open the pull request (Phase 5). Assembled by the pipeline,
/// consumed by the GitHub agent.
/// </summary>
public sealed record PullRequestContext(
    int IssueNumber,
    string BranchName,
    string Title,
    string Summary,
    string RootCause,
    IReadOnlyList<string> ChangedFiles,
    string TestingNotes,
    string Risk,
    string ReviewerNotes)
{
    /// <summary>
    /// `Closes #n` is what closes the issue on merge — the pipeline never closes one by hand.
    /// </summary>
    public string RenderBody() =>
        $"""
         Closes #{IssueNumber}

         ## Summary
         {Summary}

         ## Root Cause
         {RootCause}

         ## Changes
         {string.Join(Environment.NewLine, ChangedFiles.Select(f => $"- `{f}`"))}

         ## Testing
         {TestingNotes}

         ## Risk
         {Risk}

         ## Reviewer Notes
         {ReviewerNotes}
         """;
}
