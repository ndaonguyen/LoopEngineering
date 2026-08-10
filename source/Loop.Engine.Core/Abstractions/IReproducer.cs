using Loop.Engine.Core.Model;

namespace Loop.Engine.Core.Abstractions;

/// <summary>
/// Writes a single test that fails because the bug is present.
///
/// Like <see cref="ICoder"/>, this carries no repository path and no search capability —
/// it writes one file from what the investigation already found. It also has no way to run
/// anything: whether the test actually goes red is decided by the compiler and the test
/// runner, never by the model that wrote it.
/// </summary>
public interface IReproducer
{
    Task<ReproductionTest?> WriteFailingTestAsync(
        Issue issue,
        AnalysisResult analysis,
        FixPlan plan,
        CancellationToken cancellationToken = default);
}
