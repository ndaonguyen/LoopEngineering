namespace Loop.Engine.Core.Abstractions;

/// <summary>
/// Puts a verified fix on a branch and pushes it.
///
/// Note the absent methods: no merge, no force-push, no delete. As with <see cref="ICoder"/>
/// having no repository path and <see cref="IReviewer"/> having no writable dependency, the
/// guarantee is what the interface cannot express. "Never merges" is the whole product;
/// leaving it to a prompt or a code comment would make it a preference.
/// </summary>
public interface IGitPublisher
{
    /// <summary>
    /// Creates <paramref name="branch"/> in an already-verified worktree, commits
    /// everything, and pushes. Throws rather than overwriting an existing branch.
    /// </summary>
    Task PublishBranchAsync(
        string worktreePath,
        string branch,
        string commitMessage,
        CancellationToken cancellationToken = default);
}
