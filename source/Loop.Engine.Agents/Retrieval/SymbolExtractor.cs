using System.Text.RegularExpressions;
using Loop.Engine.Core.Model;

namespace Loop.Engine.Agents.Retrieval;

/// <summary>
/// Pulls candidate identifiers out of an issue so the retriever knows what to look for.
///
/// Pure and I/O-free on purpose — this is the half of retrieval that can be asserted
/// exactly, the same split that made <c>IssueReportFormatter</c> testable in Phase 1.
/// </summary>
public static class SymbolExtractor
{
    // "   at Loop.Engine.Worker.Pipeline.IssuePollingService.PollOnceAsync(...)"
    private static readonly Regex StackFrame = new(
        @"\bat\s+(?<symbol>[A-Z][\w]*(?:\.[A-Z][\w`]*)+)\.(?<method>[\w`]+)\s*\(",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // "IssuePollingService.cs" / "src/Foo.cs:42"
    private static readonly Regex SourceFile = new(
        @"\b(?<file>[A-Za-z0-9_]+\.cs)\b",
        RegexOptions.Compiled);

    // Bare PascalCase words — the weakest signal, so ranked last.
    private static readonly Regex PascalCase = new(
        @"\b(?<word>[A-Z][a-z0-9]+(?:[A-Z][a-z0-9]+)+)\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Candidate symbols, strongest signal first: stack-frame types and methods, then
    /// referenced <c>.cs</c> filenames, then bare PascalCase words. Order is the point —
    /// a stack trace names the failure path, a capitalised word in prose merely might.
    /// </summary>
    public static IReadOnlyList<string> Extract(Issue issue)
    {
        var text = $"{issue.Title}\n{issue.Body}";
        var ranked = new List<string>();

        foreach (Match m in StackFrame.Matches(text))
        {
            AddTypeName(ranked, m.Groups["symbol"].Value);
            Add(ranked, m.Groups["method"].Value);
        }

        foreach (Match m in SourceFile.Matches(text))
        {
            Add(ranked, Path.GetFileNameWithoutExtension(m.Groups["file"].Value));
        }

        foreach (Match m in PascalCase.Matches(text))
        {
            Add(ranked, m.Groups["word"].Value);
        }

        return ranked;
    }

    /// <summary>A namespace-qualified name is most useful as its trailing type name.</summary>
    private static void AddTypeName(List<string> ranked, string qualified)
    {
        var last = qualified.Split('.').LastOrDefault();
        if (!string.IsNullOrEmpty(last))
        {
            Add(ranked, last);
        }
    }

    private static void Add(List<string> ranked, string symbol)
    {
        symbol = symbol.Split('`')[0].Trim();

        // Two characters isn't a symbol, it's a coincidence.
        if (symbol.Length < 3)
        {
            return;
        }

        if (!ranked.Contains(symbol, StringComparer.Ordinal))
        {
            ranked.Add(symbol);
        }
    }
}
