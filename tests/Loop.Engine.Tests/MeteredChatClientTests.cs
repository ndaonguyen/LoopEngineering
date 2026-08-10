using AwesomeAssertions;
using Loop.Engine.Agents.Telemetry;
using Loop.Engine.Core.Model;
using Loop.Engine.Tests.Fakes;
using Microsoft.Extensions.AI;
using Xunit;

namespace Loop.Engine.Tests;

public class MeteredChatClientTests
{
    private static ChatMessage[] APrompt() => [new(ChatRole.User, "why")];

    [Fact]
    public async Task Every_call_is_recorded_without_the_agent_doing_anything()
    {
        // The whole point of wrapping the client: an agent that forgot to report itself
        // would be invisible exactly where the numbers matter, and its missing tokens would
        // look like an efficient stage rather than an unmeasured one.
        var inner = new FakeChatClient("ok") { Usage = new UsageDetails { InputTokenCount = 120, OutputTokenCount = 45 } };
        var metrics = new RunMetrics();
        using var client = new MeteredChatClient(inner, metrics);

        using (metrics.BeginStage("Investigation"))
        {
            await client.GetResponseAsync(APrompt());
        }

        var stage = metrics.Snapshot().Single();

        stage.Stage.Should().Be("Investigation");
        stage.InputTokens.Should().Be(120);
        stage.OutputTokens.Should().Be(45);
    }

    [Fact]
    public async Task A_provider_that_omits_usage_still_registers_a_call()
    {
        var inner = new FakeChatClient("ok");
        var metrics = new RunMetrics();
        using var client = new MeteredChatClient(inner, metrics);

        using (metrics.BeginStage("Coding"))
        {
            await client.GetResponseAsync(APrompt());
        }

        var stage = metrics.Snapshot().Single();

        stage.ModelCalls.Should().Be(1);
        stage.TotalTokens.Should().Be(0);
    }

    [Fact]
    public async Task The_response_passes_through_untouched()
    {
        var inner = new FakeChatClient("the answer");
        using var client = new MeteredChatClient(inner, new RunMetrics());

        var response = await client.GetResponseAsync(APrompt());

        // Metering must not become a place where responses can be altered or lost.
        response.Text.Should().Be("the answer");
        inner.ReceivedMessages.Should().ContainSingle();
    }

    [Fact]
    public async Task Repeated_calls_accumulate_into_one_stage()
    {
        var inner = new FakeChatClient("ok") { Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 } };
        var metrics = new RunMetrics();
        using var client = new MeteredChatClient(inner, metrics);

        using (metrics.BeginStage("Coding"))
        {
            // The Coder makes one call per file — the reason a per-call count matters.
            await client.GetResponseAsync(APrompt());
            await client.GetResponseAsync(APrompt());
            await client.GetResponseAsync(APrompt());
        }

        var stage = metrics.Snapshot().Single();

        stage.ModelCalls.Should().Be(3);
        stage.InputTokens.Should().Be(30);
    }
}
