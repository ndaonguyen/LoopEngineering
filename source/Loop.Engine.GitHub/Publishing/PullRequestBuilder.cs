using System.Text;
using System.Text.RegularExpressions;
using Loop.Engine.Core.Model;

namespace Loop.Engine.GitHub.Publishing;

/// <summary>
/// Assembles the pull request from what the pipeline produced. Pure, so the parts that
/// matter — the Conventional Commits prefix, <c>Closes #n</c>, the review findings — can be
/// asserted without a network.
/// </summary>
public static class PullRequestBuilder
{
    public static PullRequestContext Build(
        Issue issue,
        AnalysisResult analysis,
        VerificationResult verification,
        ReviewReport review,
        string branchPrefix,
        ReproductionResult? reproduction = null)
    {
        var branch = $"{branchPrefix}{issue.Number}-{Slug(issue.Title)}";

        var changed = verification.Edits.Select(e => e.RelativePath).ToList();

        // The reproduction test is a real file in the branch, written before the fix. It
        // does not come through verification.Edits, so listing it here is the only way the
        // pull request describes everything it actually changes.
        if (reproduction?.Test is { } test && !changed.Contains(test.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            changed.Insert(0, test.RelativePath);
        }

        return new PullRequestContext(
            IssueNumber: issue.Number,
            BranchName: branch,
            // pr-lint.yml enforces Conventional Commits. Bugs are `fix:`.
            Title: $"fix: {FirstLine(issue.Title)}",
            Summary: BuildSummary(analysis, verification),
            RootCause: BuildRootCause(analysis),
            ChangedFiles: changed,
            TestingNotes: BuildTestingNotes(verification, reproduction),
            Risk: BuildRisk(review),
            ReviewerNotes: BuildReviewerNotes(verification, review),
            Warning: BuildWarning(review),
            NeedsHuman: review.NeedsHuman);
    }

    /// <summary>
    /// The banner above the fold. Empty for a clean review — a warning that appears on
    /// every pull request is one nobody reads.
    /// </summary>
    private static string BuildWarning(ReviewReport review)
    {
        if (!review.NeedsHuman)
        {
            return string.Empty;
        }

        var high = review.Findings
            .Where(f => f.ParsedSeverity == ReviewSeverity.High)
            .ToList();

        var lines = new StringBuilder();
        lines.AppendLine(
            $"> [!WARNING]\n> **The automated review raised {high.Count} high-severity " +
            "finding(s). Read these before the diff.**");

        foreach (var finding in high)
        {
            lines.AppendLine($"> - **{finding.Category}** — {finding.Detail}");
        }

        return lines.ToString().TrimEnd();
    }

    /// <summary>
    /// Matches the <c>bug-loop</c> skill's rule so branches from both loops look alike:
    /// lowercase, non-alphanumerics collapsed to '-', trimmed, 50 characters.
    /// </summary>
    public static string Slug(string title)
    {
        var slug = Regex.Replace(title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length <= 50 ? slug : slug[..50].TrimEnd('-');
    }

    private static string FirstLine(string text) =>
        text.Split('\n')[0].Trim();

    private static string BuildSummary(AnalysisResult analysis, VerificationResult verification)
    {
        var attempts = verification.Attempts == 1
            ? "Built and tested clean on the first attempt."
            : $"Took {verification.Attempts} attempts to build and test clean.";

        return $"{analysis.Symptoms.Trim()}\n\n{attempts}";
    }

    private static string BuildRootCause(AnalysisResult analysis) =>
        analysis.PossibleRootCauses.Count == 0
            ? "_Not determined._"
            : string.Join("\n", analysis.PossibleRootCauses.Select(c => $"- {c}"));

    /// <summary>
    /// The disclaimer is removed only against evidence — a test that was observed failing
    /// on the unfixed tree and passing on the fixed one. Anything less and the note stays,
    /// because "the suite is green" has never meant "the bug is gone".
    /// </summary>
    private static string BuildTestingNotes(
        VerificationResult verification, ReproductionResult? reproduction)
    {
        const string clean = "`dotnet build` and `dotnet test` pass in a clean worktree.";

        if (reproduction is { IsRed: true, Test: { } test })
        {
            return $"""
                {clean}

                **A test reproduces the original defect.** `{test.FullyQualifiedName}` was written
                before the fix and observed **failing** against the unfixed code; it passes with
                the fix applied. That red-to-green transition is the evidence this change works —
                revert the fix and the test goes red again.

                The test is `{test.RelativePath}`, included in this branch.
                """;
        }

        return $"""
            {clean}

            **No test reproduces the original defect.** The suite proves this change breaks
            nothing; it does not prove the reported bug is fixed. Confirm that yourself before
            merging.
            """;
    }

    private static string BuildRisk(ReviewReport review)
    {
        var high = review.Findings
            .Where(f => f.ParsedSeverity == ReviewSeverity.High)
            .ToList();

        return high.Count == 0
            ? "No high-severity findings from the automated review."
            : $"**{high.Count} high-severity finding(s) from the automated review — see below.**";
    }

    private static string BuildReviewerNotes(VerificationResult verification, ReviewReport review)
    {
        var notes = new StringBuilder();

        notes.AppendLine(
            "Written by an automated pipeline: investigate → plan → code → build → test → review. " +
            "It does not merge.");

        if (verification.Hypotheses.Count > 0)
        {
            notes.AppendLine();
            notes.AppendLine("**Diagnoses tried before this one worked:**");
            foreach (var (hypothesis, index) in verification.Hypotheses.Select((h, i) => (h, i + 1)))
            {
                notes.AppendLine($"{index}. {hypothesis}");
            }
        }

        notes.AppendLine();
        notes.AppendLine("**Automated review:**");

        if (review.Findings.Count == 0)
        {
            notes.AppendLine();
            notes.AppendLine("_No findings._");
        }
        else
        {
            foreach (var finding in review.Findings)
            {
                notes.AppendLine();
                notes.AppendLine($"- **{finding.Category} / {finding.Severity}** — {finding.Detail}");
            }
        }

        return notes.ToString().TrimEnd();
    }
}
