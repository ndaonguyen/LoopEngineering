using System.Text.Json;

namespace Loop.Engine.Agents.Investigation;

/// <summary>
/// Extracts the report from whatever the model actually sent.
///
/// Pure and tolerant on purpose. Provider support for native structured output varies and
/// cannot be relied on through the `IChatClient` abstraction, so the contract is carried
/// by the prompt — and a prompt-carried contract is honoured *approximately*. Models wrap
/// JSON in fences, prepend a sentence, or add an XML wrapper; none of that is worth
/// failing a run over. Genuinely absent JSON still fails, loudly.
/// </summary>
public static class ReportParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static bool TryParse(string? text, out InvestigationReport report)
    {
        report = new InvestigationReport();

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var candidate in Candidates(text))
        {
            try
            {
                if (JsonSerializer.Deserialize<InvestigationReport>(candidate, Options) is { } parsed)
                {
                    report = parsed;
                    return true;
                }
            }
            catch (JsonException)
            {
                // Try the next candidate rather than giving up — the first brace in the
                // response may belong to a code sample rather than the report.
            }
        }

        return false;
    }

    /// <summary>
    /// Plausible JSON substrings, most likely first: the whole response, then each
    /// balanced <c>{…}</c> block found in it.
    /// </summary>
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
