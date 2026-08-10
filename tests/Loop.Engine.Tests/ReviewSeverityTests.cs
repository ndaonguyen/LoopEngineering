using AwesomeAssertions;
using Loop.Engine.Core.Model;
using Xunit;

namespace Loop.Engine.Tests;

public class ReviewSeverityTests
{
    private static ReviewFinding A(string severity) => new("correctness", severity, "detail");

    [Theory]
    [InlineData("high", ReviewSeverity.High)]
    [InlineData("HIGH", ReviewSeverity.High)]
    [InlineData("  High  ", ReviewSeverity.High)]
    [InlineData("medium", ReviewSeverity.Medium)]
    [InlineData("low", ReviewSeverity.Low)]
    public void ParsedSeverity_reads_the_vocabulary_the_prompt_asks_for(
        string raw, ReviewSeverity expected) =>
        A(raw).ParsedSeverity.Should().Be(expected);

    [Theory]
    [InlineData("critical")]
    [InlineData("blocker")]
    [InlineData("")]
    public void ParsedSeverity_does_not_guess_at_words_outside_the_vocabulary(string raw)
    {
        // The prompt asks for "low | medium | high" and a model is free to ignore it.
        // Guessing that "critical" outranks "high" would be reasonable and still wrong:
        // the moment an unrecognised word can trip the label, every PR gets labelled and
        // the label stops meaning anything.
        A(raw).ParsedSeverity.Should().Be(ReviewSeverity.None);
    }

    [Fact]
    public void HighestSeverity_is_None_for_a_clean_review() =>
        ReviewReport.Empty.HighestSeverity.Should().Be(ReviewSeverity.None);

    [Fact]
    public void HighestSeverity_takes_the_worst_finding()
    {
        var review = new ReviewReport([A("low"), A("high"), A("medium")]);

        review.HighestSeverity.Should().Be(ReviewSeverity.High);
        review.NeedsHuman.Should().BeTrue();
    }

    [Fact]
    public void NeedsHuman_stays_false_below_high()
    {
        // Medium findings are worth reading, not worth flagging. A label on everything is
        // a label on nothing.
        var review = new ReviewReport([A("low"), A("medium")]);

        review.NeedsHuman.Should().BeFalse();
    }
}
