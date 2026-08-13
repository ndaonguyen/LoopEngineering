using Loop.Engine.Core.Model;

namespace Loop.Engine.Worker.Pipeline;

/// <summary>
/// The outcome of choosing what to work on: an issue, or the reason there isn't one.
///
/// A rejection carries its own sentence rather than a code the caller has to translate,
/// because every one of these is written to be read by a human at 3am wondering why the
/// tick did nothing.
/// </summary>
public sealed record IssueSelection(Issue? Issue, string? Rejection)
{
    public static IssueSelection Chosen(Issue issue) => new(issue, null);

    public static IssueSelection Rejected(string reason) => new(null, reason);
}

/// <summary>
/// Decides which issue a tick works on. Pure, so the policy can be asserted against plain
/// lists — the alternative is discovering the rule is wrong by watching the engine open a
/// pull request against a feature request.
/// </summary>
public static class IssueSelector
{
    /// <summary>
    /// Whether this issue is work for the engine at all.
    ///
    /// Public because the console report renders the same judgement, and this project has
    /// twice paid for two pieces of code holding separate opinions about the same thing.
    /// One definition, two callers.
    /// </summary>
    public static bool IsEligible(Issue issue, string requiredLabel) =>
        string.IsNullOrWhiteSpace(requiredLabel) || HasLabel(issue, requiredLabel);

    /// <param name="inFlight">
    /// Issue numbers that already have an open fix pull request. Skipped when picking
    /// automatically, because the engine cannot resume a branch — it would rebuild the same
    /// fix and fail at publish. An explicit <paramref name="requested"/> still wins: naming a
    /// number is a deliberate act, and re-running a stage against a known bug is how retrieval
    /// gets checked.
    /// </param>
    public static IssueSelection Select(
        IReadOnlyList<Issue> issues,
        int? requested,
        string requiredLabel,
        IReadOnlySet<int>? inFlight = null)
    {
        var filtering = !string.IsNullOrWhiteSpace(requiredLabel);

        if (requested is not { } wanted)
        {
            // Oldest first: a bug that has been open longest has waited longest.
            var eligible = issues
                .Where(i => IsEligible(i, requiredLabel))
                .OrderBy(i => i.Number)
                .ToList();

            var next = eligible.FirstOrDefault(i => inFlight?.Contains(i.Number) != true);

            if (next is not null)
            {
                return IssueSelection.Chosen(next);
            }

            // Everything eligible is taken. Said separately from "nothing is labelled" because
            // the two need opposite responses: one wants a label, this one wants a review.
            if (eligible.Count > 0)
            {
                return IssueSelection.Rejected(
                    $"All {eligible.Count} eligible issue(s) already have an open fix pull " +
                    "request. The engine stops at a green pull request and a human merges, so " +
                    "nothing new is picked up until one is merged or closed.");
            }

            return IssueSelection.Rejected(
                filtering
                    ? $"No open issue is labelled '{requiredLabel}' ({issues.Count} open). Nothing to do."
                    : "No open issues. Nothing to do.");
        }

        var match = issues.FirstOrDefault(i => i.Number == wanted);

        if (match is null)
        {
            return IssueSelection.Rejected(
                $"Pipeline:IssueNumber={wanted} was requested but is not among the {issues.Count} " +
                "open issue(s). Nothing investigated this tick.");
        }

        // An explicit request does not override the label. Being asked for a specific issue
        // says which one, not that it is a bug — and the whole pipeline is built on the
        // assumption that it is looking at one.
        if (!IsEligible(match, requiredLabel))
        {
            return IssueSelection.Rejected(
                $"Pipeline:IssueNumber={wanted} (\"{match.Title}\") is not labelled " +
                $"'{requiredLabel}'. This engine fixes bugs; pointed at anything else it " +
                "produces a confident, plausible, wrong fix. Label the issue or change " +
                "Pipeline:RequiredLabel.");
        }

        return IssueSelection.Chosen(match);
    }

    private static bool HasLabel(Issue issue, string label) =>
        issue.Labels.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase));
}
