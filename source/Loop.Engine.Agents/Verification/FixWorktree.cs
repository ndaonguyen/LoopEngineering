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

    /// <summary>
    /// Creates a worktree at the tip of <paramref name="baseBranch"/> — the branch the
    /// eventual pull request will target.
    ///
    /// Not <c>HEAD</c>. A fix destined for a PR against <c>main</c> must be built on
    /// <c>main</c>: branching from whatever the developer happens to be standing on drags
    /// every unmerged commit of that branch into the PR, so a two-file fix arrives as a
    /// twenty-four-file diff that nobody can review.
    ///
    /// This tree is then used for <b>everything</b> — retrieval, coding, building, and
    /// publishing. One tree means "the code we are changing" and "the code we are
    /// compiling" cannot disagree, which is the failure that silently deleted working code
    /// earlier in this project and would have come back the moment those two diverged.
    /// </summary>
    public static FixWorktree Create(string repositoryRoot, string baseBranch, string remote, ILogger logger)
    {
        var root = System.IO.Path.GetFullPath(repositoryRoot);

        // A hard kill leaves worktree metadata behind; prune before adding so a previous
        // crash cannot block this run.
        Run(root, ["worktree", "prune"], logger, throwOnFailure: false);
        Run(root, ["fetch", remote, baseBranch], logger, throwOnFailure: false);

        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"loop-engine-wt-{Guid.NewGuid():N}");

        var start = ResolveStartPoint(root, baseBranch, remote, logger);

        Run(root, ["worktree", "add", "--detach", path, start], logger, throwOnFailure: true);
        logger.LogInformation("Created worktree at {Path} from {Start}.", path, start);

        return new FixWorktree(root, path, logger);
    }

    /// <summary>
    /// Prefers the remote-tracking ref so the fix is built on what is actually published,
    /// not on a stale local copy. Falls back to the local branch when there is no remote —
    /// which is the case in tests.
    /// </summary>
    private static string ResolveStartPoint(string root, string baseBranch, string remote, ILogger logger)
    {
        var tracking = $"{remote}/{baseBranch}";

        var exists = Run(root, ["rev-parse", "--verify", "--quiet", tracking], logger, throwOnFailure: false);

        if (!string.IsNullOrWhiteSpace(exists))
        {
            return tracking;
        }

        logger.LogWarning(
            "No {Tracking} ref; falling back to the local '{Base}'. The fix may be built on a stale base.",
            tracking, baseBranch);

        return baseBranch;
    }

    // The dirty-tree guard that used to live here is gone, and deliberately so. It existed
    // because the Coder read the developer's working tree while the build ran against HEAD;
    // now every stage reads and writes this one worktree, so there is no second tree left
    // to diverge from. Removing the cause beats guarding the symptom.

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
