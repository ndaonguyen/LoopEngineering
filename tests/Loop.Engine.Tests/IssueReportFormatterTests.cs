using AwesomeAssertions;
using Loop.Engine.Core.Model;
using Loop.Engine.Worker.Pipeline;
using Xunit;

namespace Loop.Engine.Tests;

public class IssueReportFormatterTests
{
    private static Issue AnIssue(string? assignee = null) => new(
        Number: 132,
        Title: "NullReferenceException",
        Body: "Throws when the cart is empty.",
        Url: "https://github.com/owner/repo/issues/132",
        Labels: ["bug"],
        Assignee: assignee,
        CreatedAt: DateTimeOffset.UnixEpoch,
        UpdatedAt: DateTimeOffset.UnixEpoch);

    /// <summary>
    /// The formatter uses <see cref="Environment.NewLine"/>, so raw comparisons differ
    /// between Windows and the Linux CI runner. Compare on content, not on line endings.
    /// </summary>
    private static string Normalize(string text) => text.ReplaceLineEndings("\n");

    [Fact]
    public void Format_renders_the_phase_one_deliverable()
    {
        var report = IssueReportFormatter.Format(AnIssue());

        Normalize(report).Should().Be(Normalize(
            """
            Found Issue #132
            Title      NullReferenceException
            Assigned   No
            Ready for investigation
            """));
    }

    [Fact]
    public void Format_names_the_assignee_when_a_human_has_taken_it()
    {
        var report = IssueReportFormatter.Format(AnIssue(assignee: "ndaonguyen"));

        report.Should().Contain("Assigned   ndaonguyen");
    }

    [Fact]
    public void FormatAll_says_so_when_there_is_nothing_to_do()
    {
        IssueReportFormatter.FormatAll([]).Should().Be("No open issues.");
    }

    [Fact]
    public void FormatAll_separates_issues_with_a_blank_line()
    {
        var report = IssueReportFormatter.FormatAll([AnIssue(), AnIssue()]);

        Normalize(report).Should().Contain("Ready for investigation\n\nFound Issue #132");
    }
}
