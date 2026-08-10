using System.Diagnostics;
using Loop.Engine.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loop.Engine.GitHub.Publishing;

/// <summary>
/// Branches, commits, and pushes the verified worktree.
///
/// It runs in the worktree Phase 4 built and tested, so what gets published is exactly what
/// passed — branching from anywhere else risks shipping something subtly different from
/// what the compiler saw, which is how a working-tree/HEAD mismatch silently deleted code
/// earlier in this project's life.
/// </summary>
public sealed class GitPublisher : IGitPublisher
{
    private readonly PublishingOptions _options;
    private readonly ILogger<GitPublisher> _logger;

    public GitPublisher(IOptions<PublishingOptions> options, ILogger<GitPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishBranchAsync(
        string worktreePath,
        string branch,
        string commitMessage,
        CancellationToken cancellationToken = default)
    {
        GuardBranchName(branch);
        GuardBranchDoesNotExist(worktreePath, branch);

        Run(worktreePath, ["checkout", "-b", branch]);
        Run(worktreePath, ["add", "-A"]);
        Run(worktreePath, ["commit", "-m", commitMessage]);
        Run(worktreePath, ["push", "-u", _options.Remote, branch]);

        _logger.LogInformation("Pushed {Branch} to {Remote}.", branch, _options.Remote);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Belt and braces. The branch name is derived, not user-supplied, so this should be
    /// impossible — but a one-line check removes an entire category of catastrophe, and
    /// "should be impossible" is how catastrophes are usually described beforehand.
    /// </summary>
    private void GuardBranchName(string branch)
    {
        if (string.IsNullOrWhiteSpace(branch))
        {
            throw new InvalidOperationException("Refusing to publish to an empty branch name.");
        }

        if (string.Equals(branch, _options.BaseBranch, StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, "main", StringComparison.OrdinalIgnoreCase)
            || string.Equals(branch, "master", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to publish directly to '{branch}'. The pipeline opens pull requests; " +
                "it never writes to a protected branch.");
        }
    }

    /// <summary>
    /// Refuses rather than force-pushing. A second run on the same issue would otherwise
    /// overwrite a pull request a human may be halfway through reviewing.
    /// </summary>
    private void GuardBranchDoesNotExist(string worktreePath, string branch)
    {
        // Exit 0 means the ref was found. Absent is exit 2 — not 1 — so "non-zero means
        // error" would reject every new branch and publish nothing, ever.
        var remote = RunRaw(worktreePath, ["ls-remote", "--exit-code", "--heads", _options.Remote, branch]);

        if (remote.ExitCode == 0)
        {
            throw new InvalidOperationException(
                $"Branch '{branch}' already exists on {_options.Remote}. Refusing to force-push " +
                "over it — a pull request may already be open. Delete the branch or close the PR first.");
        }

        var local = RunRaw(worktreePath, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"]);

        if (local.ExitCode == 0)
        {
            throw new InvalidOperationException($"Branch '{branch}' already exists locally.");
        }
    }

    private void Run(string workingDirectory, IReadOnlyList<string> args)
    {
        var result = RunRaw(workingDirectory, args);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} failed ({result.ExitCode}): {result.Stderr.Trim()}");
        }
    }

    private (int ExitCode, string Stdout, string Stderr) RunRaw(
        string workingDirectory, IReadOnlyList<string> args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            // Separate streams throughout this project: merging them once nearly put git's
            // CRLF advisories inside a diff.
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start 'git'. Is it on PATH?");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        _logger.LogDebug("git {Args} -> {Exit}", string.Join(' ', args), process.ExitCode);

        return (process.ExitCode, stdout, stderr);
    }
}
