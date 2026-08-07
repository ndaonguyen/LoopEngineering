using Loop.Engine.Agents.Providers;
using AwesomeAssertions;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Verification;
using Loop.Engine.Core.Model;
using Loop.Engine.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loop.Engine.Tests;

public class FixVerifierTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("loop-engine-verify").FullName;

    private static Issue AnIssue() => new(
        Number: 8,
        Title: "RootPath resolves against the working directory",
        Body: "Relative paths land in the wrong tree.",
        Url: "https://github.com/owner/repo/issues/8",
        Labels: ["bug"],
        Assignee: null,
        CreatedAt: DateTimeOffset.UnixEpoch,
        UpdatedAt: DateTimeOffset.UnixEpoch);

    private static IReadOnlyList<CodeEdit> AnEdit() =>
        [new CodeEdit("src/A.cs", "class A { int x; }\n")];

    /// <summary>A repair reply that parses, with a fresh hypothesis each time.</summary>
    private static string RepairJson(string hypothesis) => $$"""
        {"hypothesis": "{{hypothesis}}", "edits": [{"path": "src/A.cs", "contents": "class A { int y; }\n"}]}
        """;

    private FixVerifier Verifier(FakeBuildRunner runner, string repairJson, int maxAttempts = 5) => new(
        new FakeChatClient(repairJson),
        runner,
        Options.Create(new VerificationOptions { MaxAttempts = maxAttempts, TestProject = null }),
        Options.Create(new AiOptions { Model = "claude-sonnet-5", OutputDirectory = "out" }),
        NullLogger<FixVerifier>.Instance);

    // A plain temp directory stands in for the worktree: these tests exercise the retry
    // loop, not git. FixWorktree has its own coverage.
    private static FakeFixWorkspace AWorktree() => new();

    [Fact]
    public async Task VerifyAsync_succeeds_immediately_when_the_build_is_green()
    {
        var runner = new FakeBuildRunner([BuildResult.Success()]);
        using var worktree = AWorktree();

        var result = await Verifier(runner, RepairJson("n/a")).VerifyAsync(AnIssue(), AnEdit(), worktree);

        result.Succeeded.Should().BeTrue();
        result.Attempts.Should().Be(1);
        result.Hypotheses.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyAsync_recovers_from_a_broken_build_within_the_cap()
    {
        // The issue's exit criterion: a deliberately broken build recovers on its own.
        var runner = FakeBuildRunner.FailsThenSucceeds(2);
        using var worktree = AWorktree();

        var result = await Verifier(runner, RepairJson("The brace was unbalanced")).VerifyAsync(
            AnIssue(), AnEdit(), worktree);

        result.Succeeded.Should().BeTrue();
        result.Attempts.Should().Be(3);
        runner.Invocations.Should().Be(3);
    }

    [Fact]
    public async Task VerifyAsync_stops_at_the_attempt_cap()
    {
        var runner = FakeBuildRunner.AlwaysFailing();
        using var worktree = AWorktree();

        var result = await Verifier(runner, RepairJson("Still wrong"), maxAttempts: 5).VerifyAsync(
            AnIssue(), AnEdit(), worktree);

        // Exactly five, not six. Raising the cap to get past a stubborn bug trades a
        // bounded failure for an unbounded one.
        result.Succeeded.Should().BeFalse();
        runner.Invocations.Should().Be(5);
    }

    [Fact]
    public async Task VerifyAsync_records_each_hypothesis_so_they_are_not_repeated()
    {
        var runner = FakeBuildRunner.AlwaysFailing();
        using var worktree = AWorktree();

        var result = await Verifier(runner, RepairJson("The anchor is wrong"), maxAttempts: 3).VerifyAsync(
            AnIssue(), AnEdit(), worktree);

        // Recorded so the next prompt can forbid restating them — the mechanism that stops
        // five attempts sharing one wrong diagnosis.
        result.Hypotheses.Should().NotBeEmpty();
        result.Hypotheses.Should().AllBe("The anchor is wrong");
    }

    [Fact]
    public async Task VerifyAsync_stops_early_when_the_repair_is_unparseable()
    {
        var runner = FakeBuildRunner.AlwaysFailing();
        using var worktree = AWorktree();

        var result = await Verifier(runner, "I think the problem is the path.").VerifyAsync(
            AnIssue(), AnEdit(), worktree);

        result.Succeeded.Should().BeFalse();
        // No point spending the remaining attempts once the model stops returning edits.
        runner.Invocations.Should().Be(1);
    }

    [Fact]
    public async Task StuckReport_names_the_diagnoses_and_the_last_failure()
    {
        var runner = FakeBuildRunner.AlwaysFailing("error CS1002: ; expected");
        using var worktree = AWorktree();

        var result = await Verifier(runner, RepairJson("Missing semicolon"), maxAttempts: 2).VerifyAsync(
            AnIssue(), AnEdit(), worktree);

        var report = result.StuckReport();

        report.Should().Contain("Gave up after");
        report.Should().Contain("Missing semicolon");
        report.Should().Contain("CS1002");
    }

    public void Dispose()
    {
        try { Directory.Delete(_repo, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }
}
