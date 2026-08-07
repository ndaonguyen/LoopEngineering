using AwesomeAssertions;
using Loop.Engine.Agents.Verification;
using Xunit;

namespace Loop.Engine.Tests;

public class BuildOutputParserFileTests
{
    // Verbatim from the run where the repair loop stalled with the right answer in hand.
    private const string RealRippleFailure = """
        C:\Temp\wt-eb12\tests\Loop.Engine.Tests\InvestigationAgentTests.cs(41,29): error CS7036: There is no argument given that corresponds to the required parameter 'contentRoot' [C:\Temp\wt-eb12\tests\Loop.Engine.Tests\Loop.Engine.Tests.csproj]
        C:\Temp\wt-eb12\tests\Loop.Engine.Tests\CoderAgentTests.cs(50,29): error CS7036: There is no argument given that corresponds to the required parameter 'contentRoot' [C:\Temp\wt-eb12\tests\Loop.Engine.Tests\Loop.Engine.Tests.csproj]
        """;

    [Fact]
    public void ParseErrorFiles_returns_paths_relative_to_the_build_directory()
    {
        var files = BuildOutputParser.ParseErrorFiles(RealRippleFailure, @"C:\Temp\wt-eb12");

        files.Should().BeEquivalentTo([
            "tests/Loop.Engine.Tests/InvestigationAgentTests.cs",
            "tests/Loop.Engine.Tests/CoderAgentTests.cs",
        ]);
    }

    [Fact]
    public void ParseErrorFiles_deduplicates_repeated_diagnostics()
    {
        var repeated = RealRippleFailure + "\n" + RealRippleFailure;

        BuildOutputParser.ParseErrorFiles(repeated, @"C:\Temp\wt-eb12").Should().HaveCount(2);
    }

    [Fact]
    public void ParseErrorFiles_ignores_files_outside_the_build_directory()
    {
        var external = """
            C:\Program Files\dotnet\sdk\Microsoft.Common.targets(5096,5): error MSB3021: Unable to copy file
            """;

        // SDK and package files are not ours to edit; letting one in would hand the model
        // a target it cannot legitimately change.
        BuildOutputParser.ParseErrorFiles(external, @"C:\Temp\wt-eb12").Should().BeEmpty();
    }

    [Fact]
    public void ParseErrorFiles_ignores_warnings()
    {
        var warning = """
            C:\Temp\wt-eb12\src\A.cs(1,1): warning NU1902: Package has a known vulnerability
            """;

        BuildOutputParser.ParseErrorFiles(warning, @"C:\Temp\wt-eb12").Should().BeEmpty();
    }

    [Fact]
    public void ParseErrorFiles_handles_empty_input()
    {
        BuildOutputParser.ParseErrorFiles("", @"C:\Temp\wt").Should().BeEmpty();
        BuildOutputParser.ParseErrorFiles("some output", "").Should().BeEmpty();
    }
}
