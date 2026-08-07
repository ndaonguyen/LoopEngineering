using AwesomeAssertions;
using Loop.Engine.Agents.Coding;
using Xunit;

namespace Loop.Engine.Tests;

public class CodeReplyParserTests
{
    [Fact]
    public void TryParse_reads_the_path_and_the_fenced_code()
    {
        var reply = """
            FILE: source/App/A.cs
            ```csharp
            class A { int x; }
            ```
            """;

        CodeReplyParser.TryParse(reply, out var parsed).Should().BeTrue();
        parsed.Path.Should().Be("source/App/A.cs");
        parsed.Contents.Should().Be("class A { int x; }");
    }

    [Fact]
    public void TryParse_preserves_backslashes_verbatim()
    {
        // The case that broke a real run: this line inside a JSON string needs four
        // backslashes to survive, and one slip discards the whole file. In a fenced block
        // it needs none, so there is nothing to get wrong.
        var reply = """
            FILE: source/App/FileRetriever.cs
            ```csharp
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            ```
            """;

        CodeReplyParser.TryParse(reply, out var parsed).Should().BeTrue();
        parsed.Contents.Should().Contain(@"Replace('\\', '/')");
    }

    [Fact]
    public void TryParse_preserves_quotes_and_interpolation()
    {
        var reply = "FILE: A.cs\n```csharp\nvar s = $\"{a} \\\"quoted\\\" {b}\";\n```";

        CodeReplyParser.TryParse(reply, out var parsed).Should().BeTrue();
        parsed.Contents.Should().Contain("\\\"quoted\\\"");
    }

    [Fact]
    public void TryParse_preserves_multi_line_layout()
    {
        var reply = """
            FILE: A.cs
            ```csharp
            class A
            {
                void M() { }
            }
            ```
            """;

        CodeReplyParser.TryParse(reply, out var parsed).Should().BeTrue();
        parsed.Contents.Should().Contain("class A");
        parsed.Contents.Should().Contain("    void M() { }");
    }

    [Fact]
    public void TryParse_reads_the_hypothesis_from_a_repair_reply()
    {
        var reply = """
            HYPOTHESIS: The root was anchored to the working directory.
            FILE: A.cs
            ```csharp
            class A { }
            ```
            """;

        CodeReplyParser.TryParse(reply, out var parsed).Should().BeTrue();
        parsed.Hypothesis.Should().Be("The root was anchored to the working directory.");
    }

    [Fact]
    public void TryParse_tolerates_markdown_decoration_around_the_path()
    {
        // Models bold things. Not worth failing a run over.
        var reply = """
            **FILE:** `source/App/A.cs`
            ```csharp
            class A { }
            ```
            """;

        CodeReplyParser.TryParse(reply, out var parsed).Should().BeTrue();
        parsed.Path.Should().Be("source/App/A.cs");
    }

    [Fact]
    public void TryParse_normalises_windows_separators_in_the_path()
    {
        var reply = "FILE: source\\App\\A.cs\n```csharp\nclass A { }\n```";

        CodeReplyParser.TryParse(reply, out var parsed).Should().BeTrue();
        parsed.Path.Should().Be("source/App/A.cs");
    }

    [Theory]
    [InlineData("")]
    [InlineData("NO_CHANGE")]
    [InlineData("FILE: A.cs")]                       // path, no code
    [InlineData("```csharp\nclass A { }\n```")]      // code, no path
    [InlineData("I would change the path resolution.")]
    public void TryParse_fails_when_a_required_part_is_missing(string reply)
    {
        // NO_CHANGE included deliberately: "nothing to do here" must read as no edit
        // rather than as an empty file to write.
        CodeReplyParser.TryParse(reply, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_fails_on_an_empty_fenced_block()
    {
        CodeReplyParser.TryParse("FILE: A.cs\n```csharp\n\n```", out _).Should().BeFalse();
    }
}
