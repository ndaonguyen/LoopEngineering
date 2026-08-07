using Loop.Engine.Agents.Providers;
using AwesomeAssertions;
using Loop.Engine.Agents.Investigation;
using Xunit;

namespace Loop.Engine.Tests;

public class AiOptionsTests
{
    [Fact]
    public void EffectiveReasoningModel_uses_the_reasoning_model_when_configured()
    {
        var options = new AiOptions
        {
            Model = "claude-sonnet-5",
            ReasoningModel = "claude-opus-5",
        };

        options.EffectiveReasoningModel.Should().Be("claude-opus-5");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EffectiveReasoningModel_falls_back_to_the_workhorse_model(string reasoningModel)
    {
        var options = new AiOptions
        {
            Model = "claude-sonnet-5",
            ReasoningModel = reasoningModel,
        };

        // Leaving it unset must mean "one model everywhere", not "no model" — otherwise
        // deleting a line from appsettings.json breaks the Investigation agent only.
        options.EffectiveReasoningModel.Should().Be("claude-sonnet-5");
    }

    [Fact]
    public void EffectiveReasoningModel_trims_whitespace()
    {
        var options = new AiOptions
        {
            Model = "claude-sonnet-5",
            ReasoningModel = "  claude-opus-5  ",
        };

        // A stray space from a pasted config value would otherwise reach the API as part
        // of the model id and fail as an unhelpful 404.
        options.EffectiveReasoningModel.Should().Be("claude-opus-5");
    }
}
