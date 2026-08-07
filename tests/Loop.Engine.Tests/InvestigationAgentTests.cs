using AwesomeAssertions;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Core.Model;
using Loop.Engine.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loop.Engine.Tests;

public class InvestigationAgentTests : IDisposable
{
    private readonly string _repoRoot = Directory.CreateTempSubdirectory("loop-engine-tests").FullName;

    private static Issue AnIssue() => new(
        Number: 7,
        Title: "NullReferenceException in IssuePollingService",
        Body: """
            Unhandled exception.
               at Loop.Engine.Worker.Pipeline.IssuePollingService.PollOnceAsync(CancellationToken token)
            """,
        Url: "https://github.com/owner/repo/issues/7",
        Labels: ["bug"],
        Assignee: null,
        CreatedAt: DateTimeOffset.UnixEpoch,
        UpdatedAt: DateTimeOffset.UnixEpoch);

    /// <summary>Lays down a file the retriever can actually find, so the agent has real input.</summary>
    private void GivenSourceFile(string relativePath, string contents)
    {
        var full = Path.Combine(_repoRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
    }

    private InvestigationAgent AgentReturning(string json, out FakeChatClient chat)
    {
        chat = new FakeChatClient(json);
        var retriever = new FileRetriever(
            Options.Create(new RepositoryOptions { RootPath = _repoRoot }),
            NullLogger<FileRetriever>.Instance);

        return new InvestigationAgent(
            chat,
            retriever,
            Options.Create(new InvestigationOptions { Model = "claude-sonnet-5", OutputDirectory = "out" }),
            NullLogger<InvestigationAgent>.Instance);
    }

    [Fact]
    public async Task InvestigateAsync_maps_the_model_response_onto_AnalysisResult()
    {
        GivenSourceFile("source/App/IssuePollingService.cs", "class IssuePollingService { }");

        var agent = AgentReturning("""
            {
              "symptoms": "Throws when the list is empty.",
              "possible_root_causes": ["FirstOrDefault returns null"],
              "affected_files": ["source/App/IssuePollingService.cs"],
              "confidence": 0.8,
              "recommended_investigation": "Add a zero-issue test."
            }
            """, out _);

        var result = await agent.InvestigateAsync(AnIssue());

        result.Symptoms.Should().Be("Throws when the list is empty.");
        result.PossibleRootCauses.Should().ContainSingle().Which.Should().Be("FirstOrDefault returns null");
        result.AffectedFiles.Should().ContainSingle().Which.Should().Be("source/App/IssuePollingService.cs");
        result.Confidence.Should().Be(0.8);
        result.Markdown.Should().Contain("## Symptoms");
    }

    [Fact]
    public async Task InvestigateAsync_discards_files_that_were_never_retrieved()
    {
        GivenSourceFile("source/App/IssuePollingService.cs", "class IssuePollingService { }");

        var agent = AgentReturning("""
            {
              "symptoms": "x",
              "possible_root_causes": [],
              "affected_files": ["source/App/IssuePollingService.cs", "source/App/Imaginary.cs"],
              "confidence": 0.5,
              "recommended_investigation": "y"
            }
            """, out _);

        var result = await agent.InvestigateAsync(AnIssue());

        // A hallucinated path would land on Phase 3's allow-list and send the Coder at a
        // file that does not exist.
        result.AffectedFiles.Should().NotContain("source/App/Imaginary.cs");
        result.AffectedFiles.Should().ContainSingle();
    }

    [Fact]
    public async Task InvestigateAsync_sends_the_no_code_generation_instruction()
    {
        GivenSourceFile("source/App/IssuePollingService.cs", "class IssuePollingService { }");

        var agent = AgentReturning("""
            {"symptoms":"x","possible_root_causes":[],"affected_files":[],"confidence":0.1,"recommended_investigation":"y"}
            """, out var chat);

        await agent.InvestigateAsync(AnIssue());

        chat.ReceivedMessages.Should().Contain(m => m.Text.Contains("NOT to fix"));
    }

    [Fact]
    public async Task InvestigateAsync_throws_when_the_model_returns_unparseable_output()
    {
        GivenSourceFile("source/App/IssuePollingService.cs", "class IssuePollingService { }");

        var agent = AgentReturning("I'm afraid I can't do that.", out _);

        // Returning an empty result instead would report success having investigated
        // nothing, and hand Phase 3 an empty allow-list.
        var act = () => agent.InvestigateAsync(AnIssue());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("no parseable investigation");
    }

    public void Dispose()
    {
        try { Directory.Delete(_repoRoot, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }
}
