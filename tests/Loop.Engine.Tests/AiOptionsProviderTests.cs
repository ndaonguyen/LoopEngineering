using AwesomeAssertions;
using Loop.Engine.Agents.Providers;
using Xunit;

namespace Loop.Engine.Tests;

public class AiOptionsProviderTests
{
    [Fact]
    public void RequiredProviders_returns_one_entry_when_both_models_share_a_provider()
    {
        var options = new AiOptions { Model = "claude-sonnet-5", ReasoningModel = "claude-opus-5" };

        options.RequiredProviders().Should().ContainSingle().Which.Should().Be(ModelProvider.Anthropic);
    }

    [Fact]
    public void RequiredProviders_returns_both_when_the_models_are_mixed()
    {
        // Mixing is supported on purpose: the reasoning stage and the workhorse stage have
        // different demands, and nothing says they have to come from the same vendor.
        var options = new AiOptions { Model = "gpt-5", ReasoningModel = "claude-opus-5" };

        options.RequiredProviders().Should().BeEquivalentTo(
            [ModelProvider.OpenAi, ModelProvider.Anthropic]);
    }

    [Fact]
    public void ResolveKey_prefers_the_configured_value()
    {
        var options = new AiOptions { AnthropicApiKey = "sk-ant-configured" };

        options.ResolveKey(ModelProvider.Anthropic).Should().Be("sk-ant-configured");
    }

    [Fact]
    public void ResolveKey_falls_back_to_the_environment_variable()
    {
        var original = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-from-environment");

        try
        {
            // The SDK reads the environment itself, so validating only the config value
            // would pass while the credential actually in use is missing.
            new AiOptions().ResolveKey(ModelProvider.OpenAi).Should().Be("sk-from-environment");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", original);
        }
    }

    [Fact]
    public void ResolveKey_returns_null_when_neither_source_has_a_value()
    {
        var original = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        try
        {
            new AiOptions().ResolveKey(ModelProvider.OpenAi).Should().BeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", original);
        }
    }

    [Fact]
    public void ResolveKey_trims_the_configured_value()
    {
        var options = new AiOptions { AnthropicApiKey = "  sk-ant-padded  " };

        // A trailing newline from a paste otherwise reaches the API inside the header.
        options.ResolveKey(ModelProvider.Anthropic).Should().Be("sk-ant-padded");
    }
}
