using AwesomeAssertions;
using Loop.Engine.Agents.Reproduction;
using Xunit;

namespace Loop.Engine.Tests;

public class SignatureTypesTests
{
    [Fact]
    public void Finds_the_parameter_type_a_test_would_have_to_construct()
    {
        // The real failure: SymbolExtractor.Extract takes an Issue, the model was never
        // shown Issue, and the reproduction died on CS7036.
        const string source = """
            namespace Loop.Engine.Agents.Retrieval;
            public static class SymbolExtractor
            {
                public static IReadOnlyList<string> Extract(Issue issue) => [];
            }
            """;

        SignatureTypes.Extract(source).Should().Contain("Issue");
    }

    [Fact]
    public void Unwraps_generic_arguments()
    {
        // The container is rarely the interesting type; the argument almost always is.
        const string source = """
            public class C
            {
                public IReadOnlyList<RetrievedFile> Read(List<CodeEdit> edits) => [];
            }
            """;

        var types = SignatureTypes.Extract(source);

        types.Should().Contain("RetrievedFile").And.Contain("CodeEdit");
        types.Should().NotContain("IReadOnlyList").And.NotContain("List");
    }

    [Fact]
    public void Finds_positional_record_parameters()
    {
        // The shape behind the CS7036: the parameter list hangs off the type declaration,
        // not off a constructor node, so walking constructors alone misses it entirely.
        const string source = "public sealed record ReviewReport(IReadOnlyList<ReviewFinding> Findings);";

        SignatureTypes.Extract(source).Should().Contain("ReviewFinding");
    }

    [Fact]
    public void Finds_constructor_parameters()
    {
        const string source = """
            public class Agent
            {
                public Agent(FileRetriever retriever, AiOptions options) { }
            }
            """;

        SignatureTypes.Extract(source).Should().Contain("FileRetriever").And.Contain("AiOptions");
    }

    [Fact]
    public void Finds_property_and_base_types()
    {
        const string source = """
            public sealed class FixWorktree : IFixWorkspace
            {
                public BuildResult Last { get; set; }
            }
            """;

        SignatureTypes.Extract(source).Should().Contain("IFixWorkspace").And.Contain("BuildResult");
    }

    [Fact]
    public void Leaves_out_types_the_framework_supplies()
    {
        // Retrieving these would spend the file budget on files that are not in the
        // repository, pushing out the one type the test actually needs.
        const string source = """
            public class C
            {
                public Task<string> M(CancellationToken token, DateTimeOffset when, int count) => null!;
            }
            """;

        SignatureTypes.Extract(source).Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not C# at all {{{")]
    public void Returns_empty_rather_than_throwing_on_junk(string source) =>
        SignatureTypes.Extract(source).Should().BeEmpty();
}
