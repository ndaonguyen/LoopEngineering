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

        await LabelIfNeedsHumanAsync(context, pr.Number);

        return new PublishedPullRequest(pr.Number, pr.HtmlUrl);
    }

    /// <summary>
    /// Flags a pull request the review was unhappy with. GitHub models pull requests as
    /// issues, so the issue label API is the one that applies here.
    ///
    /// A failure to label must never lose the pull request: it is already open, already
    /// pushed, and already carries the same warning in its body. Swallowing this is the
    /// rare case where carrying on beats failing loudly.
    /// </summary>
    private async Task LabelIfNeedsHumanAsync(PullRequestContext context, int number)
    {
        if (!context.NeedsHuman || string.IsNullOrWhiteSpace(_publishing.NeedsHumanLabel))
        {
            return;
        }

        try
        {
            await _client.Issue.Labels.AddToIssue(
                _gitHub.Owner, _gitHub.Repository, number, [_publishing.NeedsHumanLabel]);

            _logger.LogInformation(
                "Labelled PR #{Number} '{Label}' — the review raised a high-severity finding.",
                number, _publishing.NeedsHumanLabel);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not label PR #{Number} '{Label}'. The warning is still in the body. " +
                "Does the label exist in {Owner}/{Repository}?",
                number, _publishing.NeedsHumanLabel, _gitHub.Owner, _gitHub.Repository);
        }
    }
}
