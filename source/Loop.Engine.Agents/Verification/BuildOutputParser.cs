using System.Text.RegularExpressions;

namespace Loop.Engine.Agents.Verification;

/// <summary>
/// Pulls actionable failures out of MSBuild and xunit output.
///
/// Pure, so it can be asserted against recorded output rather than a live compiler.
/// Warnings are dropped on purpose — see <see cref="Core.Model.BuildResult"/>.
/// </summary>
public static class BuildOutputParser
{
    // C:\path\File.cs(46,9): error CS1929: message [C:\path\Project.csproj]
    private static readonly Regex CompilerError = new(
        @"^\s*(?<location>.+?):\s*error\s+(?<code>[A-Z]+\d+):\s*(?<message>.+?)(?:\s*\[[^\]]*\])?\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // [xUnit.net 00:00:00.53]     Namespace.Class.Method [FAIL]
    private static readonly Regex TestFailure = new(
        @"^\s*\[xUnit\.net[^\]]*\]\s+(?<test>\S+)\s+\[FAIL\]",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Failed!  - Failed:     1, Passed:    54, ...
    private static readonly Regex FailureSummary = new(
        @"^\s*Failed!\s*-\s*Failed:\s*(?<count>\d+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public static IReadOnlyList<string> ParseErrors(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var errors = new List<string>();

        foreach (Match m in CompilerError.Matches(output))
        {
            var error = $"{m.Groups["code"].Value}: {m.Groups["message"].Value.Trim()} " +
                        $"({m.Groups["location"].Value.Trim()})";

            // MSBuild repeats each diagnostic per target; the model does not need it twice.
            if (!errors.Contains(error, StringComparer.Ordinal))
            {
                errors.Add(error);
            }
        }

        foreach (Match m in TestFailure.Matches(output))
        {
            var failure = $"Test failed: {m.Groups["test"].Value}";
            if (!errors.Contains(failure, StringComparer.Ordinal))
            {
                errors.Add(failure);
            }
        }

        return errors;
    }

    /// <summary>
    /// True when the output shows no compiler errors and no failing tests. Warnings do not
    /// affect this — a repository with a pre-existing advisory would otherwise never pass.
    /// </summary>
    public static bool Succeeded(string output, int exitCode) =>
        exitCode == 0 && ParseErrors(output).Count == 0 && !FailureSummary.IsMatch(output);
}
