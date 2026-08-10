using System.Diagnostics;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
using Microsoft.Extensions.Logging;

namespace Loop.Engine.Agents.Verification;

/// <summary>
/// A throwaway git worktree the fix is built in.
///
/// <c>EditWorkspace</c> holds only the allow-listed files, which is enough to diff and not
/// nearly enough to compile. A detached worktree is a full checkout that costs no re-clone,
/// and it keeps every build off the user's branch and working tree.
///
/// Edits are applied by <b>writing file contents</b>, not by <c>git apply</c>: the diff's
/// paths are workspace-relative (<c>a/original/…</c>) and were never meant to be applied.
/// </summary>
public sealed class FixWorktree : IFixWorkspace, IDisposable
{
    private readonly string _repositoryRoot;
    private readonly ILogger _logger;
    private bool _disposed;

    public string Path { get; }

    private FixWorktree(string repositoryRoot, string path, ILogger logger)
    {
        _repositoryRoot = repositoryRoot;
        Path = path;
        _logger = logger;
    }

    public static FixWorktree Create(string repositoryRoot, ILogger logger)
    {
        var root = System.IO.Path.GetFullPath(repositoryRoot);

        GuardAgainstUncommittedChanges(root, logger);

        // A hard kill leaves worktree metadata behind; prune before adding so a previous
        // crash cannot block this run.
        Run(root, ["worktree", "prune"], logger, throwOnFailure: false);

        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"loop-engine-wt-{Guid.NewGuid():N}");

        Run(root, ["worktree", "add", "--detach", path, "HEAD"], logger, throwOnFailure: true);
        logger.LogInformation("Created worktree at {Path}.", path);

        return new FixWorktree(root, path, logger);
    }

    /// <summary>
    /// Refuses to verify when the working tree has uncommitted changes.
    ///
    /// The Coder reads files from the working tree; this worktree is checked out at HEAD.
    /// When those disagree, the model is shown code the compiler cannot see, and the
    /// repair loop reasons correctly to a catastrophic conclusion: the references must be
    /// fabricated, so delete them. It converges — on stripping out working functionality,
    /// reporting success the whole way.
    ///
    /// That happened. It is not a hypothetical, and it is silent, which is why this is a
    /// hard failure rather than a warning. The real fix is to read and build from the same
    /// tree; until then, refusing on a dirty tree removes the mismatch entirely.
    /// </summary>
    private static void GuardAgainstUncommittedChanges(string root, ILogger logger)
    {
        var status = Run(root, ["status", "--porcelain"], logger, throwOnFailure: false).Trim();

        if (status.Length == 0)
        {
            return;
        }

        // Porcelain lines are "XY path". Split on the first space rather than slicing at a
        // fixed offset: the whole output was already trimmed, so the leading status column
        // is no longer a reliable width.
        var changed = status
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                var trimmed = line.Trim();
                var space = trimmed.IndexOf(' ');
                return space >= 0 ? trimmed[(space + 1)..].Trim() : trimmed;
            })
            .Take(10)
            .ToList();

        throw new InvalidOperationException(
            "Refusing to verify a fix while the working tree has uncommitted changes. " +
            "The Coder reads the working tree but the build runs against HEAD, so any " +
            "difference makes the compiler judge code it was never shown — and the repair " +
            "loop 'fixes' that by deleting the code it cannot see. Commit or stash first. " +
            $"Uncommitted: {string.Join(", ", changed)}" +
            (status.Split('\n').Length > 10 ? ", …" : string.Empty));
    }

    /// <summary>Writes the Coder's file contents into the worktree.</summary>
    public void Apply(IReadOnlyList<CodeEdit> edits)
    {
        foreach (var edit in edits)
        {
            var relative = edit.RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar);
            var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, relative));

            if (!full.StartsWith(Path + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Refusing to write '{edit.RelativePath}' outside the worktree.");
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);

            // Preserve the existing file's line endings — a '\n' rewrite over '\r\n' shows
            // as a wholesale change and, worse, obscures the real edit in any later diff.
            var lineEnding = File.Exists(full) && File.ReadAllText(full).Contains("\r\n", StringComparison.Ordinal)
                ? "\r\n"
                : "\n";

            File.WriteAllText(full, edit.NewContents.ReplaceLineEndings(lineEnding));
        }

        _logger.LogInformation("Applied {Count} edit(s) to the worktree.", edits.Count);
    }

    private static string Run(
        string workingDirectory, IReadOnlyList<string> args, ILogger logger, bool throwOnFailure)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
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

        if (process.ExitCode != 0)
        {
            var message = $"git {string.Join(' ', args)} failed ({process.ExitCode}): {stderr.Trim()}";

            if (throwOnFailure)
            {
                throw new InvalidOperationException(message);
            }

            logger.LogDebug("{Message}", message);
        }

        return stdout;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            Run(_repositoryRoot, ["worktree", "remove", "--force", Path], _logger, throwOnFailure: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove the worktree at {Path}.", Path);
        }
    }
}
