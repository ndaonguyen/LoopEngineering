using AwesomeAssertions;
using Loop.Engine.Core.Model;
using Loop.Engine.Worker.Pipeline;
using Xunit;

namespace Loop.Engine.Tests;

public class IssueSelectorTests
{
    private static Issue AnIssue(int number, params string[] labels) => new(
        Number: number,
        Title: $"Issue {number}",
        Body: string.Empty,
        Url: $"https://github.com/owner/repo/issues/{number}",
        Labels: labels,
        Assignee: null,
        CreatedAt: DateTimeOffset.UnixEpoch,
        UpdatedAt: DateTimeOffset.UnixEpoch);

    [Fact]
    public void Select_takes_the_oldest_bug()
    {
        var issues = new[] { AnIssue(9, "bug"), AnIssue(4, "bug"), AnIssue(7, "bug") };

        var selection = IssueSelector.Select(issues, requested: null, requiredLabel: "bug");

        // Oldest first: the bug that has been open longest has waited longest.
        selection.Issue!.Number.Should().Be(4);
        selection.Rejection.Should().BeNull();
    }

    [Fact]
    public void Select_skips_issues_that_are_not_bugs()
    {
        // The defect this fixes: #1 is a feature request and older than the real bug, so
        // the engine picked it and would have opened a "fix" for a dark mode toggle.
        var issues = new[] { AnIssue(1, "enhancement"), AnIssue(8, "bug") };

        var selection = IssueSelector.Select(issues, requested: null, requiredLabel: "bug");

        selection.Issue!.Number.Should().Be(8);
    }

    [Fact]
    public void Select_reports_when_nothing_is_labelled_a_bug()
    {
        var issues = new[] { AnIssue(1, "enhancement"), AnIssue(2) };

        var selection = IssueSelector.Select(issues, requested: null, requiredLabel: "bug");

        selection.Issue.Should().BeNull();
        // Saying so matters: a tick that quietly does nothing looks exactly like a tick
        // that found nothing wrong.
        selection.Rejection.Should().Contain("bug").And.Contain("2 open");
    }

    [Fact]
    public void Select_matches_the_label_case_insensitively()
    {
        var issues = new[] { AnIssue(3, "Bug") };

        var selection = IssueSelector.Select(issues, requested: null, requiredLabel: "bug");

        selection.Issue!.Number.Should().Be(3);
    }

    [Fact]
    public void Select_honours_an_explicit_request()
    {
        var issues = new[] { AnIssue(4, "bug"), AnIssue(8, "bug") };

        var selection = IssueSelector.Select(issues, requested: 8, requiredLabel: "bug");

        selection.Issue!.Number.Should().Be(8);
    }

    [Fact]
    public void Select_refuses_a_requested_issue_that_is_not_open()
    {
        var issues = new[] { AnIssue(4, "bug") };

        var selection = IssueSelector.Select(issues, requested: 99, requiredLabel: "bug");

        selection.Issue.Should().BeNull();
        selection.Rejection.Should().Contain("99");
    }

    [Fact]
    public void Select_refuses_a_requested_issue_that_is_not_a_bug()
    {
        // Naming an issue says which one, not that it is a bug. Pointed at a feature
        // request the pipeline still produces a plausible, confident, wrong fix.
        var issues = new[] { AnIssue(1, "enhancement") };

        var selection = IssueSelector.Select(issues, requested: 1, requiredLabel: "bug");

        selection.Issue.Should().BeNull();
        selection.Rejection.Should().Contain("not labelled").And.Contain("Pipeline:RequiredLabel");
    }

    [Fact]
    public void Select_ignores_labels_entirely_when_no_label_is_required()
    {
        // The escape hatch, for a repository that does not label bugs.
        var issues = new[] { AnIssue(1, "enhancement") };

        var selection = IssueSelector.Select(issues, requested: null, requiredLabel: "");

        selection.Issue!.Number.Should().Be(1);
    }

    [Fact]
    public void The_console_report_agrees_with_the_selector()
    {
        // Printing "Ready for investigation" under an issue the selector will refuse
        // describes a pipeline that does not exist. Both ask IssueSelector.IsEligible, so
        // they cannot drift apart.
        var bug = AnIssue(8, "bug");
        var feature = AnIssue(1, "enhancement");

        IssueReportFormatter.Format(bug, "bug").Should().Contain("Ready for investigation");
        IssueReportFormatter.Format(feature, "bug").Should().Contain("Skipped");
        IssueSelector.IsEligible(feature, "bug").Should().BeFalse();
    }

    [Fact]
    public void Select_reports_an_empty_backlog()
    {
        var selection = IssueSelector.Select([], requested: null, requiredLabel: "bug");

        selection.Issue.Should().BeNull();
        selection.Rejection.Should().NotBeNullOrWhiteSpace();
    }
}
