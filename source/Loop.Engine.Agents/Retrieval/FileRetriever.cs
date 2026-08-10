using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;

namespace Loop.Engine.Agents.Retrieval;

/// <summary>
/// Finds the files a set of symbols points at. Filename matches rank above content
/// matches — a symbol appearing in a file's name is far stronger evidence than the same
/// symbol appearing somewhere in its body.
/// </summary>
public sealed class FileRetriever
{
    private readonly RepositoryOptions _options;
    private readonly ILogger<FileRetriever> _logger;
    private readonly string _applicationRoot;

    public FileRetriever(IOptions<RepositoryOptions> options, ILogger<FileRetriever> logger)
    {
        _options = options.Value;
        _logger = logger;
        _applicationRoot = Path.GetFullPath(AppContext.BaseDirectory);
        
        // Ensure RootPath is absolute; if relative, throw exception
        if (!Path.IsPathRooted(_options.RootPath))
        {
            throw new InvalidOperationException($"RootPath '{_options.RootPath}' must be an absolute path.");
        }

        // Resolve RootPath against the application root
        _options.RootPath = Path.Combine(_applicationRoot, _options.RootPath);
    }

    public IReadOnlyList<RetrievedFile> Retrieve(IReadOnlyList<string> symbols)
    {
        if (symbols.Count == 0)
        {
            _logger.LogWarning("No symbols extracted; retrieval has nothing to search for.");
            return [];
        }

        var root = Path.GetFullPath(_options.RootPath);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Repository root '{root}' does not exist.");
        }

        var matcher = new Matcher();
        matcher.AddIncludePatterns(_options.IncludeGlobs);
        var candidates = matcher.GetResultsInFullPath(root).ToList();

        var scored = new List<(RetrievedFile File, int Score)>();

        foreach (var path in candidates)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var text = ReadCapped(path, out var lineCount);

            var matched = new List<string>();
            var score = 0;

            foreach (var symbol in symbols)
            {
                if (string.Equals(name, symbol, StringComparison.OrdinalIgnoreCase))
                {
                    // The file is named after the symbol — the strongest signal available.
                    score += 100;
                    matched.Add(symbol);
                }
                else if (text.Contains(symbol, StringComparison.Ordinal))
                {
                    score += 1;
                    matched.Add(symbol);
                }
            }

            if (score > 0)
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                scored.Add((new RetrievedFile(relative, text, matched), score));
                _logger.LogDebug("Candidate {Path} scored {Score} ({Lines} lines).", relative, score, lineCount);
            }
        }

        var results = scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.File.RelativePath, StringComparer.Ordinal)
            .Take(_options.MaxFiles)
            .Select(s => s.File)
            .ToList();

        _logger.LogInformation(
            "Retrieved {Count} file(s) from {Candidates} candidate(s) for {Symbols} symbol(s).",
            results.Count, candidates.Count, symbols.Count);

        return results;
    }

    /// <summary>
    /// Reads a known list of paths. Not a search: the Coder is handed the investigation's
    /// findings and reads exactly those, so nothing here widens what it can see. Paths
    /// that escape the repository root are refused rather than skipped — they should be
    /// impossible, and quietly ignoring an impossible input hides the bug that produced it.
    /// </summary>
    public IReadOnlyList<RetrievedFile> ReadFiles(IReadOnlyList<string> relativePaths)
    {
        var root = Path.GetFullPath(_options.RootPath);
        var files = new List<RetrievedFile>();

        foreach (var relative in relativePaths)
        {
            var normalised = relative.Replace('\\', '/').Trim();
            var full = Path.GetFullPath(Path.Combine(root, normalised.Replace('/', Path.DirectorySeparatorChar)));

            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Refusing to read '{relative}': it resolves outside the repository root.");
            }

            if (!File.Exists(full))
            {
                _logger.LogWarning("Skipping '{Path}' — it no longer exists under {Root}.", normalised, root);
                continue;
            }

            // Whole file, not the capped excerpt used for retrieval: the Coder is rewriting
            // it, and a truncated original would be silently rewritten as a truncated file.
            files.Add(new RetrievedFile(normalised, File.ReadAllText(full), [normalised]));
        }

        return files;
    }

    private string ReadCapped(string path, out int lineCount)
    {
        var lines = File.ReadLines(path).Take(_options.MaxLinesPerFile).ToList();
        lineCount = lines.Count;
        return string.Join('\n', lines);
    }
}