using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;

namespace Loop.Engine.Tests.Fakes;

/// <summary>
/// Returns canned build results in sequence, so the retry loop is testable without a
/// compiler. A real build is ~15 seconds; five of those per test is not a suite anyone
/// runs, and the loop's logic is what needs covering, not MSBuild's.
/// </summary>
public sealed class FakeBuildRunner : IBuildRunner
{
    private readonly Queue<BuildResult> _results;
    private readonly BuildResult _fallback;

    /// <summary>How many times build or test was invoked. Asserts the attempt cap.</summary>
    public int Invocations { get; private set; }

    public FakeBuildRunner(IEnumerable<BuildResult> results, BuildResult? fallback = null)
    {
        _results = new Queue<BuildResult>(results);
        _fallback = fallback ?? BuildResult.Failure(["error CS9999: still broken"]);
    }

    /// <summary>Always fails — for testing the attempt cap.</summary>
    public static FakeBuildRunner AlwaysFailing(string error = "error CS1002: ; expected") =>
        new([], BuildResult.Failure([error]));

    /// <summary>Fails the given number of times, then succeeds.</summary>
    public static FakeBuildRunner FailsThenSucceeds(int failures) =>
        new(
            Enumerable.Range(1, failures).Select(i => BuildResult.Failure([$"error CS1002: attempt {i} broken"])),
            BuildResult.Success());

    public Task<BuildResult> BuildAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        Invocations++;
        return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : _fallback);
    }

    /// <summary>
    /// What a filtered run returns. The red gate needs a failing single test to mean
    /// "reproduced" and a passing one to mean "proves nothing", so this is settable.
    /// </summary>
    public BuildResult? FilteredResult { get; set; }

    /// <summary>The filter the gate asked for, so a test can assert it was exact.</summary>
    public string? LastTestFilter { get; private set; }

    /// <summary>Files handed to the formatter, so a test can assert the scope was narrow.</summary>
    public List<IReadOnlyList<string>> Formatted { get; } = [];

    public Task<BuildResult> FormatAsync(
        string workingDirectory,
        IReadOnlyList<string> relativePaths,
        CancellationToken cancellationToken = default)
    {
        Formatted.Add(relativePaths);

        // Deliberately not counted as an invocation: formatting is not an attempt, and
        // letting it inflate the count would break the attempt-cap assertions.
        return Task.FromResult(BuildResult.Success());
    }

    // Tests only run when the build passed, so returning success keeps a "build succeeded"
    // result meaning exactly that in these tests.
    public Task<BuildResult> TestAsync(
        string workingDirectory,
        string? projectFilter = null,
        string? testFilter = null,
        CancellationToken cancellationToken = default)
    {
        LastTestFilter = testFilter;

        return Task.FromResult(
            testFilter is not null && FilteredResult is not null
                ? FilteredResult
                : BuildResult.Success());
    }
}
