using Loop.Engine.Agents.Providers;
using AwesomeAssertions;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Review;
using Loop.Engine.Core.Model;
using Loop.Engine.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loop.Engine.Tests;

public class ReviewerAgentTests
{
    private const string ADiff = """
        diff --git a/src/A.cs b/src/A.cs
        --- a/src/A.cs
        +++ b/src/A.cs
        @@ -1 +1 @@
        -class A { }
        +class A { public string Password = "hunter2"; }
        """;

    private static Issue AnIssue() => new(
        Number: 8,
        Title: "RootPath resolves against the working directory",
        Body: "…",
        Url: "https://github.com/owner/repo/issues/8",
        Labels: ["bug"],
        Assignee: null,
        CreatedAt: DateTimeOffset.UnixEpoch,
        UpdatedAt: DateTimeOffset.UnixEpoch);

    private static ReviewerAgent AgentReturning(string json) => new(
        new FakeChatClient(json),
        Options.Create(new AiOptions { Model = "claude-sonnet-5", OutputDirectory = "out" }),
        NullLogger<ReviewerAgent>.Instance);

    [Fact]
    public async Task ReviewAsync_returns_findings()
    {
        var agent = AgentReturning("""
            {"findings": [
              {"category": "security", "severity": "high", "detail": "Hard-coded credential."}
            ]}
            """);

        var report = await agent.ReviewAsync(AnIssue(), ADiff);

        report.Findings.Should().ContainSingle();
        report.Findings[0].Category.Should().Be("security");
        report.Findings[0].Severity.Should().Be("high");
    }

    [Fact]
    public async Task ReviewAsync_accepts_an_empty_findings_list()
    {
        var report = await AgentReturning("""{"findings": []}""").ReviewAsync(AnIssue(), ADiff);

        // A small correct diff is allowed to have nothing wrong with it. Manufacturing a
        // finding to look thorough is worse than saying nothing.
        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task ReviewAsync_returns_empty_rather_than_throwing_on_unparseable_output()
    {
        var report = await AgentReturning("The diff looks fine to me.").ReviewAsync(AnIssue(), ADiff);

        // The review is advisory. A failed review must not sink a fix that already builds
        // and passes its tests.
        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task ReviewAsync_skips_the_model_call_for_an_empty_diff()
    {
        var chat = new FakeChatClient("""{"findings": [{"category":"x","severity":"low","detail":"y"}]}""");
        var agent = new ReviewerAgent(
            chat,
            Options.Create(new AiOptions { Model = "claude-sonnet-5", OutputDirectory = "out" }),
            NullLogger<ReviewerAgent>.Instance);

        var report = await agent.ReviewAsync(AnIssue(), "");

        report.Findings.Should().BeEmpty();
        chat.ReceivedMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task ReviewAsync_drops_findings_with_no_detail()
    {
        var agent = AgentReturning("""
            {"findings": [
              {"category": "naming", "severity": "low", "detail": ""},
              {"category": "security", "severity": "high", "detail": "Real problem."}
            ]}
            """);

        var report = await agent.ReviewAsync(AnIssue(), ADiff);

        report.Findings.Should().ContainSingle().Which.Detail.Should().Be("Real problem.");
    }

    [Fact]
    public void Reviewer_prompt_forbids_editing_and_praise()
    {
        ReviewerPrompt.System.Should().Contain("you do not edit");
        ReviewerPrompt.System.Should().Contain("skip praise");
    }
}
