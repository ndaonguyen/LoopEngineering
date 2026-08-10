using System.Diagnostics;
using System.Text;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loop.Engine.Agents.Verification;

/// <summary>Shells out to the real <c>dotnet</c> CLI.</summary>
public sealed class DotnetBuildRunner : IBuildRunner
{
    private readonly VerificationOptions _options;
    private readonly ILogger<DotnetBuildRunner> _logger;

    public DotnetBuildRunner(IOptions<VerificationOptions> options, ILogger<DotnetBuildRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<BuildResult> BuildAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
        RunAsync(workingDirectory, ["build", "--nologo", "-v", "q"], "build", cancellationToken);

    public Task<BuildResult> TestAsync(
        string workingDirectory,
        string? projectFilter = null,
        string? testFilter = null,
        CancellationToken cancellationToken = default)
    {
        List<string> args = ["test", "--nologo", "-v", "q"];

        var project = projectFilter ?? _options.TestProject;
        if (!string.IsNullOrWhiteSpace(project))
        {
            args.Add(project);
        }

        if (!string.IsNullOrWhiteSpace(testFilter))
        {
            // Exact match, not `~`. A substring filter on a generated name can sweep in
            // neighbouring tests, and then a failure somewhere else reads as the bug being
            // reproduced.
            args.Add("--filter");
            args.Add($"FullyQualifiedName={testFilter}");
        }

        return RunAsync(workingDirectory, args, "test", cancellationToken);
    }

    private async Task<BuildResult> RunAsync(
        string workingDirectory, IReadOnlyList<string> args, string label, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            // Separate streams, as in DiffGenerator: merging them once nearly put git's
            // CRLF advisories inside a diff, and the same mistake here would put restore
            // chatter inside the errors handed back to the model.
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start 'dotnet'. Is the SDK on PATH?");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return BuildResult.Failure(
                [$"The {label} timed out after {_options.Timeout}."], string.Empty);
        }

        var output = new StringBuilder()
            .AppendLine(await stdout)
            .AppendLine(await stderr)
            .ToString();

        var errors = BuildOutputParser.ParseErrors(output);
        var succeeded = BuildOutputParser.Succeeded(output, process.ExitCode);

        _logger.LogInformation(
            "dotnet {Label} finished with exit code {Code} and {Errors} error(s).",
            label, process.ExitCode, errors.Count);

        return succeeded
            ? BuildResult.Success(output)
            : BuildResult.Failure(
                errors.Count > 0 ? errors : [$"The {label} failed with exit code {process.ExitCode}."],
                output);
    }

    private void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not kill the timed-out dotnet process.");
        }
    }
}
