namespace Loop.Engine.Core.Model;

/// <summary>One reviewer observation. Advisory — nothing downstream branches on it.</summary>
public sealed record ReviewFinding(string Category, string Severity, string Detail);

/// <summary>The Reviewer's output. Comments only; it has no way to change code.</summary>
public sealed record ReviewReport(IReadOnlyList<ReviewFinding> Findings)
{
    public static ReviewReport Empty { get; } = new([]);
}
