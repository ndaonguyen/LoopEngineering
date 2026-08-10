using System.ComponentModel.DataAnnotations;

namespace Loop.Engine.Agents.Verification;

/// <summary>Configuration for the build-and-test retry loop. Bound from <c>Verification</c>.</summary>
public sealed class VerificationOptions
{
    public const string SectionName = "Verification";

    /// <summary>
    /// Attempts before giving up, counting the Coder's original output as attempt 1.
    ///
    /// A hard limit. Raising it to get past a stubborn bug trades a bounded failure for an
    /// unbounded one — the honest outcome after five tries is a stuck report, not a sixth
    /// variation on the same wrong idea.
    /// </summary>
    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Project passed to <c>dotnet test</c>. Defaults to the engine's own tests: the full
    /// solution takes ~60s because the API tests spin up a host, and five attempts of that
    /// is five minutes spent on tests unrelated to any fix the agent made.
    /// </summary>
    public string? TestProject { get; set; } = "tests/Loop.Engine.Tests/Loop.Engine.Tests.csproj";

    /// <summary>Timeout for a single build or test invocation.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Run <c>dotnet format</c> over the generated files before building them.
    ///
    /// On by default, unlike the pipeline stages. Those are off until asked for because
    /// each one does something new and outward-facing; this changes nothing about what the
    /// engine produces, only whether the whitespace in it is right. PR #13 shipped with its
    /// trailing newline stripped and filler comments left behind — mechanical defects that
    /// cost a human review comment and that no model needs to be consulted about.
    ///
    /// Failure is never fatal: the formatter not running is not a reason to discard a fix
    /// that builds and passes.
    /// </summary>
    public bool FormatGeneratedCode { get; set; } = true;
}
