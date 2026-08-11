namespace Loop.Engine.Agents.Reproduction;

/// <summary>
/// Decides where the reproduction test file goes.
///
/// The model derives the path from the namespace of the code under test, which on a real run
/// produced <c>tests/Loop.Engine.Agents.Retrieval/SymbolExtractorTests.cs</c> — a directory
/// belonging to no project. Nothing compiled it, the filter matched nothing, and the gate
/// read that as the test passing.
///
/// Placement is mechanical, so it is decided here rather than asked for. Pure, because a
/// path rule that only runs behind a model call is a path rule nobody can test.
/// </summary>
public static class TestFilePath
{
    /// <summary>
    /// The proposed path if the project that runs the suite already contains it, otherwise
    /// the same filename inside that project's directory.
    ///
    /// Only the directory moves. The namespace inside the file is left alone — C# does not
    /// require it to match the folder, and the fully-qualified name is read from the syntax
    /// tree, so the two cannot drift apart.
    /// </summary>
    public static string InsideProject(string proposed, string? testProject)
    {
        if (string.IsNullOrWhiteSpace(proposed) || string.IsNullOrWhiteSpace(testProject))
        {
            return proposed;
        }

        var directory = System.IO.Path.GetDirectoryName(testProject)?.Replace('\\', '/').TrimEnd('/');

        if (string.IsNullOrWhiteSpace(directory))
        {
            return proposed;
        }

        var normalised = proposed.Replace('\\', '/').Trim();

        if (normalised.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase))
        {
            return normalised;
        }

        return $"{directory}/{System.IO.Path.GetFileName(normalised)}";
    }
}
