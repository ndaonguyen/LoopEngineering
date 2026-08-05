using AwesomeAssertions;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Core.Model;
using Xunit;

namespace Loop.Engine.Tests;

public class SymbolExtractorTests
{
    private static Issue IssueWith(string title, string body) => new(
        Number: 42,
        Title: title,
        Body: body,
        Url: "https://github.com/owner/repo/issues/42",
        Labels: ["bug"],
        Assignee: null,
        CreatedAt: DateTimeOffset.UnixEpoch,
        UpdatedAt: DateTimeOffset.UnixEpoch);

    [Fact]
    public void Extract_pulls_type_and_method_from_a_stack_frame()
    {
        var issue = IssueWith("NullReferenceException on poll", """
            Unhandled exception. System.NullReferenceException:
               at Loop.Engine.Worker.Pipeline.IssuePollingService.PollOnceAsync(CancellationToken token)
            """);

        var symbols = SymbolExtractor.Extract(issue);

        symbols.Should().Contain("IssuePollingService");
        symbols.Should().Contain("PollOnceAsync");
    }

    [Fact]
    public void Extract_ranks_stack_frame_symbols_ahead_of_prose()
    {
        var issue = IssueWith("Crash in the Widget Repository", """
            The GitHubIssueSource seems fine but it still breaks.
               at Loop.Engine.Agents.Investigation.InvestigationAgent.InvestigateAsync(Issue issue)
            """);

        var symbols = SymbolExtractor.Extract(issue);

        // A stack trace names the failure path; a capitalised word in prose merely might.
        var ranked = symbols.ToList();
        ranked.Should().StartWith("InvestigationAgent");
        ranked.IndexOf("InvestigationAgent").Should().BeLessThan(ranked.IndexOf("GitHubIssueSource"));
    }

    [Fact]
    public void Extract_finds_referenced_source_filenames()
    {
        var issue = IssueWith("Bad mapping", "The bug is somewhere in GitHubIssueSource.cs I think.");

        SymbolExtractor.Extract(issue).Should().Contain("GitHubIssueSource");
    }

    [Fact]
    public void Extract_deduplicates_repeated_symbols()
    {
        var issue = IssueWith("FileRetriever FileRetriever", "FileRetriever again, and FileRetriever.cs");

        SymbolExtractor.Extract(issue).Count(s => s == "FileRetriever").Should().Be(1);
    }

    [Fact]
    public void Extract_ignores_tokens_too_short_to_be_symbols()
    {
        var issue = IssueWith("Ab Cd", "Ab Cd Ef — none of these are real symbols.");

        SymbolExtractor.Extract(issue).Should().BeEmpty();
    }

    [Fact]
    public void Extract_returns_nothing_for_an_issue_with_no_detail()
    {
        var issue = IssueWith("it broke", "please fix");

        SymbolExtractor.Extract(issue).Should().BeEmpty();
    }
}
