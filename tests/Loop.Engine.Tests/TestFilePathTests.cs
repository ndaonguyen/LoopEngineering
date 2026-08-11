using AwesomeAssertions;
using Loop.Engine.Agents.Reproduction;
using Xunit;

namespace Loop.Engine.Tests;

public class TestFilePathTests
{
    private const string Project = "tests/Loop.Engine.Tests/Loop.Engine.Tests.csproj";

    [Fact]
    public void A_path_no_project_compiles_is_moved_into_the_suite()
    {
        // The real failure: the model derived the directory from the namespace of the code
        // under test. Nothing compiled the file, the filter matched nothing, dotnet test
        // exited zero, and the gate concluded the test "already passes".
        TestFilePath.InsideProject("tests/Loop.Engine.Agents.Retrieval/SymbolExtractorTests.cs", Project)
            .Should().Be("tests/Loop.Engine.Tests/SymbolExtractorTests.cs");
    }

    [Fact]
    public void A_path_already_inside_the_suite_is_left_alone()
    {
        TestFilePath.InsideProject("tests/Loop.Engine.Tests/SymbolExtractorTests.cs", Project)
            .Should().Be("tests/Loop.Engine.Tests/SymbolExtractorTests.cs");
    }

    [Fact]
    public void A_nested_folder_inside_the_suite_is_preserved()
    {
        // Projects organise tests into folders; relocating those would be officious.
        TestFilePath.InsideProject("tests/Loop.Engine.Tests/Retrieval/SymbolExtractorTests.cs", Project)
            .Should().Be("tests/Loop.Engine.Tests/Retrieval/SymbolExtractorTests.cs");
    }

    [Fact]
    public void Backslashes_are_normalised()
    {
        TestFilePath.InsideProject(@"src\Whatever\SymbolExtractorTests.cs", Project)
            .Should().Be("tests/Loop.Engine.Tests/SymbolExtractorTests.cs");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Without_a_configured_project_the_path_is_untouched(string? project)
    {
        // Nothing is known about where tests live, so inventing a location would be worse
        // than leaving the model's choice alone.
        TestFilePath.InsideProject("some/where/Tests.cs", project)
            .Should().Be("some/where/Tests.cs");
    }
}
