using Loop.Engine.Core.Model;

namespace Loop.Engine.Core.Abstractions;

/// <summary>Opens the pull request. It cannot merge, approve, or close one.</summary>
public interface IPullRequestPublisher
{
    Task<PublishedPullRequest> OpenAsync(
        PullRequestContext context, CancellationToken cancellationToken = default);
}
