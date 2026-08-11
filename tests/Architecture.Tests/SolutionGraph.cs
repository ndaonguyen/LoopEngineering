using System.Xml.Linq;

namespace Architecture.Tests;

/// <summary>
/// The project-reference graph, read off disk.
///
/// Reading the <c>.csproj</c> files rather than loading assemblies is deliberate: the rule
/// being enforced is about project references, so checking them directly needs no reference of
/// its own. A test project that referenced everything in order to inspect everything would be
/// the one edge the graph could never see.
/// </summary>
internal static class SolutionGraph
{
    private const string SolutionFileName = "LoopEngineering.sln";

    /// <summary>Project name → the project names it references, for everything under <c>source/</c>.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Read()
    {
        var source = Path.Combine(RepositoryRoot(), "source");

        return Directory
            .EnumerateFiles(source, "*.csproj", SearchOption.AllDirectories)
            .Where(IsNotBuildOutput)
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path.AsSpan()).ToString(),
                ReferencesOf,
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Walks up from the test binaries to the directory holding the solution file. The test
    /// runner's working directory is not the repository root and is not worth assuming.
    /// </summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find {SolutionFileName} above {AppContext.BaseDirectory}. " +
            "The architecture tests locate the repository root by that file.");
    }

    private static IReadOnlyList<string> ReferencesOf(string projectPath) =>
        XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            // csproj paths are Windows-style even on other platforms; normalise before taking
            // the leaf so this suite gives the same answer wherever CI runs it.
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static bool IsNotBuildOutput(string path)
    {
        var normalised = path.Replace('\\', '/');

        return !normalised.Contains("/bin/", StringComparison.Ordinal)
            && !normalised.Contains("/obj/", StringComparison.Ordinal);
    }
}
