using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace Loop.Engine.GitHub.Publishing;

/// <summary>
/// Opens the pull request via Octokit, then stops.
///
/// There is no merge call here and there should never be one. A green pipeline makes
/// merging look safe, repeatedly, right up until the once it is not — the human step is
/// the product, not a limitation to be engineered away later.
/// </summary>
public sealed class PullRequestPublisher : IPullRequestPublisher
{
    private readonly IGitHubClient _client;
    private readonly GitHubOptions _gitHub;
    private readonly PublishingOptions _publishing;
    private readonly ILogger<PullRequestPublisher> _logger;

    public PullRequestPublisher(
        IGitHubClient client,
        IOptions<GitHubOptions> gitHub,
        IOptions<PublishingOptions> publishing,
        ILogger<PullRequestPublisher> logger)
    {
        _client = client;
        _gitHub = gitHub.Value;
        _publishing = publishing.Value;
        _logger = logger;
    }

    public async Task<PublishedPullRequest> OpenAsync(
        PullRequestContext context, CancellationToken cancellationToken = default)
    {
        var request = new NewPullRequest(context.Title, context.BranchName, _publishing.BaseBranch)
        {
            Body = context.RenderBody(),
        };

        var pr = await _client.PullRequest.Create(_gitHub.Owner, _gitHub.Repository, request);

        _logger.LogInformation(
            "Opened PR #{Number} for issue #{Issue}: {Url}", pr.Number, context.IssueNumber, pr.HtmlUrl);

        return new PublishedPullRequest(pr.Number, pr.HtmlUrl);
    }
}
