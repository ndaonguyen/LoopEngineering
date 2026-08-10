using System.Runtime.CompilerServices;
using Loop.Engine.Core.Model;
using Microsoft.Extensions.AI;

namespace Loop.Engine.Agents.Telemetry;

/// <summary>
/// Records every model call into <see cref="RunMetrics"/>.
///
/// Wrapping the client rather than instrumenting each agent is the point: an agent that
/// forgot to report itself would be invisible precisely where the numbers matter, and the
/// missing tokens would look like an efficient stage rather than an unmeasured one.
/// </summary>
public sealed class MeteredChatClient(IChatClient inner, RunMetrics metrics)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        Record(response.Usage);

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Nothing streams today. Counting it anyway costs one loop and means the figures do
        // not quietly become wrong the day some stage switches.
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            foreach (var content in update.Contents.OfType<UsageContent>())
            {
                Record(content.Details);
            }

            yield return update;
        }
    }

    /// <summary>
    /// A response with no usage still counts as a call. Providers omit the field often
    /// enough that dropping those would understate the call count — and the call count is
    /// what shows a stage looping.
    /// </summary>
    private void Record(UsageDetails? usage) =>
        metrics.RecordModelCall(usage?.InputTokenCount ?? 0, usage?.OutputTokenCount ?? 0);
}
