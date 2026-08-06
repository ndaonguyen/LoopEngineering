using System.Text;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Core.Model;

namespace Loop.Engine.Agents.Investigation;

/// <summary>
/// Builds the investigation prompt. Pure and static so the instructions can be asserted
/// in a test — the no-code-generation constraint is the whole premise of this phase, and
/// a constraint nothing checks is a constraint that quietly disappears.
/// </summary>
public static class InvestigationPrompt
{
    public const string System = """
        You are a debugging specialist investigating a reported defect in a .NET codebase.

        Your job is to explain the mechanism of the failure and say where it can be
        observed. It is NOT to fix anything. Do not write, propose, or sketch code
        changes — not a patch, not a diff, not a "you could just" snippet. A separate
        agent handles fixes, and it depends on your analysis being untainted by one.

        Ground every claim in the excerpts you are given. If they are insufficient to
        identify the cause, say so plainly and report low confidence — a candid "I could
        not determine this from the code provided" is far more useful than a confident
        guess, because a wrong file list sends the next stage down the wrong path.

        In `affected_files`, list only paths that appear in the excerpts below, exactly as
        given. Do not invent paths.

        # Output format

        Reply with a single JSON object and nothing else. No prose before or after it, no
        markdown code fence, no <analysis> wrapper. The output is parsed by a program.

        {
          "symptoms": "what the reporter observes, one short paragraph",
          "possible_root_causes": ["most likely mechanism first"],
          "affected_files": ["exact path from the excerpts below"],
          "confidence": 0.0,
          "recommended_investigation": "how to confirm the cause"
        }

        `confidence` is a number between 0 and 1. Every array may be empty; no field may be
        omitted.
        """;

    public static string BuildUserMessage(Issue issue, IReadOnlyList<RetrievedFile> files)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine($"# Issue #{issue.Number}: {issue.Title}");
        prompt.AppendLine();
        prompt.AppendLine(string.IsNullOrWhiteSpace(issue.Body) ? "_No description provided._" : issue.Body);
        prompt.AppendLine();

        prompt.AppendLine("# Candidate source files");
        prompt.AppendLine();

        if (files.Count == 0)
        {
            // Say so explicitly rather than sending an empty section — otherwise the model
            // fills the silence by inventing plausible-looking paths.
            prompt.AppendLine(
                "_No candidate files were retrieved. Report low confidence and state that " +
                "the issue does not contain enough detail to locate the failure path._");
        }
        else
        {
            foreach (var file in files)
            {
                prompt.AppendLine($"## {file.RelativePath}");
                prompt.AppendLine($"_Matched: {string.Join(", ", file.MatchedSymbols)}_");
                prompt.AppendLine();
                prompt.AppendLine("```csharp");
                prompt.AppendLine(file.Excerpt);
                prompt.AppendLine("```");
                prompt.AppendLine();
            }
        }

        prompt.Append("Investigate this defect. Analysis only — no code changes.");
        return prompt.ToString();
    }
}
