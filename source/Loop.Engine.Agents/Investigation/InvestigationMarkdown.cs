using System.Text;
using Loop.Engine.Core.Model;

namespace Loop.Engine.Agents.Investigation;

/// <summary>
/// Renders <c>investigation.md</c>. A pure function over the parsed result, so the
/// document is a view of the structured data rather than the source of it.
/// </summary>
public static class InvestigationMarkdown
{
    public static string Render(Issue issue, AnalysisResult analysis)
    {
        var md = new StringBuilder();

        md.AppendLine($"# Investigation — #{issue.Number}: {issue.Title}");
        md.AppendLine();
        md.AppendLine($"<{issue.Url}>");
        md.AppendLine();

        md.AppendLine("## Symptoms");
        md.AppendLine();
        md.AppendLine(Fallback(analysis.Symptoms, "_Not reported._"));
        md.AppendLine();

        md.AppendLine("## Possible root causes");
        md.AppendLine();
        AppendList(md, analysis.PossibleRootCauses, ordered: true, empty: "_None identified._");
        md.AppendLine();

        md.AppendLine("## Affected files");
        md.AppendLine();
        AppendList(md, analysis.AffectedFiles.Select(f => $"`{f}`").ToList(), ordered: false,
            empty: "_None identified — the issue may not contain enough detail to locate the failure path._");
        md.AppendLine();

        md.AppendLine("## Recommended investigation");
        md.AppendLine();
        md.AppendLine(Fallback(analysis.RecommendedInvestigation, "_None suggested._"));
        md.AppendLine();

        md.AppendLine("## Confidence");
        md.AppendLine();
        md.AppendLine($"{analysis.Confidence:P0} — self-reported by the model and uncalibrated.");
        md.AppendLine("Treat it as commentary, not a gate: nothing downstream branches on this number.");
        md.AppendLine();

        md.AppendLine("---");
        md.Append("Analysis only. No code was changed to produce this report.");

        return md.ToString();
    }

    private static void AppendList(StringBuilder md, IReadOnlyList<string> items, bool ordered, string empty)
    {
        if (items.Count == 0)
        {
            md.AppendLine(empty);
            return;
        }

        for (var i = 0; i < items.Count; i++)
        {
            md.AppendLine(ordered ? $"{i + 1}. {items[i]}" : $"- {items[i]}");
        }
    }

    private static string Fallback(string value, string empty) =>
        string.IsNullOrWhiteSpace(value) ? empty : value.Trim();
}
