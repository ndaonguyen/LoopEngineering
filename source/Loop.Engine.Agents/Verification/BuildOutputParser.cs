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

    // C:\path\to\File.cs(41,29): error CS7036: …
    private static readonly Regex ErrorFile = new(
        @"^\s*(?<file>(?:[A-Za-z]:\\|/)[^(\r\n]+?)\(\d+,\d+\):\s*error\s",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Files the compiler itself named, relative to <paramref name="workingDirectory"/>.
    ///
    /// This is the only sanctioned way the Coder's allow-list may grow. A file the build
    /// points at has earned its place by evidence — csc stating a fact, not the model
    /// speculating — so a signature change can finish repairing its own call sites without
    /// reopening the door to wandering the repository.
    ///
    /// Paths come from the error text rather than from the model, which also closes the
    /// obvious hole: a file cannot be nominated by inventing a plausible path for it.
    /// </summary>
    public static IReadOnlyList<string> ParseErrorFiles(string output, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(workingDirectory))
        {
            return [];
        }

        var root = Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var files = new List<string>();

        foreach (Match m in ErrorFile.Matches(output))
        {
            string full;

            try
            {
                full = Path.GetFullPath(m.Groups["file"].Value.Trim());
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                // Outside the tree being built — an SDK or package file, not ours to touch.
                continue;
            }

            var relative = Path.GetRelativePath(root, full).Replace('\\', '/');

            if (!files.Contains(relative, StringComparer.OrdinalIgnoreCase))
            {
                files.Add(relative);
            }
        }

        return files;
    }
}
