using Loop.Engine.Core.Model;

namespace Loop.Engine.Core.Abstractions;

/// <summary>
/// Reads a diff and reports on it.
///
/// Takes a diff and returns findings — no workspace, no file access, no coder. A critic
/// that can rewrite the thing it is criticising stops being a critic, so the inability is
/// structural rather than an instruction in a prompt.
/// </summary>
public interface IReviewer
{
    Task<ReviewReport> ReviewAsync(Issue issue, string unifiedDiff, CancellationToken cancellationToken = default);
}
