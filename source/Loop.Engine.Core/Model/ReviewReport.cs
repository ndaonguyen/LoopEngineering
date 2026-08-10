namespace Loop.Engine.Core.Model;

/// <summary>Severity of a finding, ordered so the highest can be compared.</summary>
public enum ReviewSeverity
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}

/// <summary>One reviewer observation. It cannot change code — only describe it.</summary>
public sealed record ReviewFinding(string Category, string Severity, string Detail)
{
    /// <summary>
    /// The reviewer is a language model writing into a string, so the vocabulary it was
    /// asked for ("low | medium | high") is a request, not a guarantee. Anything else is
    /// treated as <see cref="ReviewSeverity.None"/> rather than guessed at — a misread
    /// "high" would label every pull request and teach a reviewer to ignore the label.
    /// </summary>
    public ReviewSeverity ParsedSeverity => Severity?.Trim().ToLowerInvariant() switch
    {
        "high" => ReviewSeverity.High,
        "medium" => ReviewSeverity.Medium,
        "low" => ReviewSeverity.Low,
        _ => ReviewSeverity.None,
    };
}

/// <summary>The Reviewer's output. Comments only; it has no way to change code.</summary>
public sealed record ReviewReport(IReadOnlyList<ReviewFinding> Findings)
{
    public static ReviewReport Empty { get; } = new([]);

    /// <summary>The worst finding, or <see cref="ReviewSeverity.None"/> when there are none.</summary>
    public ReviewSeverity HighestSeverity =>
        Findings.Count == 0 ? ReviewSeverity.None : Findings.Max(f => f.ParsedSeverity);

    /// <summary>
    /// Whether a human should look before trusting this. Deliberately not a gate on
    /// publishing: a flagged pull request is more useful than no pull request, and
    /// withholding one turns a reviewable artifact into nothing at all.
    /// </summary>
    public bool NeedsHuman => HighestSeverity >= ReviewSeverity.High;
}
