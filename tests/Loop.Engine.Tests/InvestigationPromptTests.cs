using AwesomeAssertions;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Core.Model;
using Xunit;

namespace Loop.Engine.Tests;

public class InvestigationPromptTests
{
    private static Issue AnIssue() => new(
        Number: 7,
        Title: "NullReferenceException in the poller",
        Body: "Throws when the issue list is empty.",
        Url: "https://github.com/owner/repo/issues/7",
        Labels: ["bug"],
        Assignee: null,
        CreatedAt: DateTimeOffset.UnixEpoch,
        UpdatedAt: DateTimeOffset.UnixEpoch);

    private static RetrievedFile AFile() => new(
        RelativePath: "source/Loop.Engine.Worker/Pipeline/IssuePollingService.cs",
        Excerpt: "public sealed class IssuePollingService { }",
        MatchedSymbols: ["IssuePollingService"]);

    [Fact]
    public void System_prompt_forbids_code_generation()
    {
        // The whole premise of Phase 2. A constraint nothing checks quietly disappears.
        InvestigationPrompt.System.Should().Contain("NOT to fix");
        InvestigationPrompt.System.Should().Contain("Do not write, propose, or sketch code");
    }

    [Fact]
    public void System_prompt_forbids_inventing_file_paths()
    {
        InvestigationPrompt.System.Should().Contain("Do not invent paths");
    }

    [Fact]
    public void User_message_carries_the_issue_and_the_excerpts()
    {
        var prompt = InvestigationPrompt.BuildUserMessage(AnIssue(), [AFile()]);

        prompt.Should().Contain("Issue #7");
        prompt.Should().Contain("Throws when the issue list is empty.");
        prompt.Should().Contain("source/Loop.Engine.Worker/Pipeline/IssuePollingService.cs");
        prompt.Should().Contain("public sealed class IssuePollingService");
    }

    [Fact]
    public void User_message_says_why_each_file_was_retrieved()
    {
        var prompt = InvestigationPrompt.BuildUserMessage(AnIssue(), [AFile()]);

        prompt.Should().Contain("Matched: IssuePollingService");
    }

    [Fact]
    public void User_message_states_plainly_when_retrieval_found_nothing()
    {
        var prompt = InvestigationPrompt.BuildUserMessage(AnIssue(), []);

        // Silence invites the model to invent plausible-looking paths.
        prompt.Should().Contain("No candidate files were retrieved");
        prompt.Should().Contain("low confidence");
    }
}
