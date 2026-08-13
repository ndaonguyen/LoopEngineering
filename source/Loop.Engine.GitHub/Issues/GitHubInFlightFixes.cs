using System.Globalization;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.GitHub.Publishing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace Loop.Engine.GitHub.Issues;

/// <summary>
/// Derives in-flight work from open pull requests, using the branch name as the join key —
/// exactly as the <c>bug-loop</c> skill's picker does, so both loops read the same signal and
/// cannot disagree about what is already being worked on.
///
/// No local state file. GitHub is the record, so the answer survives a restart, a different
/// machine, a scheduled run, and a human closing a pull request in the browser mid-flight.
/// </summary>
public sealed class GitHubInFlightFixes : IInFlightFixes
{
    private readonly IGitHubClient _client;
    private readonly GitHubOptions _gitHub;
    private readonly PublishingOptions _publishing;
    private readonly ILogger<GitHubInFlightFixes> _logger;

    public GitHubInFlightFixes(
        IGitHubClient client,
        IOptions<GitHubOptions> gitHub,
        IOptions<PublishingOptions> publishing,
        ILogger<GitHubInFlightFixes> logger)
    {
        _client = client;
        _gitHub = gitHub.Value;
        _publishing = publishing.Value;
        _logger = logger;
    }

    public async Task<IReadOnlySet<int>> GetIssuesWithOpenFixAsync(
        CancellationToken cancellationToken = default)
    {
        var request = new PullRequestRequest { State = ItemStateFilter.Open };

        var pullRequests = await _client.PullRequest.GetAllForRepository(
            _gitHub.Owner, _gitHub.Repository, request);

        var inFlight = pullRequests
            .Select(pr => IssueNumberFrom(pr.Head?.Ref, _publishing.BranchPrefix))
            .Where(n => n is not null)
            .Select(n => n!.Value)
            .ToHashSet();

        if (inFlight.Count > 0)
        {
            _logger.LogInformation(
                "In flight: issue(s) {Numbers} already have an open fix pull request.",
                string.Join(", ", inFlight.Order()));
        }

        return inFlight;
    }

    /// <summary>
    /// Pulls the issue number out of a <c>fix/&lt;n&gt;-&lt;slug&gt;</c> branch name.
    ///
    /// Public and pure so the join key can be asserted directly. It is the one piece of shared
    /// vocabulary between publishing a fix and deciding not to write it twice; getting it
    /// wrong in either direction is invisible until the engine duplicates work.
    /// </summary>
    public static int? IssueNumberFrom(string? branch, string branchPrefix)
    {
        if (string.IsNullOrWhiteSpace(branch) || string.IsNullOrWhiteSpace(branchPrefix))
        {
            return null;
        }

        if (!branch.StartsWith(branchPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = branch[branchPrefix.Length..];

        // The number runs up to the first '-'. A branch with no separator is still ours when
        // the remainder is entirely digits — `fix/22` is unambiguous even if the slug is gone.
        var end = rest.IndexOf('-');
        var digits = end < 0 ? rest : rest[..end];

        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }
}
