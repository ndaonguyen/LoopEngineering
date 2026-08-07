using System.Text.RegularExpressions;

namespace Loop.Engine.Agents.Coding;

/// <summary>One file returned by the Coder, plus the diagnosis when it came from a repair.</summary>
public sealed record CodeReply(string Path, string Contents, string Hypothesis = "");

/// <summary>
/// Parses a code reply carried in a fenced block rather than a JSON string.
///
/// Source code inside JSON has to escape every backslash, quote and newline. A C# file
/// containing <c>Replace('\\', '/')</c> needs four backslashes to survive the round trip,
/// and one slip anywhere in the file discards the whole reply — which is exactly how a
/// correct fix was lost. A fenced block needs no escaping at all, so the model transcribes
/// the file as it will appear on disk.
///
/// The plan and the review still travel as JSON: they are short, structured, and contain
/// no code.
/// </summary>
public static class CodeReplyParser
{
    // FILE: path/to/File.cs   (also tolerates "path:" or bare "**FILE:**" decoration)
    private static readonly Regex PathLine = new(
        @"^[^\S\r\n]*\**\s*(?:FILE|PATH)\s*\**\s*:\s*\**\s*[`""']?(?<path>[^\r\n`""'*]+?)[`""']?\**[^\S\r\n]*$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex HypothesisLine = new(
        @"^[^\S\r\n]*\**\s*HYPOTHESIS\s*\**\s*:\s*(?<text>.+?)[^\S\r\n]*$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // ```csharp … ``` — non-greedy so the first complete block wins.
    private static readonly Regex FencedBlock = new(
        @"```[a-zA-Z#+]*[^\S\r\n]*\r?\n(?<code>.*?)\r?\n?```",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static bool TryParse(string? text, out CodeReply reply)
    {
        reply = new CodeReply(string.Empty, string.Empty);

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var pathMatch = PathLine.Match(text);
        var codeMatch = FencedBlock.Match(text);

        if (!pathMatch.Success || !codeMatch.Success)
        {
            return false;
        }

        var code = codeMatch.Groups["code"].Value;

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        reply = new CodeReply(
            Path: pathMatch.Groups["path"].Value.Trim().Replace('\\', '/'),
            Contents: code,
            Hypothesis: HypothesisLine.Match(text) is { Success: true } h
                ? h.Groups["text"].Value.Trim()
                : string.Empty);

        return true;
    }
}
