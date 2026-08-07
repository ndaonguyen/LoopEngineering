using AwesomeAssertions;
using Loop.Engine.Agents.Coding;
using Xunit;

namespace Loop.Engine.Tests;

public class SyntaxVerifierTests
{
    [Fact]
    public void Verify_accepts_valid_csharp()
    {
        SyntaxVerifier.Verify("A.cs", "class A { void M() { int x = 1; } }", out var errors)
            .Should().BeTrue();

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Verify_rejects_a_missing_brace()
    {
        SyntaxVerifier.Verify("A.cs", "class A { void M() { int x = 1; ", out var errors)
            .Should().BeFalse();

        errors.Should().NotBeEmpty();
        errors[0].Should().Contain("CS1513");
    }

    [Fact]
    public void Verify_rejects_content_that_is_not_csharp_at_all()
    {
        SyntaxVerifier.Verify("A.cs", "this is not C# at all !!!", out _).Should().BeFalse();
    }

    [Fact]
    public void Verify_accepts_a_call_to_an_undefined_method()
    {
        // Documents the limit rather than the capability: this PARSES cleanly and would
        // fail `dotnet build`. Anyone reading a green verifier as "it compiles" is wrong,
        // and this test is where they find that out.
        SyntaxVerifier.Verify("A.cs", "class A { void M() { Undefined(); } }", out var errors)
            .Should().BeTrue();

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Verify_ignores_non_csharp_files()
    {
        SyntaxVerifier.Verify("appsettings.json", "{ not valid c# }", out _).Should().BeTrue();
    }
}
