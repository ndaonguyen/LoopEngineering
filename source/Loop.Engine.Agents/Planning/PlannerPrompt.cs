using System.Text;
using Loop.Engine.Core.Model;

namespace Loop.Engine.Agents.Planning;

/// <summary>
/// Builds the Planner's prompt. Pure and static so the instructions can be asserted —
/// the field names in particular, since Phase 2 proved that omitting them yields prose.
/// </summary>
public static class PlannerPrompt
{
    public const string System = """
        You are a senior engineer turning a completed bug investigation into an
        implementation plan. You do not write code — a separate agent does that. Your job
        is to decide what should be done, in what order, before anything is edited.

        Keep the plan minimal and mechanical. One step per coherent change. Do not add
        refactors, cleanups, or improvements the bug does not require: a bug fix does not
        need surrounding tidying, and every extra step is another chance to break something
        unrelated.

        Every file you list must come from the affected-files list you are given. You
        cannot see the rest of the repository and must not guess at paths in it.

        # Output format

        Reply with a single JSON object and nothing else. No prose before or after it, no
        markdown code fence, no <plan> wrapper. The output is parsed by a program.

        {
          "steps": [
            { "description": "what to change and why", "files": ["exact path from the list"] }
          ]
        }

        Steps are applied in array order. Every step needs at least one file. Return at
        least one step.
        """;

    public static string BuildUserMessage(Issue issue, AnalysisResult analysis)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine($"# Issue #{issue.Number}: {issue.Title}");
        prompt.AppendLine();
        prompt.AppendLine(string.IsNullOrWhiteSpace(issue.Body) ? "_No description provided._" : issue.Body);
        prompt.AppendLine();

        prompt.AppendLine("# Investigation");
        prompt.AppendLine();
        prompt.AppendLine($"**Symptoms:** {analysis.Symptoms}");
        prompt.AppendLine();

        if (analysis.PossibleRootCauses.Count > 0)
        {
            prompt.AppendLine("**Possible root causes:**");
            foreach (var cause in analysis.PossibleRootCauses)
            {
                prompt.AppendLine($"- {cause}");
            }
            prompt.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(analysis.RecommendedInvestigation))
        {
            prompt.AppendLine($"**Recommended investigation:** {analysis.RecommendedInvestigation}");
            prompt.AppendLine();
        }

        prompt.AppendLine("# Affected files — the only paths you may use");
        prompt.AppendLine();

        foreach (var file in analysis.AffectedFiles)
        {
            prompt.AppendLine($"- `{file}`");
        }

        prompt.AppendLine();
        prompt.Append("Produce the plan. Do not write code.");

        return prompt.ToString();
    }
}
