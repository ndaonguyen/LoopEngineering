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
    private readonly IHostApplicationLifetime _lifetime;
    private readonly GitHubOptions _gitHub;
    private readonly InvestigationOptions _investigation;
    private readonly PipelineOptions _pipeline;
    private readonly ILogger<IssuePollingService> _logger;

    public IssuePollingService(
        IIssueSource issues,
        IInvestigator investigator,
        IHostApplicationLifetime lifetime,
        IOptions<GitHubOptions> gitHub,
        IOptions<InvestigationOptions> investigation,
        IOptions<PipelineOptions> pipeline,
        ILogger<IssuePollingService> logger)
    {
        _issues = issues;
        _investigator = investigator;
        _lifetime = lifetime;
        _gitHub = gitHub.Value;
        _investigation = investigation.Value;
        _pipeline = pipeline.Value;
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

            if (_pipeline.RunOnce)
            {
                _logger.LogInformation("RunOnce set — stopping after a single tick.");
                _lifetime.StopApplication();
                return;
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var issues = await _issues.GetOpenIssuesAsync(cancellationToken);
            Console.WriteLine(IssueReportFormatter.FormatAll(issues));

            var target = SelectTarget(issues);
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

    /// <summary>
    /// The configured issue when one is set, otherwise the oldest open one. An explicitly
    /// requested issue that isn't open is an error worth saying out loud — silently
    /// investigating a different issue would look like success.
    /// </summary>
    private Issue? SelectTarget(IReadOnlyList<Issue> issues)
    {
        if (_pipeline.IssueNumber is not { } wanted)
        {
            return issues.OrderBy(i => i.Number).FirstOrDefault();
        }

        var match = issues.FirstOrDefault(i => i.Number == wanted);
        if (match is null)
        {
            _logger.LogError(
                "Pipeline:IssueNumber={Wanted} was requested but is not among the {Count} open issue(s). " +
                "Nothing investigated this tick.", wanted, issues.Count);
        }

        return match;
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
