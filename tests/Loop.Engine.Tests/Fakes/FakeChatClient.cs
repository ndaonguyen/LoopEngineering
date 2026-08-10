using Microsoft.Extensions.AI;

namespace Loop.Engine.Tests.Fakes;

/// <summary>
/// Returns a canned response. Exists so the Investigation agent can be tested with no
/// network call and no API key — a hard requirement of Phase 2, not a convenience.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private readonly string _responseText;

    /// <summary>The messages the agent sent, so a test can assert on the prompt.</summary>
    public List<ChatMessage> ReceivedMessages { get; } = [];

    public FakeChatClient(string responseText) => _responseText = responseText;

    /// <summary>
    /// Token usage to report, when a test cares. Null models the providers that omit it —
    /// which is common enough that the metering has to cope.
    /// </summary>
    public UsageDetails? Usage { get; set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ReceivedMessages.AddRange(messages);

        return Task.FromResult(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, _responseText)) { Usage = Usage });
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The Investigation agent does not stream.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
