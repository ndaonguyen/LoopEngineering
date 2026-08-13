using AwesomeAssertions;
using Loop.Engine.Agents.Verification;
using Xunit;

namespace Loop.Engine.Tests;

/// <summary>
/// The guard exists for one scenario: the engine pointed at another repository with
/// <c>Verification:TestProject</c> left on its own suite. That run cannot fail honestly — it
/// passes tests unrelated to the fix — so these assertions are about refusing to start.
/// </summary>
public class TestProjectGuardTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("loop-engine-guard").FullName;

    private string GivenTestProject(string relativePath)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "<Project />");
        return relativePath;
    }

    [Fact]
    public void Accepts_a_test_project_that_exists_in_the_target_repository()
    {
        var project = GivenTestProject(Path.Combine("tests", "MyProject.Tests", "MyProject.Tests.csproj"));

        TestProjectGuard.Check(verifyFix: true, _root, project).Should().BeNull();
    }

    [Fact]
    public void Refuses_a_test_project_that_is_not_in_the_target_repository()
    {
        // The real mistake: RootPath moved to another repo, TestProject left on the engine's own.
        var reason = TestProjectGuard.Check(
            verifyFix: true, _root, "tests/Loop.Engine.Tests/Loop.Engine.Tests.csproj");

        reason.Should().NotBeNull();
    }

    [Fact]
    public void Says_what_it_looked_for_and_what_to_do()
    {
        // A refusal nobody can act on just moves the confusion earlier.
        var reason = TestProjectGuard.Check(
            verifyFix: true, _root, "tests/Loop.Engine.Tests/Loop.Engine.Tests.csproj");

        reason.Should().Contain("Verification:TestProject")
            .And.Contain("Repository:RootPath")
            .And.Contain(_root);
    }

    [Fact]
    public void Stays_out_of_the_way_when_verification_is_off()
    {
        // Investigating another repository without setting TestProject is a legitimate run:
        // nothing is built, so nothing can be falsely verified.
        TestProjectGuard.Check(verifyFix: false, _root, "tests/Whatever/Whatever.csproj")
            .Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Treats_an_empty_test_project_as_run_the_whole_solution(string? testProject)
    {
        // The escape hatch for a target with no single test project. Deliberate, not a mistake.
        TestProjectGuard.Check(verifyFix: true, _root, testProject).Should().BeNull();
    }

    [Fact]
    public void Accepts_a_relative_root_that_resolves_to_the_repository()
    {
        // FixWorktree resolves RootPath with Path.GetFullPath, against the working directory.
        // The guard must agree with it, or it would reject configurations that actually work.
        var project = GivenTestProject(Path.Combine("tests", "T", "T.csproj"));
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(_root);

            TestProjectGuard.Check(verifyFix: true, ".", project).Should().BeNull();
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }
}
