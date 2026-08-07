using Loop.Engine.Agents.Providers;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Json;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loop.Engine.Agents.Review;

/// <summary>
/// Reads a diff and reports on it.
///
/// Its dependencies are the whole guarantee: a chat client, options, a logger. No
/// workspace, no worktree, no coder, no file system. It could not edit the code it is
/// reviewing even if a prompt told it to.
/// </summary>
public sealed class ReviewerAgent : IReviewer
{
    private readonly IChatClient _chat;
    private readonly AiOptions _options;
    private readonly ILogger<ReviewerAgent> _logger;

    public ReviewerAgent(
        IChatClient chat,
        IOptions<AiOptions> options,
        ILogger<ReviewerAgent> logger)
    {
        _chat = chat;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ReviewReport> ReviewAsync(
        Issue issue, string unifiedDiff, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(unifiedDiff))
        {
            return ReviewReport.Empty;
        }

        List<ChatMessage> messages =
        [
            new(ChatRole.System, ReviewerPrompt.System),
            new(ChatRole.User, ReviewerPrompt.Build(issue, unifiedDiff)),
        ];

        var options = new ChatOptions { MaxOutputTokens = _options.MaxOutputTokens };
        var response = await _chat.GetResponseAsync(messages, options, cancellationToken);

        if (!TolerantJson.TryParse<ReviewDto>(response.Text, out var dto))
        {
            // Advisory output: a failed review must not sink a verified fix. Say so and
            // return nothing rather than throwing away work that already builds and passes.
            _logger.LogWarning(
                "No parseable review for #{Number}. FinishReason={Reason}, TextLength={Length}.",
                issue.Number, response.FinishReason?.ToString() ?? "none", response.Text.Length);

            return ReviewReport.Empty;
        }

        var findings = dto.Findings
            .Where(f => !string.IsNullOrWhiteSpace(f.Detail))
            .Select(f => new ReviewFinding(
                Category: string.IsNullOrWhiteSpace(f.Category) ? "general" : f.Category.Trim(),
                Severity: string.IsNullOrWhiteSpace(f.Severity) ? "low" : f.Severity.Trim(),
                Detail: f.Detail.Trim()))
            .ToList();

        _logger.LogInformation("Review of #{Number} produced {Count} finding(s).", issue.Number, findings.Count);
        return new ReviewReport(findings);
    }
}
