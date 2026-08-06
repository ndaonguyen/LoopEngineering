using AwesomeAssertions;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Core.Model;
using Xunit;

namespace Loop.Engine.Tests;

public class InvestigationMarkdownTests
{
    /// <summary>Compare on content, not line endings — CI is Linux, dev boxes are Windows.</summary>
    private static string Normalize(string text) => text.ReplaceLineEndings("\n");

    private static Issue AnIssue() => new(
        Number: 7,
        Title: "NullReferenceException in the poller",
        Body: "Throws when the issue list is empty.",
        Url: "https://github.com/owner/repo/issues/7",
        Labels: ["bug"],
        Assignee: null,
        CreatedAt: DateTimeOffset.UnixEpoch,
        UpdatedAt: DateTimeOffset.UnixEpoch);

    private static AnalysisResult AnAnalysis() => new(
        Symptoms: "Throws on an empty list.",
        PossibleRootCauses: ["FirstOrDefault returns null and is dereferenced"],
        AffectedFiles: ["source/Loop.Engine.Worker/Pipeline/IssuePollingService.cs"],
        RecommendedInvestigation: "Add a test with zero open issues.")
    {
        Confidence = 0.82,
    };

    [Fact]
    public void Render_includes_every_section_the_issue_requires()
    {
        var md = Normalize(InvestigationMarkdown.Render(AnIssue(), AnAnalysis()));

        md.Should().Contain("## Symptoms");
        md.Should().Contain("## Possible root causes");
        md.Should().Contain("## Affected files");
        md.Should().Contain("## Recommended investigation");
        md.Should().Contain("## Confidence");
    }

    [Fact]
    public void Render_names_the_issue_and_links_to_it()
    {
        var md = Normalize(InvestigationMarkdown.Render(AnIssue(), AnAnalysis()));

        md.Should().StartWith("# Investigation — #7: NullReferenceException in the poller");
        md.Should().Contain("https://github.com/owner/repo/issues/7");
    }

    [Fact]
    public void Render_marks_confidence_as_uncalibrated()
    {
        var md = Normalize(InvestigationMarkdown.Render(AnIssue(), AnAnalysis()));

        // It is a self-report. The document must not let a reader mistake it for a gate.
        md.Should().Contain("uncalibrated");
        md.Should().Contain("nothing downstream branches on this number");
    }

    [Fact]
    public void Render_states_that_no_code_was_changed()
    {
        var md = Normalize(InvestigationMarkdown.Render(AnIssue(), AnAnalysis()));

        md.Should().Contain("Analysis only. No code was changed");
    }

    [Fact]
    public void Render_says_so_when_no_files_were_identified()
    {
        var empty = AnAnalysis() with { AffectedFiles = [] };

        var md = Normalize(InvestigationMarkdown.Render(AnIssue(), empty));

        md.Should().Contain("None identified");
        md.Should().Contain("not contain enough detail");
    }
}
