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
    private readonly InvestigationOptions _options;
    private readonly ILogger<CoderAgent> _logger;

    public CoderAgent(
        IChatClient chat,
        FileRetriever retriever,
        DiffGenerator diff,
        IOptions<InvestigationOptions> options,
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

        List<ChatMessage> messages =
        [
            new(ChatRole.System, CoderPrompt.System),
            new(ChatRole.User, CoderPrompt.BuildUserMessage(issue, analysis, plan, files)),
        ];

        var options = new ChatOptions { MaxOutputTokens = _options.MaxOutputTokens };
        var response = await _chat.GetResponseAsync(messages, options, cancellationToken);

        if (!TolerantJson.TryParse<CodeEditSetDto>(response.Text, out var dto) || dto.Edits.Length == 0)
        {
            throw new InvalidOperationException(
                $"The model returned no parseable edits for issue #{issue.Number}. " +
                $"FinishReason={response.FinishReason?.ToString() ?? "none"}, " +
                $"TextLength={response.Text.Length}, " +
                $"MaxOutputTokens={_options.MaxOutputTokens}.");
        }

        var edits = ApplyEdits(dto, workspace);

        if (edits.Count == 0)
        {
            throw new InvalidOperationException(
                $"Every edit the model proposed for issue #{issue.Number} was rejected — " +
                "all cited files outside the allow-list, or failed to parse.");
        }

        var diff = await _diff.GenerateAsync(workspace, cancellationToken);

        _logger.LogInformation(
            "Coder produced {Count} edit(s) and a {Length}-character diff for #{Number}.",
            edits.Count, diff.Length, issue.Number);

        return new CodeChangeSet(edits, diff);
    }

    /// <summary>
    /// Writes each accepted edit into the workspace. Two gates: the path must be in the
    /// allow-list (enforced by the workspace), and the result must still parse.
    /// </summary>
    private List<CodeEdit> ApplyEdits(CodeEditSetDto dto, EditWorkspace workspace)
    {
        var accepted = new List<CodeEdit>();

        foreach (var edit in dto.Edits)
        {
            var path = edit.Path.Replace('\\', '/').Trim();

            if (!SyntaxVerifier.Verify(path, edit.Contents, out var errors))
            {
                // Catching this here means Phase 4 never sees a mangled file, and the
                // failure names the file that broke rather than the build that noticed.
                _logger.LogWarning(
                    "Rejecting edit to '{Path}' — it no longer parses: {Errors}",
                    path, string.Join("; ", errors.Take(3)));
                continue;
            }

            try
            {
                workspace.Write(path, edit.Contents);
                accepted.Add(new CodeEdit(path, edit.Contents));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Rejecting edit to '{Path}': {Reason}", path, ex.Message);
            }
        }

        return accepted;
    }
}
