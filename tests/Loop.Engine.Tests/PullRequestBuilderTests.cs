using AwesomeAssertions;
using Loop.Engine.Core.Model;
using Loop.Engine.GitHub.Publishing;
using Xunit;

namespace Loop.Engine.Tests;

public class PullRequestBuilderTests
{
    private static Issue AnIssue() => new(
        Number: 8,
        Title: "Repository:RootPath is resolved against the working directory, not the app",
        Body: "Relative paths land in the wrong tree.",
        Url: "https://github.com/owner/repo/issues/8",
        Labels: ["bug"],
        Assignee: null,
        CreatedAt: DateTimeOffset.UnixEpoch,
        UpdatedAt: DateTimeOffset.UnixEpoch);

    private static AnalysisResult AnAnalysis() => new(
        Symptoms: "The configured root resolves differently depending on the launch directory.",
        PossibleRootCauses: ["Path.GetFullPath anchors to Environment.CurrentDirectory"],
        AffectedFiles: ["source/App/FileRetriever.cs"],
        RecommendedInvestigation: "Log the resolved root.");

    private static VerificationResult AVerification(int attempts = 1, params string[] hypotheses) => new(
        Succeeded: true,
        Attempts: attempts,
        Edits: [new CodeEdit("source/App/FileRetriever.cs", "class FileRetriever { }")],
        Hypotheses: hypotheses,
        LastFailure: null);

    private static PullRequestContext Build(
        VerificationResult? verification = null, ReviewReport? review = null) =>
        PullRequestBuilder.Build(
            AnIssue(), AnAnalysis(), verification ?? AVerification(), review ?? ReviewReport.Empty, "fix/");

    [Fact]
    public void Build_titles_the_pr_with_a_conventional_commits_prefix()
    {
        // pr-lint.yml fails the PR without it — a title that cannot pass CI is not a PR.
        Build().Title.Should().StartWith("fix: ");
    }

    [Fact]
    public void Build_names_the_branch_after_the_issue()
    {
        Build().BranchName.Should().StartWith("fix/8-repository-rootpath-is-resolved");
    }

    [Fact]
    public void Build_body_closes_the_issue_on_merge()
    {
        // The pipeline never closes an issue by hand; merging the PR does it.
        Build().RenderBody().Should().Contain("Closes #8");
    }

    [Fact]
    public void Build_body_contains_every_template_section()
    {
        var body = Build().RenderBody();

        foreach (var section in new[]
                 { "## Summary", "## Root Cause", "## Changes", "## Testing", "## Risk", "## Reviewer Notes" })
        {
            body.Should().Contain(section);
        }
    }

    [Fact]
    public void Build_testing_notes_admit_that_no_test_reproduces_the_bug()
    {
        // The honest caveat. A green build proves the change breaks nothing; it does not
        // prove the reported defect is fixed, and a reviewer must not infer otherwise.
        Build().RenderBody().Should().Contain("No test reproduces the original defect");
    }

    [Fact]
    public void Build_lists_the_changed_files()
    {
        Build().RenderBody().Should().Contain("source/App/FileRetriever.cs");
    }

    [Fact]
    public void Build_includes_the_repair_history_when_there_was_one()
    {
        var verification = AVerification(3, "The anchor was wrong", "The call site needed updating");

        var body = Build(verification).RenderBody();

        body.Should().Contain("Diagnoses tried");
        body.Should().Contain("The anchor was wrong");
        body.Should().Contain("Took 3 attempts");
    }

    [Fact]
    public void Build_surfaces_high_severity_findings_in_the_risk_section()
    {
        var review = new ReviewReport([new ReviewFinding("security", "high", "Hard-coded credential.")]);

        var body = Build(review: review).RenderBody();

        body.Should().Contain("1 high-severity finding");
        body.Should().Contain("Hard-coded credential.");
    }

    [Fact]
    public void Build_says_so_plainly_when_the_review_found_nothing()
    {
        var body = Build().RenderBody();

        body.Should().Contain("No high-severity findings");
        body.Should().Contain("_No findings._");
    }

    [Theory]
    [InlineData("Fix the Widget!", "fix-the-widget")]
    [InlineData("  Trailing  spaces  ", "trailing-spaces")]
    [InlineData("Repository:RootPath / FileRetriever", "repository-rootpath-fileretriever")]
    public void Slug_matches_the_bug_loop_skills_rule(string title, string expected)
    {
        // Same rule as scripts/bug-loop/pick-work.sh so branches from both loops look alike.
        PullRequestBuilder.Slug(title).Should().Be(expected);
    }

    [Fact]
    public void Slug_caps_length_without_leaving_a_trailing_dash()
    {
        var slug = PullRequestBuilder.Slug(new string('a', 30) + " " + new string('b', 40));

        slug.Length.Should().BeLessThanOrEqualTo(50);
        slug.Should().NotEndWith("-");
    }
}
