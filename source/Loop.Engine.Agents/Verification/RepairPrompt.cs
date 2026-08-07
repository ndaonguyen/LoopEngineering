using System.Text;
using Loop.Engine.Core.Model;

namespace Loop.Engine.Agents.Verification;

/// <summary>Builds the prompt for a repair attempt. Pure, so the discipline is assertable.</summary>
public static class RepairPrompt
{
    public const string System = """
        A fix you produced does not build, or breaks the tests. Repair it.

        Before proposing any code, state in one sentence what you now believe was wrong —
        specifically, and differently from what you believed last time. Attempts that only
        vary the code while keeping the same diagnosis produce five variations on one wrong
        idea and burn the budget without learning anything. If the previous hypotheses were
        all wrong, say so plainly and reason from the error text rather than from them.

        Change only what the failure requires. Do not take the opportunity to refactor
        something the compiler happened to draw your attention to.

        You may only return the file named as your target. You cannot see the rest of the
        repository.

        # Output format

        Reply with a single JSON object and nothing else. No prose outside it, no markdown
        fence. The output is parsed by a program.

        {
          "hypothesis": "what you now believe was wrong, in one sentence",
          "edits": [
            { "path": "the target file", "contents": "the entire new file" }
          ]
        }
        """;

    public static string Build(
        Issue issue,
        string targetFile,
        string currentContents,
        BuildResult failure,
        IReadOnlyList<string> previousHypotheses,
        int attempt,
        int maxAttempts)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine($"# Attempt {attempt} of {maxAttempts} — issue #{issue.Number}");
        prompt.AppendLine();

        prompt.AppendLine("# What failed");
        prompt.AppendLine();
        foreach (var error in failure.Errors)
        {
            prompt.AppendLine($"- {error}");
        }
        prompt.AppendLine();

        if (previousHypotheses.Count > 0)
        {
            prompt.AppendLine("# Diagnoses already tried — all of them were wrong");
            prompt.AppendLine();
            foreach (var (hypothesis, index) in previousHypotheses.Select((h, i) => (h, i + 1)))
            {
                prompt.AppendLine($"{index}. {hypothesis}");
            }
            prompt.AppendLine();
            prompt.AppendLine("Do not restate any of these. Your new diagnosis must differ.");
            prompt.AppendLine();
        }

        prompt.AppendLine($"# Current contents of `{targetFile}`");
        prompt.AppendLine();
        prompt.AppendLine("```csharp");
        prompt.AppendLine(currentContents);
        prompt.AppendLine("```");
        prompt.AppendLine();

        prompt.Append($"Diagnose the failure, then return the corrected `{targetFile}`.");
        return prompt.ToString();
    }
}
