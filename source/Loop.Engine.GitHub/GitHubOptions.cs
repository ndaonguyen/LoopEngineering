using System.ComponentModel.DataAnnotations;

namespace Loop.Engine.GitHub;

/// <summary>Configuration for the GitHub connection. Bound from the <c>GitHub</c> section.</summary>
public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>Repository owner, e.g. <c>ndaonguyen</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Owner { get; set; } = string.Empty;

    /// <summary>Repository name, e.g. <c>LoopEngineering</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Repository { get; set; } = string.Empty;

    /// <summary>
    /// A personal access token with <c>repo</c> scope. Never commit one — supply it via
    /// user-secrets locally (<c>GitHub:Token</c>) or the <c>GitHub__Token</c> environment
    /// variable in CI. The token needs <b>write</b> access: a read-only token can list
    /// issues but silently fails to label them, which looks like the pipeline doing nothing.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Token { get; set; } = string.Empty;

    /// <summary>How often the scheduler polls for issues.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>User-Agent sent to the GitHub API; required by their terms.</summary>
    public string ProductHeader { get; set; } = "loop-engine";
}
