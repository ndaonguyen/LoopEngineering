using System.Text;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Core.Model;

namespace Loop.Engine.Agents.Coding;

/// <summary>Builds the Coder's prompt. Pure and static so the instructions are assertable.</summary>
public static class CoderPrompt
{
    public const string System = """
        You are implementing a bug fix that has already been investigated and planned. Work
        the plan; do not redesign it.

        Return the complete new contents of every file you change — the whole file, not a
        patch and not a fragment. Producing a valid unified diff by hand is error-prone;
        the surrounding tooling computes the diff from your files.

        Change only what the fix requires. No refactors, no renames, no tidying of nearby
        code, no defensive checks for situations that cannot arise. A bug fix does not need
        surrounding cleanup, and every unrequested change is one a reviewer must now
        justify.

        Preserve the file's existing style exactly: indentation, brace placement, comment
        density, naming. Your edit should be indistinguishable from the surrounding code.

        You may only return files listed below. You cannot see the rest of the repository.

        # Output format

        Reply with a single JSON object and nothing else. No prose before or after it, no
        markdown code fence, no <edits> wrapper. The output is parsed by a program.

        {
          "edits": [
            { "path": "exact path from the list", "contents": "the entire new file" }
          ]
        }

        JSON-escape the file contents properly. Omit any file you did not change.
        """;

    public static string BuildUserMessage(
        Issue issue, AnalysisResult analysis, FixPlan plan, IReadOnlyList<RetrievedFile> files)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine($"# Issue #{issue.Number}: {issue.Title}");
        prompt.AppendLine();
        prompt.AppendLine(string.IsNullOrWhiteSpace(issue.Body) ? "_No description provided._" : issue.Body);
        prompt.AppendLine();

        prompt.AppendLine("# Root cause");
        prompt.AppendLine();
        prompt.AppendLine(analysis.Symptoms);
        foreach (var cause in analysis.PossibleRootCauses)
        {
            prompt.AppendLine($"- {cause}");
        }
        prompt.AppendLine();

        prompt.AppendLine("# Plan — implement these in order");
        prompt.AppendLine();
        foreach (var step in plan.Steps)
        {
            prompt.AppendLine($"{step.Order}. {step.Description}");
            prompt.AppendLine($"   Files: {string.Join(", ", step.Files.Select(f => $"`{f}`"))}");
        }
        prompt.AppendLine();

        prompt.AppendLine("# Current file contents");
        prompt.AppendLine();
        foreach (var file in files)
        {
            prompt.AppendLine($"## {file.RelativePath}");
            prompt.AppendLine();
            prompt.AppendLine("```csharp");
            prompt.AppendLine(file.Excerpt);
            prompt.AppendLine("```");
            prompt.AppendLine();
        }

        prompt.Append("Implement the plan and return the changed files.");
        return prompt.ToString();
    }
}
