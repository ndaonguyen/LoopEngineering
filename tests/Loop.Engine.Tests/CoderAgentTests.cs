using Loop.Engine.Agents.Providers;
using AwesomeAssertions;
using Loop.Engine.Agents.Coding;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Core.Model;
using Loop.Engine.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loop.Engine.Tests;

public class CoderAgentTests : IDisposable
{
    private readonly string _repoRoot = Directory.CreateTempSubdirectory("loop-engine-coder").FullName;

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
        PossibleRootCauses: [],
        AffectedFiles: files,
        RecommendedInvestigation: "");

    private static FixPlan APlan(params string[] files) =>
        new([new FixPlanStep(1, "Fix the resolution", files)]);

    private void GivenFile(string relativePath, string contents)
    {
        var full = Path.Combine(_repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
    }

    private CoderAgent AgentReturning(string json)
    {
        var retriever = new FileRetriever(
            Options.Create(new RepositoryOptions { RootPath = _repoRoot }),
            NullLogger<FileRetriever>.Instance);

        return new CoderAgent(
            new FakeChatClient(json),
            retriever,
            new DiffGenerator(NullLogger<DiffGenerator>.Instance),
            Options.Create(new AiOptions { Model = "claude-sonnet-5", OutputDirectory = "out" }),
            NullLogger<CoderAgent>.Instance);
    }

    [Fact]
    public async Task WriteCodeAsync_returns_the_edit_and_a_unified_diff()
    {
        GivenFile("src/A.cs", "class A { }\n");
        var agent = AgentReturning("""
            {"edits": [{"path": "src/A.cs", "contents": "class A { int x; }\n"}]}
            """);

        var changes = await agent.WriteCodeAsync(AnIssue(), AnAnalysis("src/A.cs"), APlan("src/A.cs"));

        changes.Edits.Should().ContainSingle().Which.RelativePath.Should().Be("src/A.cs");
        changes.UnifiedDiff.Should().Contain("+class A { int x; }");
        changes.HasChanges.Should().BeTrue();
    }

    [Fact]
    public async Task WriteCodeAsync_rejects_an_edit_outside_the_allow_list()
    {
        GivenFile("src/A.cs", "class A { }\n");
        GivenFile("src/Secret.cs", "class Secret { }\n");

        var agent = AgentReturning("""
            {"edits": [
              {"path": "src/A.cs", "contents": "class A { int x; }\n"},
              {"path": "src/Secret.cs", "contents": "class Secret { int pwned; }\n"}
            ]}
            """);

        var changes = await agent.WriteCodeAsync(AnIssue(), AnAnalysis("src/A.cs"), APlan("src/A.cs"));

        // Secret.cs exists and is readable — it is excluded because the investigation
        // never named it, which is the whole guarantee this phase rests on.
        changes.Edits.Should().ContainSingle().Which.RelativePath.Should().Be("src/A.cs");
        changes.UnifiedDiff.Should().NotContain("pwned");
    }

    [Fact]
    public async Task WriteCodeAsync_rejects_an_edit_that_no_longer_parses()
    {
        GivenFile("src/A.cs", "class A { }\n");
        var agent = AgentReturning("""
            {"edits": [{"path": "src/A.cs", "contents": "class A { void M() { "}]}
            """);

        var act = () => agent.WriteCodeAsync(AnIssue(), AnAnalysis("src/A.cs"), APlan("src/A.cs"));

        // Every edit was rejected, so there is nothing to hand on — better than emitting
        // a diff that Phase 4 will only discover is mangled.
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("rejected");
    }

    [Fact]
    public async Task WriteCodeAsync_throws_when_the_model_returns_prose()
    {
        GivenFile("src/A.cs", "class A { }\n");
        var agent = AgentReturning("I would change the path resolution.");

        var act = () => agent.WriteCodeAsync(AnIssue(), AnAnalysis("src/A.cs"), APlan("src/A.cs"));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("No usable edit was produced");
    }

    [Fact]
    public async Task WriteCodeAsync_still_produces_a_diff_when_one_file_fails()
    {
        GivenFile("src/A.cs", "class A { }\n");
        GivenFile("src/B.cs", "class B { }\n");

        // The fake replies the same thing to every call, so the request for B yields an
        // edit to A instead — which is dropped as off-target. One bad file must not sink
        // the run: per-file calls exist precisely so a failure stays local.
        var agent = AgentReturning("""
            {"edits": [{"path": "src/A.cs", "contents": "class A { int x; }\n"}]}
            """);

        var changes = await agent.WriteCodeAsync(
            AnIssue(),
            AnAnalysis("src/A.cs", "src/B.cs"),
            new FixPlan([new FixPlanStep(1, "Fix both", ["src/A.cs", "src/B.cs"])]));

        changes.Edits.Should().ContainSingle().Which.RelativePath.Should().Be("src/A.cs");
        changes.UnifiedDiff.Should().Contain("int x");
    }

    [Fact]
    public async Task WriteCodeAsync_throws_when_no_affected_file_can_be_read()
    {
        var agent = AgentReturning("""{"edits": []}""");

        var act = () => agent.WriteCodeAsync(AnIssue(), AnAnalysis("src/Missing.cs"), APlan("src/Missing.cs"));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("could be read");
    }

    public void Dispose()
    {
        try { Directory.Delete(_repoRoot, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }
}
