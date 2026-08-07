using AwesomeAssertions;
using Loop.Engine.Agents.Providers;
using Xunit;

namespace Loop.Engine.Tests;

public class ModelProvidersTests
{
    [Theory]
    [InlineData("claude-sonnet-5")]
    [InlineData("claude-opus-5")]
    [InlineData("claude-haiku-4-5")]
    [InlineData("CLAUDE-OPUS-5")]
    public void Resolve_recognises_anthropic_ids(string model)
    {
        ModelProviders.Resolve(model).Should().Be(ModelProvider.Anthropic);
    }

    [Theory]
    [InlineData("gpt-5")]
    [InlineData("gpt-4o")]
    [InlineData("o3-mini")]
    [InlineData("o4-mini")]
    [InlineData("chatgpt-4o-latest")]
    public void Resolve_recognises_openai_ids(string model)
    {
        ModelProviders.Resolve(model).Should().Be(ModelProvider.OpenAi);
    }

    [Fact]
    public void Resolve_trims_surrounding_whitespace()
    {
        // A stray space from a pasted config value must not change the provider.
        ModelProviders.Resolve("  claude-sonnet-5  ").Should().Be(ModelProvider.Anthropic);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("llama-3")]
    [InlineData("claud-sonnet-5")]  // typo — one character from valid
    public void Resolve_throws_on_an_id_it_cannot_place(string model)
    {
        // Defaulting to a provider would send an OpenAI key to Anthropic and surface as an
        // opaque 401 several layers from the typo. Fail at startup, naming the prefixes.
        var act = () => ModelProviders.Resolve(model);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*claude-*").And.Message.Should().Contain("gpt-");
    }

    [Fact]
    public void TryResolve_reports_failure_without_throwing()
    {
        ModelProviders.TryResolve("llama-3", out _).Should().BeFalse();
    }
}
