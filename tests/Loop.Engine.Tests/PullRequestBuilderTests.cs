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

    private static ReproductionTest AReproductionTest() => new(
        "tests/Loop.Engine.Tests/RootPathTests.cs",
        "namespace T; public class RootPathTests { [Fact] public void It() { } }",
        "Loop.Engine.Tests.RootPathTests.It_resolves_against_the_app");

    [Fact]
    public void Build_drops_the_disclaimer_when_a_test_went_red_then_green()
    {
        var reproduction = ReproductionResult.Red(AReproductionTest(), "fails today");

        var body = PullRequestBuilder.Build(
            AnIssue(), AnAnalysis(), AVerification(), ReviewReport.Empty, "fix/", reproduction).RenderBody();

        body.Should().NotContain("No test reproduces");
        body.Should().Contain("A test reproduces the original defect");
        body.Should().Contain("Loop.Engine.Tests.RootPathTests.It_resolves_against_the_app");
    }

    [Fact]
    public void Build_keeps_the_disclaimer_when_the_gate_rejected_the_test()
    {
        // "The suite is green" has never meant "the bug is gone". The claim is only
        // dropped against an observed red-to-green transition.
        var rejected = ReproductionResult.Rejected(
            ReproductionOutcome.AlreadyPasses, AReproductionTest(), "passes already");

        var body = PullRequestBuilder.Build(
            AnIssue(), AnAnalysis(), AVerification(), ReviewReport.Empty, "fix/", rejected).RenderBody();

        body.Should().Contain("No test reproduces the original defect");
    }

    [Fact]
    public void Build_keeps_the_disclaimer_when_no_reproduction_was_attempted()
    {
        Build().RenderBody().Should().Contain("No test reproduces the original defect");
    }

    [Fact]
    public void Build_lists_the_reproduction_test_among_the_changed_files()
    {
        var reproduction = ReproductionResult.Red(AReproductionTest(), "fails today");

        var context = PullRequestBuilder.Build(
            AnIssue(), AnAnalysis(), AVerification(), ReviewReport.Empty, "fix/", reproduction);

        // It is a real file in the branch but never passes through verification.Edits, so
        // without this the pull request under-reports what it changes.
        context.ChangedFiles.Should().Contain("tests/Loop.Engine.Tests/RootPathTests.cs");
    }

    [Fact]
    public void Build_puts_a_high_severity_warning_above_the_summary()
    {
        var review = new ReviewReport([new ReviewFinding("security", "high", "Hard-coded credential.")]);

        var body = Build(review: review).RenderBody();

        // Position is the whole point. The findings already appeared at the bottom, under
        // Reviewer Notes — by which point the diff has been read and a view formed. A
        // warning that arrives after the decision is decoration.
        body.IndexOf("[!WARNING]", StringComparison.Ordinal)
            .Should().BeLessThan(body.IndexOf("## Summary", StringComparison.Ordinal));
        body.Should().Contain("Read these before the diff");
    }

    [Fact]
    public void Build_leaves_a_clean_review_unbannered()
    {
        var body = Build().RenderBody();

        // A banner on every pull request is a banner nobody reads.
        body.Should().NotContain("[!WARNING]");
        Build().NeedsHuman.Should().BeFalse();
    }

    [Fact]
    public void Build_flags_for_a_human_only_at_high_severity()
    {
        var medium = new ReviewReport([new ReviewFinding("style", "medium", "Naming is inconsistent.")]);
        var high = new ReviewReport([new ReviewFinding("correctness", "high", "Off-by-one.")]);

        Build(review: medium).NeedsHuman.Should().BeFalse();
        Build(review: high).NeedsHuman.Should().BeTrue();
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
