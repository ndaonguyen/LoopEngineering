using Loop.Engine.Agents.Verification;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loop.Engine.Agents.Reproduction;

/// <summary>
/// Decides whether a reproduction test actually reproduces anything.
///
/// The model asserts nothing here. The test is written into the worktree, compiled, and run
/// against the <b>unfixed</b> code, and the compiler and the runner supply the answer. This
/// is the same principle as the repair loop's allow-list: evidence, not a claim.
/// </summary>
public sealed class ReproductionGate
{
    private readonly IBuildRunner _runner;
    private readonly VerificationOptions _options;
    private readonly ILogger<ReproductionGate> _logger;

    public ReproductionGate(
        IBuildRunner runner,
        IOptions<VerificationOptions> options,
        ILogger<ReproductionGate> logger)
    {
        _runner = runner;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Applies the test to <paramref name="workspace"/> and runs it alone. Only
    /// <see cref="ReproductionOutcome.Red"/> is permission to proceed.
    /// </summary>
    public async Task<ReproductionResult> RunAsync(
        ReproductionTest? test, IFixWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (test is null)
        {
            return ReproductionResult.Rejected(
                ReproductionOutcome.NotProduced, null,
                "No reproduction test was produced, so there is nothing to prove the bug exists.");
        }

        workspace.Apply([new CodeEdit(test.RelativePath, test.Contents)]);

        var build = await _runner.BuildAsync(workspace.Path, cancellationToken);

        if (!build.Succeeded)
        {
            return ReproductionResult.Rejected(
                ReproductionOutcome.DoesNotCompile, test,
                "The reproduction test does not compile, so it demonstrates nothing about the " +
                $"bug: {string.Join("; ", build.Errors.Take(3))}");
        }

        var run = await _runner.TestAsync(
            workspace.Path, _options.TestProject, test.FullyQualifiedName, cancellationToken);

        if (MatchedNothing(run))
        {
            // dotnet test exits non-zero for "no test matches" exactly as it does for a
            // failing test. Without this branch a name that matches nothing is read as a
            // successful reproduction — the pipeline would then "prove" the fix by making
            // a test that never ran stop not-running.
            return ReproductionResult.Rejected(
                ReproductionOutcome.TestNotFound, test,
                $"No test matched '{test.FullyQualifiedName}'. The filter found nothing to run, " +
                "which is not the same as the bug being reproduced.");
        }

        if (run.Succeeded)
        {
            return ReproductionResult.Rejected(
                ReproductionOutcome.AlreadyPasses, test,
                $"'{test.FullyQualifiedName}' passes against the unfixed code, so it does not " +
                "exercise this bug. It would pass after the fix too, and prove nothing.");
        }

        _logger.LogInformation(
            "Reproduction confirmed: {Name} fails against the unfixed code.", test.FullyQualifiedName);

        return ReproductionResult.Red(
            test,
            $"'{test.FullyQualifiedName}' fails against the unfixed code. The fix must turn it green.");
    }

    /// <summary>
    /// Whether nothing actually ran.
    ///
    /// Decided by the reported test count first, and only then by the runner's wording. The
    /// wording alone was not enough: a generated test once landed outside the test project,
    /// so nothing compiled it, the filter matched nothing, <c>dotnet test</c> exited zero
    /// without printing any of the expected phrases, and the gate concluded the test
    /// "already passes". It had never executed. Counting what ran is a fact; matching on a
    /// message is a hope.
    /// </summary>
    private static bool MatchedNothing(BuildResult result)
    {
        if (BuildOutputParser.TryReadTestTotal(result.RawOutput) is { } total)
        {
            return total == 0;
        }

        // Falling back to the wording only when there is no count at all. Treating "no
        // summary" as "nothing ran" was tried and was worse: it turned a legitimate
        // single-test pass into a TestNotFound, which is the one rejection that stops the
        // gate ever reporting the dangerous case again.
        return result.RawOutput.Contains("No test matches", StringComparison.OrdinalIgnoreCase)
            || result.RawOutput.Contains("No test is available", StringComparison.OrdinalIgnoreCase);
    }
}
