using System.Text.Json;

namespace Loop.Engine.Agents.Json;

/// <summary>
/// Extracts a typed payload from whatever the model actually sent.
///
/// Provider support for native structured output cannot be relied on through the
/// `IChatClient` abstraction — Phase 2 established this the expensive way — so every
/// agent's contract is carried by its prompt. A prompt-carried contract is honoured
/// <i>approximately</i>: models fence JSON, prepend a sentence, or wrap it in XML tags.
/// None of that is worth failing a run over. Genuinely absent JSON still fails, loudly.
/// </summary>
public static class TolerantJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static bool TryParse<T>(string? text, out T value) where T : new()
    {
        value = new T();

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var candidate in Candidates(text))
        {
            try
            {
                if (JsonSerializer.Deserialize<T>(candidate, Options) is { } parsed)
                {
                    value = parsed;
                    return true;
                }
            }
            catch (JsonException)
            {
                // Try the next candidate — the first brace in a response often belongs to
                // a code sample rather than the payload.
            }
        }

        return false;
    }

    private static IEnumerable<string> Candidates(string text)
    {
        var trimmed = text.Trim();
        yield return trimmed;

        foreach (var block in BalancedObjects(trimmed))
        {
            yield return block;
        }
    }

    /// <summary>
    /// Walks the text tracking brace depth, ignoring braces inside string literals so an
    /// embedded <c>"{"</c> cannot end a block early.
    /// </summary>
    private static IEnumerable<string> BalancedObjects(string text)
    {
        var depth = 0;
        var start = -1;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;

                case '{':
                    if (depth == 0) start = i;
                    depth++;
                    break;

                case '}':
                    if (depth > 0 && --depth == 0 && start >= 0)
                    {
                        yield return text[start..(i + 1)];
                    }
                    break;
            }
        }
    }
}
