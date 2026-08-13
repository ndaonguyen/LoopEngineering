using AwesomeAssertions;
using Loop.Engine.Agents.Reproduction;
using Xunit;

namespace Loop.Engine.Tests;

/// <summary>
/// Covers the collision a real run hit: the model named its class after the thing under test,
/// the target repository already had a class by that name, and <c>CS0101</c> stopped a run
/// that had otherwise reasoned about the bug correctly.
/// </summary>
public class TestTypeNameTests
{
    private const string Source = """
        using Xunit;

        namespace AiPMOInsight.Application.Tests.HealthScoring;

        public class HealthScoringServiceTests
        {
            [Fact]
            public void Score_is_order_independent()
            {
                // mentions HealthScoringServiceTests in a comment
                var note = "HealthScoringServiceTests";
            }
        }
        """;

    [Fact]
    public void Renames_the_test_class_to_something_the_repository_cannot_already_have()
    {
        var rewritten = TestTypeName.Rewrite(Source, issueNumber: 1);

        rewritten.Should().Contain("class Issue1ReproductionTests");
        rewritten.Should().NotContain("class HealthScoringServiceTests");
    }

    [Fact]
    public void Leaves_every_other_mention_of_the_old_name_alone()
    {
        // Rewriting the token rather than the text. A textual replace would edit the comment
        // and the string literal too, which is how a "rename" quietly changes behaviour.
        var rewritten = TestTypeName.Rewrite(Source, issueNumber: 1);

        rewritten.Should().Contain("// mentions HealthScoringServiceTests in a comment");
        rewritten.Should().Contain("\"HealthScoringServiceTests\"");
    }

    [Fact]
    public void The_name_stays_readable_by_the_identity_reader()
    {
        // The two run back to back in the agent: rename, then read the filter name off the
        // result. If they disagreed, dotnet test would filter on a class that does not exist.
        var rewritten = TestTypeName.Rewrite(Source, issueNumber: 42);

        TestIdentity.TryReadFullyQualifiedName(rewritten)
            .Should().Be("AiPMOInsight.Application.Tests.HealthScoring.Issue42ReproductionTests.Score_is_order_independent");
    }

    [Fact]
    public void Is_stable_for_the_same_issue()
    {
        // A re-run overwrites its own previous attempt instead of leaving dead files behind.
        TestTypeName.For(7).Should().Be(TestTypeName.For(7));
        TestTypeName.For(7).Should().NotBe(TestTypeName.For(8));
    }

    [Fact]
    public void Rewriting_an_already_correct_name_changes_nothing()
    {
        var already = Source.Replace("HealthScoringServiceTests", "Issue1ReproductionTests");

        TestTypeName.Rewrite(already, issueNumber: 1).Should().Be(already);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("public class NoTestsHere { public void Method() { } }")]
    public void Leaves_source_with_no_test_method_untouched(string source)
    {
        // An absent [Fact] is a rejection the gate already reports. Inventing a class here
        // would hide it behind a test that never asserts anything.
        TestTypeName.Rewrite(source, issueNumber: 1).Should().Be(source);
    }
}
