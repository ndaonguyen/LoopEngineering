using AwesomeAssertions;
using Loop.Engine.Core.Model;
using Loop.Engine.GitHub.Issues;
using Loop.Engine.Worker.Pipeline;
using Xunit;

namespace Loop.Engine.Tests;

/// <summary>
/// The branch name is the join key between an issue and its fix. These cover both halves of
/// it: reading a number back out of a branch, and refusing to start work that is already
/// open — the defect being that a scheduled run rebuilt the same fix every tick and failed at
/// push, spending a full pipeline to produce a diff that already existed.
/// </summary>
public class InFlightFixesTests
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

    // ---- the join key -----------------------------------------------------------------

    [Theory]
    [InlineData("fix/22-symbolextractor-never-extracts", 22)]
    [InlineData("fix/7-a", 7)]
    [InlineData("fix/1234-long-slug-here", 1234)]
    [InlineData("fix/22", 22)]                  // slug stripped; still unambiguous
    [InlineData("FIX/22-upper-prefix", 22)]     // git refs are case sensitive, humans are not
    public void Reads_the_issue_number_out_of_a_fix_branch(string branch, int expected)
    {
        GitHubInFlightFixes.IssueNumberFrom(branch, "fix/").Should().Be(expected);
    }

    [Theory]
    [InlineData("feat/22-a-feature")]           // another prefix entirely
    [InlineData("main")]
    [InlineData("fix/-no-number")]
    [InlineData("fix/abc-not-a-number")]
    [InlineData("fix/22abc-trailing-junk")]     // "22abc" is not a number
    [InlineData("")]
    [InlineData(null)]
    public void Ignores_branches_that_are_not_ours(string? branch)
    {
        GitHubInFlightFixes.IssueNumberFrom(branch, "fix/").Should().BeNull();
    }

    [Fact]
    public void Respects_a_configured_branch_prefix()
    {
        // Changing Publishing:BranchPrefix must move both halves together, or the engine
        // publishes under one name and looks for work under another.
        GitHubInFlightFixes.IssueNumberFrom("bugfix/9-thing", "bugfix/").Should().Be(9);
        GitHubInFlightFixes.IssueNumberFrom("fix/9-thing", "bugfix/").Should().BeNull();
    }

    // ---- selection --------------------------------------------------------------------

    [Fact]
    public void Skips_an_issue_whose_fix_is_already_open()
    {
        var issues = new[] { AnIssue(4, "bug"), AnIssue(8, "bug") };

        var selection = IssueSelector.Select(
            issues, requested: null, requiredLabel: "bug", inFlight: new HashSet<int> { 4 });

        // #4 is older and would normally win, but its fix is in flight.
        selection.Issue!.Number.Should().Be(8);
    }

    [Fact]
    public void Says_so_when_every_eligible_issue_is_already_in_flight()
    {
        var issues = new[] { AnIssue(4, "bug"), AnIssue(8, "bug") };

        var selection = IssueSelector.Select(
            issues, requested: null, requiredLabel: "bug", inFlight: new HashSet<int> { 4, 8 });

        selection.Issue.Should().BeNull();
        // Distinct from "nothing is labelled a bug" — that wants a label, this wants a review.
        selection.Rejection.Should().Contain("open fix pull request").And.Contain("merged or closed");
    }

    [Fact]
    public void An_explicit_request_still_wins()
    {
        // Naming a number is deliberate, and re-running against a known bug is how retrieval
        // gets checked. Publishing would fail on the existing branch, which is loud enough.
        var issues = new[] { AnIssue(4, "bug") };

        var selection = IssueSelector.Select(
            issues, requested: 4, requiredLabel: "bug", inFlight: new HashSet<int> { 4 });

        selection.Issue!.Number.Should().Be(4);
    }

    [Fact]
    public void Nothing_in_flight_behaves_exactly_as_before()
    {
        var issues = new[] { AnIssue(4, "bug"), AnIssue(8, "bug") };

        IssueSelector.Select(issues, null, "bug", inFlight: new HashSet<int>())
            .Issue!.Number.Should().Be(4);

        IssueSelector.Select(issues, null, "bug", inFlight: null)
            .Issue!.Number.Should().Be(4);
    }
}
