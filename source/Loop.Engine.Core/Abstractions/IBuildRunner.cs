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

    Task<BuildResult> TestAsync(
        string workingDirectory, string? projectFilter = null, CancellationToken cancellationToken = default);
}
