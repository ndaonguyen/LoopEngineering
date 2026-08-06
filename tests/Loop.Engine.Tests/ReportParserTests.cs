using AwesomeAssertions;
using Loop.Engine.Agents.Investigation;
using Xunit;

namespace Loop.Engine.Tests;

public class ReportParserTests
{
    private const string Valid = """
        {
          "symptoms": "Resolves against the working directory.",
          "possible_root_causes": ["Path.GetFullPath uses CWD"],
          "affected_files": ["source/App/FileRetriever.cs"],
          "confidence": 0.9,
          "recommended_investigation": "Run from two directories and compare."
        }
        """;

    [Fact]
    public void TryParse_reads_a_bare_json_object()
    {
        ReportParser.TryParse(Valid, out var report).Should().BeTrue();

        report.Symptoms.Should().Be("Resolves against the working directory.");
        report.AffectedFiles.Should().ContainSingle().Which.Should().Be("source/App/FileRetriever.cs");
        report.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void TryParse_reads_json_inside_a_markdown_fence()
    {
        // Models fence JSON constantly, however firmly the prompt says not to.
        var fenced = $"```json\n{Valid}\n```";

        ReportParser.TryParse(fenced, out var report).Should().BeTrue();
        report.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void TryParse_reads_json_wrapped_in_prose()
    {
        var wrapped = $"<analysis>\nHere is my report:\n\n{Valid}\n\nHope that helps.\n</analysis>";

        ReportParser.TryParse(wrapped, out var report).Should().BeTrue();
        report.AffectedFiles.Should().ContainSingle();
    }

    [Fact]
    public void TryParse_skips_a_leading_code_sample_and_finds_the_report()
    {
        // The first brace in a response often belongs to an example, not the report.
        var withSample = $"Consider this snippet:\n\n```csharp\nif (x) {{ return; }}\n```\n\n{Valid}";

        ReportParser.TryParse(withSample, out var report).Should().BeTrue();
        report.Symptoms.Should().Be("Resolves against the working directory.");
    }

    [Fact]
    public void TryParse_is_not_fooled_by_braces_inside_strings()
    {
        var tricky = """
            {
              "symptoms": "The template literal ${foo} breaks it",
              "possible_root_causes": [],
              "affected_files": [],
              "confidence": 0.1,
              "recommended_investigation": "n/a"
            }
            """;

        ReportParser.TryParse(tricky, out var report).Should().BeTrue();
        report.Symptoms.Should().Contain("${foo}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("I'm afraid I can't do that.")]
    [InlineData("## Summary\n\nThe defect is in FileRetriever.Retrieve.")]
    public void TryParse_fails_when_there_is_no_json_at_all(string text)
    {
        // Prose-only is exactly the failure that must stay loud: it is the model
        // ignoring the contract, not a formatting quirk worth papering over.
        ReportParser.TryParse(text, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_fails_on_null()
    {
        ReportParser.TryParse(null, out _).Should().BeFalse();
    }
}
