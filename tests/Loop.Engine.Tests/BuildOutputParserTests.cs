using AwesomeAssertions;
using Loop.Engine.Agents.Verification;
using Xunit;

namespace Loop.Engine.Tests;

public class BuildOutputParserTests
{
    // Verbatim from a real `dotnet build` in this repository, not an invented shape.
    private const string RealCompilerError = """
        C:\MINE\LoopEngineering1\tests\Loop.Engine.Tests\SymbolExtractorTests.cs(46,9): error CS1929: 'IReadOnlyList<string>' does not contain a definition for 'IndexOf' [C:\MINE\LoopEngineering1\tests\Loop.Engine.Tests\Loop.Engine.Tests.csproj]
        C:\MINE\LoopEngineering1\tests\Loop.Engine.Tests\SymbolExtractorTests.cs(46,9): error CS1929: 'IReadOnlyList<string>' does not contain a definition for 'IndexOf' [C:\MINE\LoopEngineering1\tests\Loop.Engine.Tests\Loop.Engine.Tests.csproj]
        """;

    // Also verbatim — these advisories exist in this repo and no agent fix can remove them.
    private const string RealWarnings = """
        C:\MINE\LoopEngineering1\source\LoopEngineering.Api\LoopEngineering.Api.csproj : warning NU1902: Package 'OpenTelemetry.Exporter.OpenTelemetryProtocol' 1.12.0 has a known moderate severity vulnerability
        C:\MINE\LoopEngineering1\tests\LoopEngineering.Api.Tests\LoopEngineering.Api.Tests.csproj : warning NU1903: Package 'Microsoft.OpenApi' 2.0.0 has a known high severity vulnerability
        Build succeeded.
            12 Warning(s)
            0 Error(s)
        """;

    private const string RealTestFailure = """
        [xUnit.net 00:00:00.53]     Loop.Engine.Tests.CoderAgentTests.WriteCodeAsync_throws_when_the_model_returns_prose [FAIL]
        Failed!  - Failed:     1, Passed:    54, Skipped:     0, Total:    55, Duration: 410 ms
        """;

    [Fact]
    public void ParseErrors_extracts_the_code_message_and_location()
    {
        var errors = BuildOutputParser.ParseErrors(RealCompilerError);

        errors.Should().ContainSingle();
        errors[0].Should().StartWith("CS1929:");
        errors[0].Should().Contain("does not contain a definition for 'IndexOf'");
        errors[0].Should().Contain("SymbolExtractorTests.cs(46,9)");
    }

    [Fact]
    public void ParseErrors_deduplicates_msbuilds_repeated_diagnostics()
    {
        // MSBuild prints each diagnostic once per target. The model does not need it twice.
        BuildOutputParser.ParseErrors(RealCompilerError).Should().HaveCount(1);
    }

    [Fact]
    public void ParseErrors_ignores_warnings_entirely()
    {
        // The load-bearing test of this phase. If warnings ever became a failure signal,
        // this repo's pre-existing NU1902/NU1903 advisories would exhaust all five retry
        // attempts on every run, against something no fix can address.
        BuildOutputParser.ParseErrors(RealWarnings).Should().BeEmpty();
    }

    [Fact]
    public void Succeeded_is_true_for_a_build_that_only_emitted_warnings()
    {
        BuildOutputParser.Succeeded(RealWarnings, exitCode: 0).Should().BeTrue();
    }

    [Fact]
    public void ParseErrors_extracts_failing_test_names()
    {
        var errors = BuildOutputParser.ParseErrors(RealTestFailure);

        errors.Should().ContainSingle();
        errors[0].Should().Contain("WriteCodeAsync_throws_when_the_model_returns_prose");
    }

    [Fact]
    public void Succeeded_is_false_when_tests_failed_even_on_exit_code_zero()
    {
        BuildOutputParser.Succeeded(RealTestFailure, exitCode: 0).Should().BeFalse();
    }

    [Fact]
    public void Succeeded_is_false_on_a_non_zero_exit_code()
    {
        BuildOutputParser.Succeeded("Build succeeded.", exitCode: 1).Should().BeFalse();
    }

    [Fact]
    public void ParseErrors_handles_empty_output()
    {
        BuildOutputParser.ParseErrors("").Should().BeEmpty();
    }
}
