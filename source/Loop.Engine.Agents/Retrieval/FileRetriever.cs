using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public FileRetriever(IOptions<RepositoryOptions> options, ILogger<FileRetriever> logger)
    {
        _options = options.Value;
        _logger = logger;
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

    private string ReadCapped(string path, out int lineCount)
    {
        var lines = File.ReadLines(path).Take(_options.MaxLinesPerFile).ToList();
        lineCount = lines.Count;
        return string.Join('\n', lines);
    }
}
