using AwesomeAssertions;
using Loop.Engine.Agents.Reproduction;
using Xunit;

namespace Loop.Engine.Tests;

public class TestIdentityTests
{
    [Fact]
    public void Reads_the_name_from_a_file_scoped_namespace()
    {
        const string source = """
            namespace Loop.Engine.Tests;

            public class RootPathTests
            {
                [Fact]
                public void Resolves_against_the_application_root() { }
            }
            """;

        TestIdentity.TryReadFullyQualifiedName(source)
            .Should().Be("Loop.Engine.Tests.RootPathTests.Resolves_against_the_application_root");
    }

    [Fact]
    public void Reads_the_name_from_a_block_namespace()
    {
        const string source = """
            namespace Loop.Engine.Tests
            {
                public class RootPathTests
                {
                    [Fact]
                    public void Resolves() { }
                }
            }
            """;

        TestIdentity.TryReadFullyQualifiedName(source)
            .Should().Be("Loop.Engine.Tests.RootPathTests.Resolves");
    }

    [Fact]
    public void Finds_a_Theory_as_well_as_a_Fact()
    {
        const string source = """
            namespace T;
            public class C
            {
                [Theory]
                [InlineData(1)]
                public void M(int x) { }
            }
            """;

        TestIdentity.TryReadFullyQualifiedName(source).Should().Be("T.C.M");
    }

    [Fact]
    public void Tolerates_the_fully_qualified_attribute_form()
    {
        const string source = """
            namespace T;
            public class C
            {
                [Xunit.Fact]
                public void M() { }
            }
            """;

        TestIdentity.TryReadFullyQualifiedName(source).Should().Be("T.C.M");
    }

    [Fact]
    public void Skips_helpers_and_takes_the_test_method()
    {
        const string source = """
            namespace T;
            public class C
            {
                private static int Helper() => 1;

                [Fact]
                public void The_actual_test() { }
            }
            """;

        TestIdentity.TryReadFullyQualifiedName(source).Should().Be("T.C.The_actual_test");
    }

    [Fact]
    public void Returns_null_when_there_is_no_test_method()
    {
        // Not a detail — a file with no test is a rejection. Without a name there is
        // nothing to filter on, and an unfiltered run proves nothing about this bug.
        const string source = """
            namespace T;
            public class C
            {
                public void NotATest() { }
            }
            """;

        TestIdentity.TryReadFullyQualifiedName(source).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this is not C# at all {{{")]
    public void Returns_null_rather_than_throwing_on_junk(string source) =>
        TestIdentity.TryReadFullyQualifiedName(source).Should().BeNull();
}
