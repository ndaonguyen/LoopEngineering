using Loop.Engine.Core.Model;

namespace Loop.Engine.Core.Abstractions;

/// <summary>
/// Runs the build and the tests. A port so the suite stays offline — otherwise testing the
/// retry loop needs a real compiler and a real minute per attempt, and the
/// every-test-runs-offline rule that has held since Phase 2 quietly breaks here.
/// </summary>
public interface IBuildRunner
{
    Task<BuildResult> BuildAsync(string workingDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the suite, or one test when <paramref name="testFilter"/> is a fully-qualified
    /// name. The single-test form exists for the red gate: an unfiltered run cannot tell
    /// the reproduction failing from any other test failing, and "something is red" is not
    /// evidence that the bug is reproduced.
    /// </summary>
    Task<BuildResult> TestAsync(
        string workingDirectory,
        string? projectFilter = null,
        string? testFilter = null,
        CancellationToken cancellationToken = default);
}
