using AwesomeAssertions;
using Loop.Engine.Core.Model;
using Xunit;

namespace Loop.Engine.Tests;

public class RunMetricsTests
{
    [Fact]
    public void Model_calls_are_attributed_to_the_stage_that_made_them()
    {
        var metrics = new RunMetrics();

        using (metrics.BeginStage("Investigation"))
        {
            metrics.RecordModelCall(100, 20);
        }

        using (metrics.BeginStage("Coding"))
        {
            metrics.RecordModelCall(500, 300);
            metrics.RecordModelCall(400, 200);
        }

        var stages = metrics.Snapshot();

        stages.Should().HaveCount(2);
        stages[0].Stage.Should().Be("Investigation");
        stages[0].ModelCalls.Should().Be(1);
        stages[1].Stage.Should().Be("Coding");
        stages[1].ModelCalls.Should().Be(2);
        stages[1].InputTokens.Should().Be(900);
        stages[1].OutputTokens.Should().Be(500);
    }

    [Fact]
    public void Stages_appear_in_the_order_they_were_entered()
    {
        var metrics = new RunMetrics();

        foreach (var stage in new[] { "Investigation", "Planning", "Coding" })
        {
            using (metrics.BeginStage(stage))
            {
                metrics.RecordModelCall(1, 1);
            }
        }

        metrics.Snapshot().Select(s => s.Stage)
            .Should().ContainInOrder("Investigation", "Planning", "Coding");
    }

    [Fact]
    public void Re_entering_a_stage_accumulates_rather_than_replacing()
    {
        // The repair loop enters Verification repeatedly. Overwriting would report the last
        // attempt as though it were the whole cost.
        var metrics = new RunMetrics();

        using (metrics.BeginStage("Verification")) { metrics.RecordModelCall(10, 5); }
        using (metrics.BeginStage("Verification")) { metrics.RecordModelCall(10, 5); }

        var stage = metrics.Snapshot().Single();

        stage.ModelCalls.Should().Be(2);
        stage.InputTokens.Should().Be(20);
    }

    [Fact]
    public void A_call_made_outside_any_stage_is_not_dropped()
    {
        // Filed under "unattributed" rather than lost: a number in the wrong row is a bug
        // worth seeing, a silently missing one just looks like an efficient stage.
        var metrics = new RunMetrics();

        metrics.RecordModelCall(42, 7);

        metrics.Snapshot().Single().Stage.Should().Be("unattributed");
        metrics.TotalTokens.Should().Be(49);
    }

    [Fact]
    public void A_response_without_usage_still_counts_as_a_call()
    {
        var metrics = new RunMetrics();

        using (metrics.BeginStage("Coding"))
        {
            metrics.RecordModelCall(0, 0);
        }

        // Providers omit usage often enough that dropping these would understate the call
        // count — and the call count is what shows a stage looping.
        metrics.Snapshot().Single().ModelCalls.Should().Be(1);
    }

    [Fact]
    public void Reset_clears_everything()
    {
        var metrics = new RunMetrics();

        using (metrics.BeginStage("Investigation")) { metrics.RecordModelCall(10, 10); }
        metrics.Reset();

        metrics.Snapshot().Should().BeEmpty();
        metrics.TotalModelCalls.Should().Be(0);
    }

    [Fact]
    public void Render_omits_cost_when_no_rate_is_configured()
    {
        var metrics = new RunMetrics();
        using (metrics.BeginStage("Coding")) { metrics.RecordModelCall(1000, 500); }

        var table = metrics.Render();

        // A hard-coded price goes stale silently, and a confidently wrong cost is worse
        // than an absent one.
        table.Should().NotContain("Cost");
        table.Should().Contain("1,000").And.Contain("500");
    }

    [Fact]
    public void Render_prices_the_run_when_rates_are_configured()
    {
        var metrics = new RunMetrics();
        using (metrics.BeginStage("Coding")) { metrics.RecordModelCall(1_000_000, 1_000_000); }

        var table = metrics.Render(inputCostPerMillion: 0.15m, outputCostPerMillion: 0.60m);

        table.Should().Contain("Cost");
        table.Should().Contain("0.7500");
    }

    [Fact]
    public void Render_says_so_when_nothing_ran() =>
        new RunMetrics().Render().Should().Contain("No stages ran");
}
