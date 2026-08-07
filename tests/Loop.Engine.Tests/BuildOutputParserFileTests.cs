using AwesomeAssertions;
using Loop.Engine.Agents.Verification;
using Xunit;

namespace Loop.Engine.Tests;

public class BuildOutputParserFileTests
{
    /// <summary>
    /// A build directory built for the running platform. Hard-coded <c>C:\…</c> fixtures
    /// pass on Windows and fail on the Linux CI runner, because there they are not
    /// absolute paths at all — the third platform assumption to bite this project, after
    /// two line-ending ones.
    /// </summary>
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "loop-engine-wt-fixture"));

    private static string Local(string relative) =>
        Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>An MSBuild error line for a file inside the build directory.</summary>
    private static string ErrorLine(string relative) =>
        $"{Local(relative)}(41,29): error CS7036: There is no argument given that corresponds " +
        $"to the required parameter 'contentRoot' [{Local("tests/Loop.Engine.Tests/Loop.Engine.Tests.csproj")}]";

    // The shape from the run where the repair loop stalled with the right answer in hand.
    private static string RippleFailure =>
        ErrorLine("tests/Loop.Engine.Tests/InvestigationAgentTests.cs") + "\n" +
        ErrorLine("tests/Loop.Engine.Tests/CoderAgentTests.cs");

    [Fact]
    public void ParseErrorFiles_returns_paths_relative_to_the_build_directory()
    {
        var files = BuildOutputParser.ParseErrorFiles(RippleFailure, Root);

        files.Should().BeEquivalentTo([
            "tests/Loop.Engine.Tests/InvestigationAgentTests.cs",
            "tests/Loop.Engine.Tests/CoderAgentTests.cs",
        ]);
    }

    [Fact]
    public void ParseErrorFiles_deduplicates_repeated_diagnostics()
    {
        var repeated = RippleFailure + "\n" + RippleFailure;

        BuildOutputParser.ParseErrorFiles(repeated, Root).Should().HaveCount(2);
    }

    [Fact]
    public void ParseErrorFiles_ignores_files_outside_the_build_directory()
    {
        var outside = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "somewhere-else", "Other.cs"));
        var external = $"{outside}(5,1): error MSB3021: Unable to copy file";

        // SDK and package files are not ours to edit; letting one in would hand the model
        // a target it cannot legitimately change.
        BuildOutputParser.ParseErrorFiles(external, Root).Should().BeEmpty();
    }

    [Fact]
    public void ParseErrorFiles_ignores_warnings()
    {
        var warning = $"{Local("src/A.cs")}(1,1): warning NU1902: Package has a known vulnerability";

        BuildOutputParser.ParseErrorFiles(warning, Root).Should().BeEmpty();
    }

    [Fact]
    public void ParseErrorFiles_handles_empty_input()
    {
        BuildOutputParser.ParseErrorFiles("", Root).Should().BeEmpty();
        BuildOutputParser.ParseErrorFiles("some output", "").Should().BeEmpty();
    }
}
