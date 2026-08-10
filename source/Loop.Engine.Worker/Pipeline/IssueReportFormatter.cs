using System.Text;
using Loop.Engine.Core.Model;

namespace Loop.Engine.Worker.Pipeline;

/// <summary>
/// Renders the Phase 1 deliverable. Kept pure and separate from the polling service so
/// the output can be asserted in a test without a GitHub connection or a running host.
/// </summary>
public static class IssueReportFormatter
{
    /// <summary>
    /// <paramref name="requiredLabel"/> decides the last line. Reporting every open issue as
    /// "Ready for investigation" and then refusing to touch most of them describes a
    /// pipeline that does not exist — eligibility is <see cref="IssueSelector.IsEligible"/>,
    /// asked rather than restated.
    /// </summary>
    public static string Format(Issue issue, string requiredLabel = "")
    {
        var report = new StringBuilder();
        report.AppendLine($"Found Issue #{issue.Number}");
        report.AppendLine($"Title      {issue.Title}");
        report.AppendLine($"Assigned   {(issue.IsAssigned ? issue.Assignee : "No")}");
        report.Append(IssueSelector.IsEligible(issue, requiredLabel)
            ? "Ready for investigation"
            : $"Skipped — not labelled '{requiredLabel}'");
        return report.ToString();
    }

    public static string FormatAll(IReadOnlyList<Issue> issues, string requiredLabel = "") =>
        issues.Count == 0
            ? "No open issues."
            : string.Join(
                Environment.NewLine + Environment.NewLine,
                issues.Select(i => Format(i, requiredLabel)));
}
