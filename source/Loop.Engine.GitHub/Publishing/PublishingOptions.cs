using System.ComponentModel.DataAnnotations;

namespace Loop.Engine.GitHub.Publishing;

/// <summary>How verified fixes are published. Bound from the <c>Publishing</c> section.</summary>
public sealed class PublishingOptions
{
    public const string SectionName = "Publishing";

    /// <summary>
    /// Branch prefix. Matches the <c>bug-loop</c> skill's <c>fix/&lt;n&gt;-&lt;slug&gt;</c>
    /// so both loops are legible in one branch list.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string BranchPrefix { get; set; } = "fix/";

    [Required(AllowEmptyStrings = false)]
    public string Remote { get; set; } = "origin";

    /// <summary>The branch pull requests target.</summary>
    [Required(AllowEmptyStrings = false)]
    public string BaseBranch { get; set; } = "main";

    /// <summary>
    /// Applied to a pull request whose automated review raised a high-severity finding.
    /// A signal, not a gate — the pull request opens either way.
    ///
    /// Set to empty to skip labelling.
    /// </summary>
    public string NeedsHumanLabel { get; set; } = "needs-human";
}
