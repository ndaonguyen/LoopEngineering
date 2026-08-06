using System.ComponentModel.DataAnnotations;

namespace Loop.Engine.Agents.Retrieval;

/// <summary>Where the source under investigation lives, and how much of it to read.</summary>
public sealed class RepositoryOptions
{
    public const string SectionName = "Repository";

    /// <summary>
    /// Absolute path to the repository root. The engine runs beside the repository rather
    /// than cloning it — cloning buys nothing until Phase 5 needs an isolated worktree.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Globs searched for candidate files, relative to <see cref="RootPath"/>.</summary>
    public string[] IncludeGlobs { get; set; } = ["source/**/*.cs", "tests/**/*.cs"];

    /// <summary>Upper bound on files handed to the model. Context is the scarce resource.</summary>
    [Range(1, 100)]
    public int MaxFiles { get; set; } = 15;

    /// <summary>Upper bound on lines read from any single file.</summary>
    [Range(20, 5000)]
    public int MaxLinesPerFile { get; set; } = 400;
}
