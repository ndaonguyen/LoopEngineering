using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;

namespace Loop.Engine.Tests.Fakes;

/// <summary>
/// A plain temp directory standing in for the git worktree. Keeps the retry-loop tests
/// off git entirely — the loop's job is deciding when to retry, not managing checkouts.
/// </summary>
public sealed class FakeFixWorkspace : IFixWorkspace, IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("loop-engine-fake-ws").FullName;

    /// <summary>Each set of edits applied, so a test can assert the loop re-applied them.</summary>
    public List<IReadOnlyList<CodeEdit>> Applied { get; } = [];

    public void Apply(IReadOnlyList<CodeEdit> edits) => Applied.Add(edits);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
    }
}
