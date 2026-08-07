using Loop.Engine.Agents.Providers;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Json;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loop.Engine.Agents.Coding;

/// <summary>
/// Implements the plan and returns a diff.
///
/// It reaches the repository only through <see cref="FileRetriever"/>, and only for the
/// paths the investigation identified — see <see cref="ICoder"/> for why that restriction
/// is structural rather than a prompt instruction.
/// </summary>
public sealed class CoderAgent : ICoder
{
    private readonly IChatClient _chat;
    private readonly FileRetriever _retriever;
    private readonly DiffGenerator _diff;
    private readonly AiOptions _options;
    private readonly ILogger<CoderAgent> _logger;

    public CoderAgent(
        IChatClient chat,
        FileRetriever retriever,
        DiffGenerator diff,
        IOptions<AiOptions> options,
        ILogger<CoderAgent> logger)
    {
        _chat = chat;
        _retriever = retriever;
        _diff = diff;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CodeChangeSet> WriteCodeAsync(
        Issue issue, AnalysisResult analysis, FixPlan plan, CancellationToken cancellationToken = default)
    {
        var files = _retriever.ReadFiles(analysis.AffectedFiles);

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                $"None of the investigation's files for issue #{issue.Number} could be read.");
        }

        using var workspace = EditWorkspace.Create(files);

        // One call per file. A single call returning every changed file blows the output
        // budget — thinking plus five whole files truncates the JSON mid-string, and the
        // whole run is lost. Per-file keeps each response small, isolates a failure to one
        // file, and still shows the model every file so the change stays coherent.
        var targets = plan.Steps
            .SelectMany(s => s.Files)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var edits = new List<CodeEdit>();

        foreach (var target in targets)
        {
            var edit = await WriteOneFileAsync(issue, analysis, plan, files, target, workspace, cancellationToken);
            if (edit is not null)
            {
                edits.Add(edit);
            }
        }

        if (edits.Count == 0)
        {
            throw new InvalidOperationException(
                $"No usable edit was produced for issue #{issue.Number} across {targets.Count} target file(s) — " +
                "every reply was unparseable, rejected by the allow-list, or failed to parse as C#.");
        }

        var diff = await _diff.GenerateAsync(workspace, cancellationToken);

        _logger.LogInformation(
            "Coder produced {Count} edit(s) and a {Length}-character diff for #{Number}.",
            edits.Count, diff.Length, issue.Number);

        return new CodeChangeSet(edits, diff);
    }

    /// <summary>
    /// Asks for one file and stages it. Returns null when the model declines to change it
    /// or the reply is unusable — a bad file is skipped, not fatal, because the remaining
    /// files may still produce a useful diff.
    /// </summary>
    private async Task<CodeEdit?> WriteOneFileAsync(
        Issue issue,
        AnalysisResult analysis,
        FixPlan plan,
        IReadOnlyList<RetrievedFile> files,
        string target,
        EditWorkspace workspace,
        CancellationToken cancellationToken)
    {
        List<ChatMessage> messages =
        [
            new(ChatRole.System, CoderPrompt.System),
            new(ChatRole.User, CoderPrompt.BuildUserMessage(issue, analysis, plan, files, target)),
        ];

        var options = new ChatOptions { MaxOutputTokens = _options.MaxOutputTokens };
        var response = await _chat.GetResponseAsync(messages, options, cancellationToken);

        if (!CodeReplyParser.TryParse(response.Text, out var edit))
        {
            _logger.LogWarning(
                "No parseable edit for '{Target}'. FinishReason={Reason}, TextLength={Length}, " +
                "MaxOutputTokens={Max}. FinishReason=Length means the budget was spent on " +
                "thinking plus the file body — raise Ai:MaxOutputTokens.",
                target, response.FinishReason?.ToString() ?? "none",
                response.Text.Length, _options.MaxOutputTokens);
            return null;
        }

        if (!string.Equals(edit.Path, target, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Model returned '{Returned}' when asked for '{Target}' — skipping.", edit.Path, target);
            return null;
        }

        if (!SyntaxVerifier.Verify(target, edit.Contents, out var errors))
        {
            // Catching this here means Phase 4 never sees a mangled file, and the failure
            // names the file that broke rather than the build that noticed.
            _logger.LogWarning(
                "Rejecting edit to '{Target}' — it no longer parses: {Errors}",
                target, string.Join("; ", errors.Take(3)));
            return null;
        }

        try
        {
            workspace.Write(target, edit.Contents);
            return new CodeEdit(target, edit.Contents);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Rejecting edit to '{Target}': {Reason}", target, ex.Message);
            return null;
        }
    }
}
