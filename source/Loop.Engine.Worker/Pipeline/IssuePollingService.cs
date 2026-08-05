using Loop.Engine.Agents.Investigation;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
using Loop.Engine.GitHub;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loop.Engine.Worker.Pipeline;

/// <summary>
/// The scheduler. Polls GitHub on an interval, prints what it finds, and investigates the
/// oldest open issue.
///
/// Phase 2 stops at analysis on purpose — no plan, no code, no PR. One issue per tick:
/// concurrency belongs to a later phase, and adding it here would only make the first
/// failures harder to read.
/// </summary>
public sealed class IssuePollingService : BackgroundService
{
    private readonly IIssueSource _issues;
    private readonly IInvestigator _investigator;
    private readonly GitHubOptions _gitHub;
    private readonly InvestigationOptions _investigation;
    private readonly ILogger<IssuePollingService> _logger;

    public IssuePollingService(
        IIssueSource issues,
        IInvestigator investigator,
        IOptions<GitHubOptions> gitHub,
        IOptions<InvestigationOptions> investigation,
        ILogger<IssuePollingService> logger)
    {
        _issues = issues;
        _investigator = investigator;
        _gitHub = gitHub.Value;
        _investigation = investigation.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Loop.Engine started. Polling {Owner}/{Repository} every {Interval}.",
            _gitHub.Owner, _gitHub.Repository, _gitHub.PollInterval);

        using var timer = new PeriodicTimer(_gitHub.PollInterval);

        do
        {
            await PollOnceAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var issues = await _issues.GetOpenIssuesAsync(cancellationToken);
            Console.WriteLine(IssueReportFormatter.FormatAll(issues));

            var target = issues.OrderBy(i => i.Number).FirstOrDefault();
            if (target is not null)
            {
                await InvestigateAsync(target, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A transient GitHub or model failure must not kill the scheduler — log it and
            // wait for the next tick. Anything non-transient keeps showing up in the logs.
            _logger.LogError(ex, "Poll failed; retrying on the next tick.");
        }
    }

    private async Task InvestigateAsync(Issue issue, CancellationToken cancellationToken)
    {
        var analysis = await _investigator.InvestigateAsync(issue, cancellationToken);
        var path = await WriteReportAsync(issue, analysis.Markdown, cancellationToken);

        Console.WriteLine();
        Console.WriteLine($"Investigated #{issue.Number} -> {path}");
        Console.WriteLine($"Affected files: {analysis.AffectedFiles.Count}");
    }

    private async Task<string> WriteReportAsync(
        Issue issue, string markdown, CancellationToken cancellationToken)
    {
        var directory = Path.GetFullPath(_investigation.OutputDirectory);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"investigation-{issue.Number}.md");
        await File.WriteAllTextAsync(path, markdown, cancellationToken);

        _logger.LogInformation("Wrote investigation for #{Number} to {Path}.", issue.Number, path);
        return path;
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
