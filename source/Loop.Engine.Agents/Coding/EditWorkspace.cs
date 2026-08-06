using Loop.Engine.Agents.Retrieval;

namespace Loop.Engine.Agents.Coding;

/// <summary>
/// A discardable copy of the allow-listed files.
///
/// Edits never touch the caller's working tree: a bad run must be throwable away, not
/// cleaned up by hand. Two copies are kept — <c>original/</c> and <c>edited/</c> — so the
/// diff is produced by comparing directories rather than trusting the model to emit one.
/// </summary>
public sealed class EditWorkspace : IDisposable
{
    private readonly Dictionary<string, string> _lineEndings = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public string Root { get; }
    public string OriginalDirectory => Path.Combine(Root, "original");
    public string EditedDirectory => Path.Combine(Root, "edited");

    /// <summary>Paths, relative and forward-slashed, that may be written.</summary>
    public IReadOnlyCollection<string> AllowedPaths => _lineEndings.Keys;

    private EditWorkspace(string root) => Root = root;

    public static EditWorkspace Create(IReadOnlyList<RetrievedFile> files)
    {
        var root = Directory.CreateTempSubdirectory("loop-engine-edit").FullName;
        var workspace = new EditWorkspace(root);

        Directory.CreateDirectory(workspace.OriginalDirectory);
        Directory.CreateDirectory(workspace.EditedDirectory);

        foreach (var file in files)
        {
            var relative = file.RelativePath.Replace('\\', '/');
            workspace._lineEndings[relative] = DetectLineEnding(file.Excerpt);

            foreach (var directory in new[] { workspace.OriginalDirectory, workspace.EditedDirectory })
            {
                var full = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, file.Excerpt);
            }
        }

        return workspace;
    }

    /// <summary>
    /// Writes a replacement into <c>edited/</c>. Rejects any path outside the allow-list,
    /// and any path that escapes the workspace once resolved — the model proposes these,
    /// so they are untrusted input, not merely untidy input.
    /// </summary>
    public void Write(string relativePath, string contents)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var relative = relativePath.Replace('\\', '/').Trim();

        if (!_lineEndings.TryGetValue(relative, out var lineEnding))
        {
            throw new InvalidOperationException(
                $"Refusing to write '{relativePath}': it is not among the allow-listed files.");
        }

        var full = Path.GetFullPath(
            Path.Combine(EditedDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));

        // Belt and braces: the allow-list already rejects traversal, but a path check that
        // depends on an earlier check is one refactor away from being no check at all.
        if (!full.StartsWith(Path.GetFullPath(EditedDirectory) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to write outside the workspace: '{relativePath}'.");
        }

        // Re-apply the original's line ending. A '\n' replacement over a '\r\n' original
        // marks every line as changed and the resulting diff is worthless.
        var normalised = contents.ReplaceLineEndings(lineEnding);

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, normalised);
    }

    private static string DetectLineEnding(string text) =>
        text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a run over.
        }
    }
}
