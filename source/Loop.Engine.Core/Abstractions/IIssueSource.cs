using Loop.Engine.Core.Model;

namespace Loop.Engine.Core.Abstractions;

/// <summary>
/// Where issues come from. The port that keeps the pipeline free of Octokit —
/// tests supply a fake, production supplies the GitHub adapter.
/// </summary>
public interface IIssueSource
{
    Task<IReadOnlyList<Issue>> GetOpenIssuesAsync(CancellationToken cancellationToken = default);
}
