namespace Loop.Engine.Core.Model;

/// <summary>
/// The outcome of a build or test run.
///
/// <see cref="Errors"/> deliberately carries errors and test failures only — never
/// warnings. This repository already emits <c>NU1902</c>/<c>NU1903</c> advisories that
/// predate every agent and that no fix can address; if warnings could reach the retry
/// loop it would exhaust all five attempts on day one against something unrelated to the
/// bug. Not carrying them is what makes that impossible rather than merely unlikely.
/// </summary>
public sealed record BuildResult(bool Succeeded, IReadOnlyList<string> Errors, string RawOutput)
{
    public static BuildResult Success(string output = "") => new(true, [], output);

    public static BuildResult Failure(IReadOnlyList<string> errors, string output = "") =>
        new(false, errors, output);
}
