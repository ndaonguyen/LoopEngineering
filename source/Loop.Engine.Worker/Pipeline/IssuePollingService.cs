using Loop.Engine.Core.Abstractions;
using Loop.Engine.GitHub;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loop.Engine.Worker.Pipeline;

/// <summary>
/// The scheduler. Polls GitHub on an interval and prints what it finds.
///
/// Phase 1 stops here on purpose — no queue, no AI, no code changes. The point is to prove
/// the pipeline runs on a timer and can authenticate, before anything harder is layered on.
/// </summary>
public sealed class IssuePollingService : BackgroundService
{
    private readonly IIssueSource _issues;
    private readonly GitHubOptions _options;
    private readonly ILogger<IssuePollingService> _logger;

    public IssuePollingService(
        IIssueSource issues,
        IOptions<GitHubOptions> options,
        ILogger<IssuePollingService> logger)
    {
        _issues = issues;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Loop.Engine started. Polling {Owner}/{Repository} every {Interval}.",
            _options.Owner, _options.Repository, _options.PollInterval);

        using var timer = new PeriodicTimer(_options.PollInterval);

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
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A transient GitHub failure must not kill the scheduler — log it and wait for
            // the next tick. Anything non-transient will keep showing up in the logs.
            _logger.LogError(ex, "Poll failed; retrying on the next tick.");
        }
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
