namespace Loop.Engine.Core.Model;

/// <summary>A test written to fail until the bug is fixed.</summary>
public sealed record ReproductionTest(
    string RelativePath, string Contents, string FullyQualifiedName);

/// <summary>
/// What happened when the reproduction test was run against the <b>unfixed</b> code.
///
/// Only <see cref="Red"/> is permission to continue. The other four are all ways of
/// producing a test that proves nothing, and they are enumerated separately because they
/// need different things said about them — an operator who is told "reproduction failed"
/// learns almost nothing.
/// </summary>
public enum ReproductionOutcome
{
    /// <summary>The test compiled and failed. The defect is now demonstrable.</summary>
    Red,

    /// <summary>The model returned nothing usable.</summary>
    NotProduced,

    /// <summary>The test does not compile. A broken test is not a reproduction.</summary>
    DoesNotCompile,

    /// <summary>
    /// The test compiled and passed against the unfixed code, so it does not exercise the
    /// defect. The most dangerous outcome: it would go green after the fix too, and the
    /// pull request would carry a proof of nothing.
    /// </summary>
    AlreadyPasses,

    /// <summary>
    /// The filter matched no test. <c>dotnet test</c> exits non-zero for this exactly as it
    /// does for a failing test, so without the distinction a mistyped name reads as a
    /// successful reproduction.
    /// </summary>
    TestNotFound,
}

/// <summary>The outcome of the red gate, with a sentence explaining it.</summary>
public sealed record ReproductionResult(
    ReproductionOutcome Outcome, ReproductionTest? Test, string Detail)
{
    public bool IsRed => Outcome == ReproductionOutcome.Red;

    public static ReproductionResult Red(ReproductionTest test, string detail) =>
        new(ReproductionOutcome.Red, test, detail);

    public static ReproductionResult Rejected(
        ReproductionOutcome outcome, ReproductionTest? test, string detail) =>
        new(outcome, test, detail);
}
