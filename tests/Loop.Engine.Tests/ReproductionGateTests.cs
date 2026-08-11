using AwesomeAssertions;
using Loop.Engine.Agents.Reproduction;
using Loop.Engine.Agents.Verification;
using Loop.Engine.Core.Model;
using Loop.Engine.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loop.Engine.Tests;

public class ReproductionGateTests
{
    private static ReproductionTest ATest() => new(
        "tests/Loop.Engine.Tests/RootPathTests.cs",
        "namespace T; public class RootPathTests { [Fact] public void It_resolves() { } }",
        "T.RootPathTests.It_resolves");

    private static ReproductionGate Gate(FakeBuildRunner runner) => new(
        runner,
        Options.Create(new VerificationOptions { TestProject = "tests/Some.Tests/Some.Tests.csproj" }),
        NullLogger<ReproductionGate>.Instance);

    [Fact]
    public async Task A_test_that_fails_against_the_unfixed_code_is_the_only_way_through()
    {
        var runner = new FakeBuildRunner([BuildResult.Success()])
        {
            FilteredResult = BuildResult.Failure(["Assert.Equal() Failure"], "  Failed T.RootPathTests.It_resolves"),
        };
        using var workspace = new FakeFixWorkspace();

        var result = await Gate(runner).RunAsync(ATest(), workspace);

        result.IsRed.Should().BeTrue();
        result.Outcome.Should().Be(ReproductionOutcome.Red);
    }

    [Fact]
    public async Task A_test_that_already_passes_is_rejected()
    {
        // The most dangerous outcome. It would pass after the fix too, and the pull request
        // would carry a proof of nothing.
        var runner = new FakeBuildRunner([BuildResult.Success()])
        {
            FilteredResult = BuildResult.Success(
                "Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1"),
        };
        using var workspace = new FakeFixWorkspace();

        var result = await Gate(runner).RunAsync(ATest(), workspace);

        result.IsRed.Should().BeFalse();
        result.Outcome.Should().Be(ReproductionOutcome.AlreadyPasses);
        result.Detail.Should().Contain("prove nothing");
    }

    [Fact]
    public async Task A_test_that_does_not_compile_is_rejected()
    {
        var runner = new FakeBuildRunner([BuildResult.Failure(["error CS1002: ; expected"])]);
        using var workspace = new FakeFixWorkspace();

        var result = await Gate(runner).RunAsync(ATest(), workspace);

        result.Outcome.Should().Be(ReproductionOutcome.DoesNotCompile);
        result.Detail.Should().Contain("CS1002");
    }

    [Fact]
    public async Task A_filter_that_matches_nothing_is_not_a_reproduction()
    {
        // dotnet test exits non-zero for "no test matches" exactly as it does for a failing
        // test. Without this branch a mistyped name reads as a successful reproduction, and
        // the fix would be "proved" by a test that never ran.
        var runner = new FakeBuildRunner([BuildResult.Success()])
        {
            FilteredResult = BuildResult.Failure([], "No test matches the given testcase filter"),
        };
        using var workspace = new FakeFixWorkspace();

        var result = await Gate(runner).RunAsync(ATest(), workspace);

        result.IsRed.Should().BeFalse();
        result.Outcome.Should().Be(ReproductionOutcome.TestNotFound);
    }

    [Fact]
    public async Task A_run_where_nothing_executed_is_not_a_pass()
    {
        // The real failure this caught: the generated test landed outside the test project,
        // so nothing compiled it, the filter matched nothing, and `dotnet test` exited zero
        // WITHOUT printing "No test matches". The gate reported AlreadyPasses — a confident
        // diagnosis of a test that had never run. The count is the fact; the wording was a
        // hope.
        var runner = new FakeBuildRunner([BuildResult.Success()])
        {
            FilteredResult = BuildResult.Success(
                "Passed!  - Failed:     0, Passed:     0, Skipped:     0, Total:     0"),
        };
        using var workspace = new FakeFixWorkspace();

        var result = await Gate(runner).RunAsync(ATest(), workspace);

        result.Outcome.Should().Be(ReproductionOutcome.TestNotFound);
    }

    [Fact]
    public async Task A_genuine_pass_of_one_test_is_still_AlreadyPasses()
    {
        // Guard against over-correcting: a real single-test pass must not be misread as
        // "nothing ran", or the gate would never reject the dangerous case again.
        var runner = new FakeBuildRunner([BuildResult.Success()])
        {
            FilteredResult = BuildResult.Success(
                "Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1"),
        };
        using var workspace = new FakeFixWorkspace();

        var result = await Gate(runner).RunAsync(ATest(), workspace);

        result.Outcome.Should().Be(ReproductionOutcome.AlreadyPasses);
    }

    [Fact]
    public async Task No_test_at_all_is_rejected()
    {
        var runner = new FakeBuildRunner([BuildResult.Success()]);
        using var workspace = new FakeFixWorkspace();

        var result = await Gate(runner).RunAsync(null, workspace);

        result.Outcome.Should().Be(ReproductionOutcome.NotProduced);
    }

    [Fact]
    public async Task The_test_runs_alone_under_an_exact_filter()
    {
        var runner = new FakeBuildRunner([BuildResult.Success()])
        {
            FilteredResult = BuildResult.Failure(["failed"], "Failed"),
        };
        using var workspace = new FakeFixWorkspace();

        await Gate(runner).RunAsync(ATest(), workspace);

        // An unfiltered run cannot tell this test failing from any other test failing, and
        // "something is red" is not evidence the bug is reproduced.
        runner.LastTestFilter.Should().Be("T.RootPathTests.It_resolves");
    }

    [Fact]
    public async Task The_test_is_written_into_the_workspace_before_it_is_run()
    {
        var runner = new FakeBuildRunner([BuildResult.Success()])
        {
            FilteredResult = BuildResult.Failure(["failed"], "Failed"),
        };
        using var workspace = new FakeFixWorkspace();

        await Gate(runner).RunAsync(ATest(), workspace);

        // It has to land in the tree that gets compiled — a test held only in memory
        // cannot fail against anything.
        workspace.Applied.Should().ContainSingle()
            .Which.Should().ContainSingle(e => e.RelativePath == ATest().RelativePath);
    }
}
