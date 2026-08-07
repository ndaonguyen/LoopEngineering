using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Loop.Engine.Agents.Coding;

/// <summary>
/// Produces the unified diff by running <c>git diff --no-index</c> over the workspace's
/// two directories. The model never authors a diff — it returns whole files and git works
/// out the difference, which is far more reliable than asking a model for valid patch
/// syntax.
/// </summary>
public sealed class DiffGenerator
{
    private readonly ILogger<DiffGenerator> _logger;

    public DiffGenerator(ILogger<DiffGenerator> logger) => _logger = logger;

    public async Task<string> GenerateAsync(EditWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = workspace.Root,
            RedirectStandardOutput = true,
            // Captured separately, never merged: git emits CRLF advisories on Windows and
            // they must not end up inside the diff text.
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        info.ArgumentList.Add("diff");
        info.ArgumentList.Add("--no-index");
        info.ArgumentList.Add("--");
        info.ArgumentList.Add("original");
        info.ArgumentList.Add("edited");

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start 'git'. Is it on PATH?");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        // Exit code 1 means "differences found" — that is the success case here. Only 2 and
        // above are real failures. Reading this the obvious way yields a silent empty diff.
        if (process.ExitCode > 1)
        {
            throw new InvalidOperationException(
                $"git diff failed with exit code {process.ExitCode}: {stderr.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _logger.LogDebug("git diff wrote to stderr: {Stderr}", stderr.Trim());
        }

        if (process.ExitCode == 0)
        {
            _logger.LogWarning("The Coder produced no textual change — the diff is empty.");
        }

        return stdout;
    }
}
