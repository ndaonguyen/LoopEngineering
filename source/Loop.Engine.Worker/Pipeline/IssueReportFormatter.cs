using System.Text;
using Loop.Engine.Core.Model;

namespace Loop.Engine.Worker.Pipeline;

/// <summary>
/// Renders the Phase 1 deliverable. Kept pure and separate from the polling service so
/// the output can be asserted in a test without a GitHub connection or a running host.
/// </summary>
public static class IssueReportFormatter
{
    public static string Format(Issue issue)
    {
        var report = new StringBuilder();
        report.AppendLine($"Found Issue #{issue.Number}");
        report.AppendLine($"Title      {issue.Title}");
        report.AppendLine($"Assigned   {(issue.IsAssigned ? issue.Assignee : "No")}");
        report.Append("Ready for investigation");
        return report.ToString();
    }

    public static string FormatAll(IReadOnlyList<Issue> issues) =>
        issues.Count == 0
            ? "No open issues."
            : string.Join(Environment.NewLine + Environment.NewLine, issues.Select(Format));
}
