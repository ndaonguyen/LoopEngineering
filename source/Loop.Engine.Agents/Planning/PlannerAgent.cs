using Loop.Engine.Agents.Providers;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Json;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loop.Engine.Agents.Planning;

/// <summary>Turns an <see cref="AnalysisResult"/> into an ordered <see cref="FixPlan"/>.</summary>
public sealed class PlannerAgent : IPlanner
{
    private readonly IChatClient _chat;
    private readonly AiOptions _options;
    private readonly ILogger<PlannerAgent> _logger;

    public PlannerAgent(
        [FromKeyedServices(DependencyInjection.ReasoningClientKey)] IChatClient chat,
        IOptions<AiOptions> options,
        ILogger<PlannerAgent> logger)
    {
        _chat = chat;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FixPlan> PlanAsync(
        Issue issue, AnalysisResult analysis, CancellationToken cancellationToken = default)
    {
        if (analysis.AffectedFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot plan a fix for issue #{issue.Number}: the investigation identified no files. " +
                "Planning against an empty allow-list would produce steps nothing can act on.");
        }

        List<ChatMessage> messages =
        [
            new(ChatRole.System, PlannerPrompt.System),
            new(ChatRole.User, PlannerPrompt.BuildUserMessage(issue, analysis)),
        ];

        var options = new ChatOptions { MaxOutputTokens = _options.MaxOutputTokens };
        var response = await _chat.GetResponseAsync(messages, options, cancellationToken);

        if (!TolerantJson.TryParse<PlanDto>(response.Text, out var dto))
        {
            throw new InvalidOperationException(
                $"The model returned no parseable plan for issue #{issue.Number}. " +
                $"FinishReason={response.FinishReason?.ToString() ?? "none"}, " +
                $"TextLength={response.Text.Length}, " +
                $"MaxOutputTokens={_options.MaxOutputTokens}.");
        }

        var steps = BuildSteps(dto, analysis);

        if (steps.Count == 0)
        {
            // An empty plan reads as "nothing to do" downstream, and the pipeline would
            // report success having planned nothing.
            throw new InvalidOperationException(
                $"The plan for issue #{issue.Number} contains no usable steps. " +
                $"The model returned {dto.Steps.Length} step(s), none citing an affected file.");
        }

        _logger.LogInformation("Planned {Count} step(s) for #{Number}.", steps.Count, issue.Number);
        return new FixPlan(steps);
    }

    /// <summary>
    /// Keeps only files the investigation identified. A step naming anything else is the
    /// plan leaking scope — the same filter the Investigation agent applies to hallucinated
    /// paths, for the same reason.
    /// </summary>
    private List<FixPlanStep> BuildSteps(PlanDto dto, AnalysisResult analysis)
    {
        var allowed = analysis.AffectedFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var steps = new List<FixPlanStep>();

        foreach (var step in dto.Steps)
        {
            var files = new List<string>();

            foreach (var path in step.Files)
            {
                var normalised = path.Replace('\\', '/').Trim();

                if (allowed.Contains(normalised))
                {
                    files.Add(normalised);
                }
                else
                {
                    _logger.LogWarning(
                        "Dropping '{Path}' from a plan step — it is not among the investigation's affected files.",
                        path);
                }
            }

            if (files.Count > 0)
            {
                steps.Add(new FixPlanStep(steps.Count + 1, step.Description.Trim(), files));
            }
            else
            {
                _logger.LogWarning("Dropping plan step '{Description}' — no allowed files remain.", step.Description);
            }
        }

        return steps;
    }
}
