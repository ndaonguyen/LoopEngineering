using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Loop.Engine.Agents.Investigation;

/// <summary>
/// The Phase 2 agent: extract symbols, retrieve candidate files, ask the model to
/// investigate, and return a structured result. It has no path that writes source code.
/// </summary>
public sealed class InvestigationAgent : IInvestigator
{
    private readonly IChatClient _chat;
    private readonly FileRetriever _retriever;
    private readonly ILogger<InvestigationAgent> _logger;

    public InvestigationAgent(
        IChatClient chat,
        FileRetriever retriever,
        ILogger<InvestigationAgent> logger)
    {
        _chat = chat;
        _retriever = retriever;
        _logger = logger;
    }

    public async Task<AnalysisResult> InvestigateAsync(
        Issue issue, CancellationToken cancellationToken = default)
    {
        var symbols = SymbolExtractor.Extract(issue);
        var files = _retriever.Retrieve(symbols);

        _logger.LogInformation(
            "Investigating #{Number} with {Files} candidate file(s).", issue.Number, files.Count);

        List<ChatMessage> messages =
        [
            new(ChatRole.System, InvestigationPrompt.System),
            new(ChatRole.User, InvestigationPrompt.BuildUserMessage(issue, files)),
        ];

        var response = await _chat.GetResponseAsync<InvestigationReport>(
            messages, cancellationToken: cancellationToken);

        // TryGetResult, not .Result: the property throws a raw JsonException, which tells
        // an operator nothing about which issue failed or why it matters. Failing loudly
        // is deliberate — an empty AnalysisResult would hand Phase 3 an empty allow-list
        // and the pipeline would report success having investigated nothing.
        if (!response.TryGetResult(out var report))
        {
            throw new InvalidOperationException(
                $"The model returned no parseable investigation for issue #{issue.Number}. " +
                $"Raw response: {Truncate(response.Text)}");
        }

        var analysis = new AnalysisResult(
            Symptoms: report.Symptoms,
            PossibleRootCauses: report.PossibleRootCauses,
            AffectedFiles: KeepOnlyRetrieved(report.AffectedFiles, files),
            RecommendedInvestigation: report.RecommendedInvestigation)
        {
            Confidence = report.Confidence,
        };

        return analysis with { Markdown = InvestigationMarkdown.Render(issue, analysis) };
    }

    /// <summary>Keeps the failure message readable when the model returns an essay.</summary>
    private static string Truncate(string text) =>
        text.Length <= 200 ? text : text[..200] + "…";

    /// <summary>
    /// Drop any path the model returned that was not in the retrieved set. Models
    /// occasionally produce plausible-looking paths that don't exist; letting one through
    /// would put a phantom file on Phase 3's allow-list.
    /// </summary>
    private IReadOnlyList<string> KeepOnlyRetrieved(
        IReadOnlyList<string> claimed, IReadOnlyList<RetrievedFile> retrieved)
    {
        var known = retrieved.Select(f => f.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>();

        foreach (var path in claimed)
        {
            var normalised = path.Replace('\\', '/').Trim();
            if (known.Contains(normalised))
            {
                kept.Add(normalised);
            }
            else
            {
                _logger.LogWarning(
                    "Discarding '{Path}' — the model cited a file that was never retrieved.", path);
            }
        }

        return kept;
    }
}
