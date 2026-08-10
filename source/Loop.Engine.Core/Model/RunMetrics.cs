using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Loop.Engine.Core.Model;

/// <summary>What one stage of a tick cost.</summary>
public sealed record StageMetric(
    string Stage, TimeSpan Duration, int ModelCalls, long InputTokens, long OutputTokens)
{
    public long TotalTokens => InputTokens + OutputTokens;
}

/// <summary>
/// Per-stage tokens, time, and call counts for a single tick.
///
/// It exists because the engine runs unattended, and an unattended system you cannot measure
/// is one you cannot tell apart from a broken one. Before this, a whole run reported a single
/// number — token usage — and only when a call failed.
///
/// The stage is ambient rather than passed down: every agent would otherwise have to remember
/// to name itself on every call, and the one that forgot would be invisible in exactly the
/// way that matters.
/// </summary>
public sealed class RunMetrics
{
    private static readonly AsyncLocal<string?> CurrentStage = new();

    private readonly ConcurrentDictionary<string, Accumulator> _stages = new();
    private readonly ConcurrentQueue<string> _order = new();

    /// <summary>Starts timing a stage; model calls made inside it are attributed to it.</summary>
    public IDisposable BeginStage(string stage)
    {
        var accumulator = _stages.GetOrAdd(stage, key =>
        {
            _order.Enqueue(key);
            return new Accumulator();
        });

        var previous = CurrentStage.Value;
        CurrentStage.Value = stage;

        var started = Stopwatch.GetTimestamp();

        return new Scope(() =>
        {
            accumulator.AddDuration(Stopwatch.GetElapsedTime(started));
            CurrentStage.Value = previous;
        });
    }

    /// <summary>
    /// Records one model call against whichever stage is current. A call made outside any
    /// stage is filed under "unattributed" rather than dropped — a number in the wrong row
    /// is a bug worth seeing, a silently missing one is not.
    /// </summary>
    public void RecordModelCall(long inputTokens, long outputTokens)
    {
        var stage = CurrentStage.Value ?? "unattributed";

        _stages.GetOrAdd(stage, key =>
        {
            _order.Enqueue(key);
            return new Accumulator();
        }).AddCall(inputTokens, outputTokens);
    }

    /// <summary>Clears everything. Called once per tick — figures are per-run, not lifetime.</summary>
    public void Reset()
    {
        _stages.Clear();
        _order.Clear();
    }

    /// <summary>Stages in the order they were first entered.</summary>
    public IReadOnlyList<StageMetric> Snapshot() =>
        _order
            .Distinct()
            .Where(_stages.ContainsKey)
            .Select(stage => _stages[stage].ToMetric(stage))
            .ToList();

    public int TotalModelCalls => Snapshot().Sum(s => s.ModelCalls);

    public long TotalTokens => Snapshot().Sum(s => s.TotalTokens);

    /// <summary>
    /// A markdown table. <paramref name="inputCostPerMillion"/> and
    /// <paramref name="outputCostPerMillion"/> add a cost column when set; unset means no
    /// cost column at all, because a hard-coded price goes stale silently and a wrong
    /// number is worse than a missing one.
    /// </summary>
    public string Render(decimal inputCostPerMillion = 0m, decimal outputCostPerMillion = 0m)
    {
        var stages = Snapshot();

        if (stages.Count == 0)
        {
            return "_No stages ran._";
        }

        var priced = inputCostPerMillion > 0m || outputCostPerMillion > 0m;

        var table = new StringBuilder();
        table.AppendLine(priced
            ? "| Stage | Duration | Calls | Input | Output | Cost |"
            : "| Stage | Duration | Calls | Input | Output |");
        table.AppendLine(priced
            ? "| --- | ---: | ---: | ---: | ---: | ---: |"
            : "| --- | ---: | ---: | ---: | ---: |");

        foreach (var stage in stages)
        {
            var row =
                $"| {stage.Stage} | {Format(stage.Duration)} | {stage.ModelCalls} " +
                $"| {stage.InputTokens:N0} | {stage.OutputTokens:N0} |";

            table.AppendLine(priced
                ? row + $" {Cost(stage, inputCostPerMillion, outputCostPerMillion):C4} |"
                : row);
        }

        var totalDuration = TimeSpan.FromTicks(stages.Sum(s => s.Duration.Ticks));
        var totalRow =
            $"| **Total** | **{Format(totalDuration)}** | **{stages.Sum(s => s.ModelCalls)}** " +
            $"| **{stages.Sum(s => s.InputTokens):N0}** | **{stages.Sum(s => s.OutputTokens):N0}** |";

        table.AppendLine(priced
            ? totalRow +
              $" **{stages.Sum(s => Cost(s, inputCostPerMillion, outputCostPerMillion)):C4}** |"
            : totalRow);

        return table.ToString().TrimEnd();
    }

    private static decimal Cost(StageMetric stage, decimal input, decimal output) =>
        (stage.InputTokens * input / 1_000_000m) + (stage.OutputTokens * output / 1_000_000m);

    private static string Format(TimeSpan duration) =>
        duration.TotalMinutes >= 1
            ? $"{(int)duration.TotalMinutes}m {duration.Seconds}s"
            : $"{duration.TotalSeconds:F1}s";

    private sealed class Accumulator
    {
        private long _durationTicks;
        private int _calls;
        private long _input;
        private long _output;

        public void AddDuration(TimeSpan duration) =>
            Interlocked.Add(ref _durationTicks, duration.Ticks);

        public void AddCall(long input, long output)
        {
            Interlocked.Increment(ref _calls);
            Interlocked.Add(ref _input, input);
            Interlocked.Add(ref _output, output);
        }

        public StageMetric ToMetric(string stage) => new(
            stage,
            TimeSpan.FromTicks(Interlocked.Read(ref _durationTicks)),
            Volatile.Read(ref _calls),
            Interlocked.Read(ref _input),
            Interlocked.Read(ref _output));
    }

    private sealed class Scope(Action onDispose) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            onDispose();
        }
    }
}
