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
    string ReviewerNotes,
    string Warning = "",
    bool NeedsHuman = false)
{
    /// <summary>
    /// `Closes #n` is what closes the issue on merge — the pipeline never closes one by hand.
    ///
    /// <see cref="Warning"/> renders above everything else. The findings also appear in
    /// Reviewer Notes at the bottom, which is where they used to live alone — by which
    /// point a reviewer has already read the diff and formed a view. A warning that arrives
    /// after the decision is decoration.
    /// </summary>
    public string RenderBody() =>
        $"""
         Closes #{IssueNumber}
         {(string.IsNullOrWhiteSpace(Warning) ? string.Empty : $"\n{Warning}\n")}
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
