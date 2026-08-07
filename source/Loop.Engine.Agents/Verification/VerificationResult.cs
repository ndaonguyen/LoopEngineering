using Loop.Engine.Core.Model;

namespace Loop.Engine.Agents.Verification;

/// <summary>
/// The outcome of the retry loop.
///
/// A failed verification is still a result, not an exception: a precise stuck report —
/// what was tried, what each diagnosis was, what the last failure said — is more useful
/// than a stack trace, and far more useful than a sixth attempt.
/// </summary>
public sealed record VerificationResult(
    bool Succeeded,
    int Attempts,
    IReadOnlyList<CodeEdit> Edits,
    IReadOnlyList<string> Hypotheses,
    BuildResult? LastFailure)
{
    public string StuckReport()
    {
        if (Succeeded)
        {
            return $"Verified after {Attempts} attempt(s).";
        }

        var report = new System.Text.StringBuilder();
        report.AppendLine($"Gave up after {Attempts} attempt(s).");

        if (Hypotheses.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("Diagnoses tried:");
            foreach (var (hypothesis, index) in Hypotheses.Select((h, i) => (h, i + 1)))
            {
                report.AppendLine($"  {index}. {hypothesis}");
            }
        }

        if (LastFailure is { Errors.Count: > 0 })
        {
            report.AppendLine();
            report.AppendLine("Last failure:");
            foreach (var error in LastFailure.Errors.Take(10))
            {
                report.AppendLine($"  - {error}");
            }
        }

        return report.ToString();
    }
}
