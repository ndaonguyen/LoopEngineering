using AwesomeAssertions;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Planning;
using Loop.Engine.Core.Model;
using Loop.Engine.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loop.Engine.Tests;

public class PlannerAgentTests
{
    private static Issue AnIssue() => new(
        Number: 8,
        Title: "RootPath resolves against the working directory",
        Body: "Relative paths land in the wrong tree.",
        Url: "https://github.com/owner/repo/issues/8",
        Labels: ["bug"],
        Assignee: null,
        CreatedAt: DateTimeOffset.UnixEpoch,
        UpdatedAt: DateTimeOffset.UnixEpoch);

    private static AnalysisResult AnAnalysis(params string[] files) => new(
        Symptoms: "Resolves against CWD.",
        PossibleRootCauses: ["Path.GetFullPath uses the working directory"],
        AffectedFiles: files.Length > 0 ? files : ["src/FileRetriever.cs"],
        RecommendedInvestigation: "Log the resolved root.");

    private static PlannerAgent AgentReturning(string json) => new(
        new FakeChatClient(json),
        Options.Create(new InvestigationOptions { Model = "claude-sonnet-5", OutputDirectory = "out" }),
        NullLogger<PlannerAgent>.Instance);

    [Fact]
    public async Task PlanAsync_returns_ordered_steps()
    {
        var agent = AgentReturning("""
            {"steps": [
              {"description": "Resolve against the content root", "files": ["src/FileRetriever.cs"]},
              {"description": "Reject relative paths at startup", "files": ["src/FileRetriever.cs"]}
            ]}
            """);

        var plan = await agent.PlanAsync(AnIssue(), AnAnalysis());

        plan.Steps.Should().HaveCount(2);
        plan.Steps[0].Order.Should().Be(1);
        plan.Steps[1].Order.Should().Be(2);
        plan.Steps[0].Description.Should().Be("Resolve against the content root");
    }

    [Fact]
    public async Task PlanAsync_drops_files_outside_the_investigations_findings()
    {
        var agent = AgentReturning("""
            {"steps": [
              {"description": "Fix it", "files": ["src/FileRetriever.cs", "src/Unrelated.cs"]}
            ]}
            """);

        var plan = await agent.PlanAsync(AnIssue(), AnAnalysis());

        // A step naming a file the investigation never found is the plan leaking scope.
        plan.Steps.Should().ContainSingle();
        plan.Steps[0].Files.Should().ContainSingle().Which.Should().Be("src/FileRetriever.cs");
    }

    [Fact]
    public async Task PlanAsync_throws_when_no_step_cites_an_allowed_file()
    {
        var agent = AgentReturning("""
            {"steps": [{"description": "Rewrite everything", "files": ["src/Elsewhere.cs"]}]}
            """);

        var act = () => agent.PlanAsync(AnIssue(), AnAnalysis());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("no usable steps");
    }

    [Fact]
    public async Task PlanAsync_throws_when_the_investigation_found_no_files()
    {
        var agent = AgentReturning("""{"steps": []}""");

        var act = () => agent.PlanAsync(AnIssue(), AnAnalysis() with { AffectedFiles = [] });

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("identified no files");
    }

    [Fact]
    public async Task PlanAsync_throws_when_the_model_returns_prose()
    {
        var agent = AgentReturning("Here is my plan:\n\n1. Fix the bug.");

        var act = () => agent.PlanAsync(AnIssue(), AnAnalysis());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("no parseable plan");
    }
}
