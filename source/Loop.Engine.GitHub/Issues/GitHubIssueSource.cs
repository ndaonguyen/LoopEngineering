using Loop.Engine.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using DomainIssue = Loop.Engine.Core.Model.Issue;

namespace Loop.Engine.GitHub.Issues;

/// <summary>
/// Reads open issues from GitHub via Octokit and maps them onto the domain model.
/// </summary>
public sealed class GitHubIssueSource : IIssueSource
{
    private readonly IGitHubClient _client;
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubIssueSource> _logger;

    public GitHubIssueSource(
        IGitHubClient client,
        IOptions<GitHubOptions> options,
        ILogger<GitHubIssueSource> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DomainIssue>> GetOpenIssuesAsync(
        CancellationToken cancellationToken = default)
    {
        var request = new RepositoryIssueRequest { State = ItemStateFilter.Open };

        var issues = await _client.Issue.GetAllForRepository(
            _options.Owner, _options.Repository, request);

        // GitHub models pull requests as issues; the pipeline only wants real issues.
        var mapped = issues
            .Where(i => i.PullRequest is null)
            .Select(Map)
            .ToList();

        _logger.LogInformation(
            "Fetched {Count} open issue(s) from {Owner}/{Repository}.",
            mapped.Count, _options.Owner, _options.Repository);

        return mapped;
    }

    private static DomainIssue Map(Octokit.Issue issue) => new(
        Number: issue.Number,
        Title: issue.Title ?? string.Empty,
        Body: issue.Body ?? string.Empty,
        Url: issue.HtmlUrl ?? string.Empty,
        Labels: issue.Labels?.Select(l => l.Name).ToList() ?? [],
        Assignee: issue.Assignee?.Login,
        CreatedAt: issue.CreatedAt,
        UpdatedAt: issue.UpdatedAt ?? issue.CreatedAt);
}
