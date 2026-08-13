using Loop.Engine.Agents.Providers;
using System.Text;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Reproduction;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Agents.Verification;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
using Loop.Engine.GitHub;
using Loop.Engine.GitHub.Publishing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loop.Engine.Worker.Pipeline;

/// <summary>
/// The scheduler. Polls GitHub on an interval, prints what it finds, investigates one
/// issue, and — when <see cref="PipelineOptions.GenerateFix"/> is set — plans and writes a
/// fix diff for it.
///
/// It stops at the diff. No branch, no commit, no PR, and no build: verifying the fix is
/// Phase 4 and publishing it is Phase 5. One issue per tick, because concurrent branches
/// racing each other would only make the first failures harder to read.
/// </summary>
public sealed class IssuePollingService : BackgroundService
{
    private readonly IIssueSource _issues;
    private readonly IInFlightFixes _inFlight;
    private readonly IInvestigator _investigator;
    private readonly IPlanner _planner;
    private readonly ICoder _coder;
    private readonly IReproducer _reproducer;
    private readonly ReproductionGate _gate;
    private readonly FixVerifier _verifier;
    private readonly IReviewer _reviewer;
    private readonly IGitPublisher _git;
    private readonly IPullRequestPublisher _pullRequests;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly GitHubOptions _gitHub;
    private readonly AiOptions _investigation;
    private readonly RepositoryOptions _repository;
    private readonly PublishingOptions _publishing;
    private readonly PipelineOptions _pipeline;
    private readonly RunMetrics _metrics;
    private readonly ILogger<IssuePollingService> _logger;

    public IssuePollingService(
        IIssueSource issues,
        IInFlightFixes inFlight,
        IInvestigator investigator,
        IPlanner planner,
        ICoder coder,
        IReproducer reproducer,
        ReproductionGate gate,
        FixVerifier verifier,
        IReviewer reviewer,
        IGitPublisher git,
        IPullRequestPublisher pullRequests,
        IHostApplicationLifetime lifetime,
        IOptions<GitHubOptions> gitHub,
        IOptions<AiOptions> investigation,
        IOptions<RepositoryOptions> repository,
        IOptions<PublishingOptions> publishing,
        IOptions<PipelineOptions> pipeline,
        RunMetrics metrics,
        ILogger<IssuePollingService> logger)
    {
        _issues = issues;
        _inFlight = inFlight;
        _investigator = investigator;
        _planner = planner;
        _coder = coder;
        _reproducer = reproducer;
        _gate = gate;
        _verifier = verifier;
        _reviewer = reviewer;
        _git = git;
        _pullRequests = pullRequests;
        _lifetime = lifetime;
        _gitHub = gitHub.Value;
        _investigation = investigation.Value;
        _repository = repository.Value;
        _publishing = publishing.Value;
        _pipeline = pipeline.Value;
        _metrics = metrics;
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
        // Figures are per-tick, not lifetime. A service running for a week would otherwise
        // report one enormous cumulative number that says nothing about any single run.
        _metrics.Reset();

        try
        {
            var issues = await _issues.GetOpenIssuesAsync(cancellationToken);
            Console.WriteLine(IssueReportFormatter.FormatAll(issues, _pipeline.RequiredLabel));

            // Asked before picking, not after: an issue whose fix is already open would
            // otherwise be rebuilt from scratch every tick and fail at push.
            var inFlight = await _inFlight.GetIssuesWithOpenFixAsync(cancellationToken);

            var target = SelectTarget(issues, inFlight);
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
        finally
        {
            // In `finally` on purpose: the run that burned the budget and produced nothing
            // is exactly the one whose cost you want to see.
            ReportMetrics();
        }
    }

    /// <summary>
    /// The configured issue when one is set, otherwise the oldest eligible one — see
    /// <see cref="IssueSelector"/> for the policy. Anything that stops a tick from working
    /// is said out loud: doing nothing quietly is indistinguishable from success.
    /// </summary>
    private Issue? SelectTarget(IReadOnlyList<Issue> issues, IReadOnlySet<int> inFlight)
    {
        var selection = IssueSelector.Select(
            issues, _pipeline.IssueNumber, _pipeline.RequiredLabel, inFlight);

        if (selection.Rejection is { } reason)
        {
            _logger.LogWarning("{Reason}", reason);
        }

        return selection.Issue;
    }

    private async Task InvestigateAsync(Issue issue, CancellationToken cancellationToken)
    {
        // One worktree, created from the branch the PR will target, used by every stage:
        // retrieval, coding, building, publishing. Two trees would eventually disagree
        // about which code is being changed — and when they did, the repair loop resolved
        // it by deleting the code the compiler could not see.
        using var worktree = FixWorktree.Create(
            _repository.RootPath, _publishing.BaseBranch, _publishing.Remote, _logger);

        using var scoped = ScopedRetrievalRoot(worktree.Path);

        AnalysisResult analysis;
        using (_metrics.BeginStage("Investigation"))
        {
            analysis = await _investigator.InvestigateAsync(issue, cancellationToken);
        }

        var path = await WriteReportAsync(issue, analysis.Markdown, cancellationToken);

        Console.WriteLine();
        Console.WriteLine($"Investigated #{issue.Number} -> {path}");
        Console.WriteLine($"Affected files: {analysis.AffectedFiles.Count}");

        if (_pipeline.GenerateFix)
        {
            await GenerateFixAsync(issue, analysis, worktree, cancellationToken);
        }
    }

    /// <summary>
    /// Prints what the tick spent. Always — including when a stage failed, because the run
    /// that burned the budget without producing anything is the one worth seeing.
    /// </summary>
    private void ReportMetrics()
    {
        if (_metrics.TotalModelCalls == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Run cost:");
        Console.WriteLine(_metrics.Render(_investigation.InputCostPerMillion, _investigation.OutputCostPerMillion));
    }

    /// <summary>
    /// Points retrieval at the worktree for the duration of the run, so the files the model
    /// is shown are the files that will be compiled and published.
    /// </summary>
    private IDisposable ScopedRetrievalRoot(string worktreePath)
    {
        var previous = _repository.RootPath;
        _repository.RootPath = worktreePath;

        return new Restore(() => _repository.RootPath = previous);
    }

    private sealed class Restore(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    /// <summary>
    /// Phase 3: plan, then code, then write the diff. Stops there — no branch, no commit,
    /// no PR, and no build. Verifying the diff is Phase 4's job.
    /// </summary>
    private async Task GenerateFixAsync(
        Issue issue, AnalysisResult analysis, FixWorktree worktree, CancellationToken cancellationToken)
    {
        FixPlan plan;
        using (_metrics.BeginStage("Planning"))
        {
            plan = await _planner.PlanAsync(issue, analysis, cancellationToken);
        }

        Console.WriteLine();
        Console.WriteLine($"Plan for #{issue.Number}:");
        foreach (var step in plan.Steps)
        {
            Console.WriteLine($"  {step.Order}. {step.Description}");
        }

        // The red gate runs before the fix exists, which is the only moment the test can
        // prove anything: a test written after the fix, against fixed code, cannot
        // demonstrate that it ever caught the bug.
        ReproductionResult? reproduction = null;

        if (_pipeline.ReproduceFirst)
        {
            reproduction = await ReproduceAsync(issue, analysis, plan, worktree, cancellationToken);

            if (!reproduction.IsRed)
            {
                Console.WriteLine();
                Console.WriteLine($"Reproduction rejected ({reproduction.Outcome}): {reproduction.Detail}");
                Console.WriteLine("Stopping. A fix nobody can prove is a fix nobody should merge.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Reproduced: {reproduction.Detail}");
        }

        CodeChangeSet changes;
        using (_metrics.BeginStage("Coding"))
        {
            changes = await _coder.WriteCodeAsync(issue, analysis, plan, cancellationToken);
        }

        if (!changes.HasChanges)
        {
            Console.WriteLine();
            Console.WriteLine($"Coder produced no textual change for #{issue.Number}.");
            return;
        }

        var diffPath = await WriteDiffAsync(issue, changes.UnifiedDiff, cancellationToken);

        Console.WriteLine();
        Console.WriteLine($"Wrote fix for #{issue.Number} -> {diffPath}");
        Console.WriteLine($"Files changed: {changes.Edits.Count}");

        if (_pipeline.VerifyFix)
        {
            await VerifyAndReviewAsync(issue, analysis, changes, worktree, reproduction, cancellationToken);
        }
    }

    /// <summary>
    /// Writes the failing test and puts it through the gate. The model's opinion of its own
    /// test counts for nothing here — the compiler and the runner decide.
    /// </summary>
    private async Task<ReproductionResult> ReproduceAsync(
        Issue issue,
        AnalysisResult analysis,
        FixPlan plan,
        FixWorktree worktree,
        CancellationToken cancellationToken)
    {
        using (_metrics.BeginStage("Reproduction"))
        {
            var test = await _reproducer.WriteFailingTestAsync(issue, analysis, plan, cancellationToken);

            var result = await _gate.RunAsync(test, worktree, cancellationToken);

            await WriteReproductionAsync(issue, result, cancellationToken);

            return result;
        }
    }

    /// <summary>
    /// Saves the candidate test, whether it passed the gate or not.
    ///
    /// A rejected test is discarded with the worktree, which left a rejection message naming
    /// a file nobody could read — and "it passes against the unfixed code" is not a
    /// diagnosis anyone can act on without seeing what it asserted. The other stages all
    /// leave an artifact behind; this one should too.
    /// </summary>
    private async Task WriteReproductionAsync(
        Issue issue, ReproductionResult result, CancellationToken cancellationToken)
    {
        if (result.Test is not { } test)
        {
            return;
        }

        var directory = Path.GetFullPath(_investigation.OutputDirectory);
        Directory.CreateDirectory(directory);

        // Markdown, not `.cs`. The output directory sits inside a project by default, so a
        // `.cs` artifact is picked up by the compiler and breaks the very build it is
        // reporting on — which is exactly what the first version of this did.
        var contents =
            $"""
             # Reproduction test — #{issue.Number}

             **Outcome:** {result.Outcome}

             {result.Detail}

             Written to `{test.RelativePath}`.

             ```csharp
             {test.Contents}
             ```
             """;

        var path = Path.Combine(directory, $"reproduction-{issue.Number}.md");
        await File.WriteAllTextAsync(path, contents, cancellationToken);

        Console.WriteLine($"Reproduction test for #{issue.Number} -> {path}");
    }

    /// <summary>
    /// Phase 4: build, test, repair, review. The first stage that can prove a fix wrong
    /// rather than merely reasoning about it.
    /// </summary>
    private async Task VerifyAndReviewAsync(
        Issue issue,
        AnalysisResult analysis,
        CodeChangeSet changes,
        FixWorktree worktree,
        ReproductionResult? reproduction,
        CancellationToken cancellationToken)
    {
        VerificationResult verification;
        using (_metrics.BeginStage("Verification"))
        {
            verification = await _verifier.VerifyAsync(issue, changes.Edits, worktree, cancellationToken);
        }

        Console.WriteLine();
        Console.WriteLine(verification.StuckReport());

        if (!verification.Succeeded)
        {
            // Stop here deliberately. A review of a fix that does not build would describe
            // problems the compiler already named, more vaguely.
            return;
        }

        ReviewReport review;
        using (_metrics.BeginStage("Review"))
        {
            review = await _reviewer.ReviewAsync(issue, changes.UnifiedDiff, cancellationToken);
        }

        var reviewPath = await WriteReviewAsync(issue, verification, review, cancellationToken);

        Console.WriteLine($"Review for #{issue.Number} -> {reviewPath} ({review.Findings.Count} finding(s))");

        if (_pipeline.PublishPr)
        {
            await PublishAsync(issue, analysis, verification, review, worktree, reproduction, cancellationToken);
        }
    }

    /// <summary>
    /// Phase 5: branch, commit, push, open the pull request — in the worktree that was
    /// verified, so what ships is exactly what passed. Then stop. A human merges.
    /// </summary>
    private async Task PublishAsync(
        Issue issue,
        AnalysisResult analysis,
        VerificationResult verification,
        ReviewReport review,
        FixWorktree worktree,
        ReproductionResult? reproduction,
        CancellationToken cancellationToken)
    {
        try
        {
            var context = PullRequestBuilder.Build(
                issue, analysis, verification, review, _publishing.BranchPrefix, reproduction);

            await _git.PublishBranchAsync(
                worktree.Path, context.BranchName, context.Title, cancellationToken);

            var pr = await _pullRequests.OpenAsync(context, cancellationToken);

            Console.WriteLine();
            Console.WriteLine($"Opened PR #{pr.Number} -> {pr.Url}");
            Console.WriteLine("A human reviews and merges. The pipeline stops here.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed publish must not discard a verified fix — the diff and the review
            // are already on disk, and the branch can be pushed by hand.
            _logger.LogError(ex, "Could not publish the fix for #{Number}.", issue.Number);

            Console.WriteLine();
            Console.WriteLine($"Publish failed: {ex.Message}");
            Console.WriteLine("The verified diff and review are still on disk.");
        }
    }

    private async Task<string> WriteReviewAsync(
        Issue issue, VerificationResult verification, ReviewReport review, CancellationToken cancellationToken)
    {
        var directory = Path.GetFullPath(_investigation.OutputDirectory);
        Directory.CreateDirectory(directory);

        var markdown = new StringBuilder();
        markdown.AppendLine($"# Review — #{issue.Number}: {issue.Title}");
        markdown.AppendLine();
        markdown.AppendLine($"Verified after {verification.Attempts} attempt(s).");
        markdown.AppendLine();

        if (verification.Hypotheses.Count > 0)
        {
            markdown.AppendLine("## Repair history");
            markdown.AppendLine();
            foreach (var (hypothesis, index) in verification.Hypotheses.Select((h, i) => (h, i + 1)))
            {
                markdown.AppendLine($"{index}. {hypothesis}");
            }
            markdown.AppendLine();
        }

        markdown.AppendLine("## Run cost");
        markdown.AppendLine();
        markdown.AppendLine(_metrics.Render(
            _investigation.InputCostPerMillion, _investigation.OutputCostPerMillion));
        markdown.AppendLine();

        markdown.AppendLine("## Findings");
        markdown.AppendLine();

        if (review.Findings.Count == 0)
        {
            markdown.AppendLine("_None. A small, correct diff is allowed to have nothing wrong with it._");
        }
        else
        {
            foreach (var finding in review.Findings)
            {
                markdown.AppendLine($"### {finding.Category} — {finding.Severity}");
                markdown.AppendLine();
                markdown.AppendLine(finding.Detail);
                markdown.AppendLine();
            }
        }

        var path = Path.Combine(directory, $"review-{issue.Number}.md");
        await File.WriteAllTextAsync(path, markdown.ToString(), cancellationToken);

        _logger.LogInformation("Wrote review for #{Number} to {Path}.", issue.Number, path);
        return path;
    }

    private async Task<string> WriteDiffAsync(Issue issue, string diff, CancellationToken cancellationToken)
    {
        var directory = Path.GetFullPath(_investigation.OutputDirectory);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"fix-{issue.Number}.diff");
        await File.WriteAllTextAsync(path, diff, cancellationToken);

        _logger.LogInformation("Wrote fix diff for #{Number} to {Path}.", issue.Number, path);
        return path;
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
