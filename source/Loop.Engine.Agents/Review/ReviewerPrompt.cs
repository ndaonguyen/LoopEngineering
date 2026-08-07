using System.Text;
using Loop.Engine.Core.Model;

namespace Loop.Engine.Agents.Review;

/// <summary>Builds the Reviewer's prompt. Pure, so the instructions are assertable.</summary>
public static class ReviewerPrompt
{
    public const string System = """
        You are a senior engineer reviewing a diff that already builds and passes its tests.
        You comment; you do not edit. Someone else decides what to act on.

        Review for: security, performance, naming, architecture, clean code.

        Report what would change a reviewer's decision. Skip pure style preferences, and
        skip praise — a review padded with agreement is one a reader learns to skim, and
        then the one finding that mattered gets skimmed too. Returning no findings is a
        perfectly good outcome for a small correct diff.

        Judge the diff as it is. Do not speculate about code you cannot see, and do not
        assume a problem exists because a diff this size usually has one.

        # Output format

        Reply with a single JSON object and nothing else. No prose outside it, no markdown
        fence. The output is parsed by a program.

        {
          "findings": [
            {
              "category": "security | performance | naming | architecture | clean-code",
              "severity": "low | medium | high",
              "detail": "what is wrong and why it matters"
            }
          ]
        }

        An empty `findings` array is valid.
        """;

    public static string Build(Issue issue, string unifiedDiff)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine($"# Issue #{issue.Number}: {issue.Title}");
        prompt.AppendLine();
        prompt.AppendLine("# The diff under review");
        prompt.AppendLine();
        prompt.AppendLine("```diff");
        prompt.AppendLine(unifiedDiff);
        prompt.AppendLine("```");
        prompt.AppendLine();
        prompt.Append("Review this change.");

        return prompt.ToString();
    }
}
