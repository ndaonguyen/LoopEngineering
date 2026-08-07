using Loop.Engine.Agents.Providers;
using Loop.Engine.Agents.Coding;
using Loop.Engine.Agents.Investigation;
using Loop.Engine.Agents.Json;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loop.Engine.Agents.Verification;

/// <summary>
/// Builds and tests the fix, feeding failures back until it passes or the attempts run out.
///
/// This is the first component in the pipeline that can <b>falsify</b> anything. Everything
/// upstream reasons about the code; here a compiler and a test runner decide, and a
/// confident wrong answer finally stops being indistinguishable from a right one.
/// </summary>
public sealed class FixVerifier
{
    private readonly IChatClient _chat;
    private readonly IBuildRunner _runner;
    private readonly VerificationOptions _options;
    private readonly AiOptions _model;
    private readonly ILogger<FixVerifier> _logger;

    public FixVerifier(
        IChatClient chat,
        IBuildRunner runner,
        IOptions<VerificationOptions> options,
        IOptions<AiOptions> model,
        ILogger<FixVerifier> logger)
    {
        _chat = chat;
        _runner = runner;
        _options = options.Value;
        _model = model.Value;
        _logger = logger;
    }

    public async Task<VerificationResult> VerifyAsync(
        Issue issue,
        IReadOnlyList<CodeEdit> edits,
        IFixWorkspace worktree,
        CancellationToken cancellationToken = default)
    {
        var current = edits.ToList();
        var hypotheses = new List<string>();
        BuildResult? lastFailure = null;

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            worktree.Apply(current);

            var result = await BuildAndTestAsync(worktree.Path, cancellationToken);

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "Fix for #{Number} verified on attempt {Attempt}.", issue.Number, attempt);
                return new VerificationResult(true, attempt, current, hypotheses, null);
            }

            lastFailure = result;

            _logger.LogWarning(
                "Attempt {Attempt}/{Max} for #{Number} failed with {Count} error(s): {First}",
                attempt, _options.MaxAttempts, issue.Number, result.Errors.Count,
                result.Errors.FirstOrDefault() ?? "(no detail)");

            if (attempt == _options.MaxAttempts)
            {
                break;
            }

            var repaired = await RepairAsync(issue, current, result, hypotheses, attempt + 1, cancellationToken);

            if (repaired is null)
            {
                _logger.LogError("Repair attempt {Attempt} produced nothing usable; stopping.", attempt + 1);
                break;
            }

            current = repaired;
        }

        return new VerificationResult(false, Math.Min(hypotheses.Count + 1, _options.MaxAttempts),
            current, hypotheses, lastFailure);
    }

    private async Task<BuildResult> BuildAndTestAsync(string path, CancellationToken cancellationToken)
    {
        var build = await _runner.BuildAsync(path, cancellationToken);

        // No point running tests against a tree that does not compile — the test output
        // would just restate the compiler errors in a noisier form.
        return build.Succeeded
            ? await _runner.TestAsync(path, _options.TestProject, cancellationToken)
            : build;
    }

    /// <summary>
    /// One repair round. Repairs the first edited file only — the failure almost always
    /// points at one file, and asking for every file back reintroduces the output-budget
    /// truncation that forced per-file calls in Phase 3.
    /// </summary>
    private async Task<List<CodeEdit>?> RepairAsync(
        Issue issue,
        List<CodeEdit> current,
        BuildResult failure,
        List<string> hypotheses,
        int attempt,
        CancellationToken cancellationToken)
    {
        var target = ChooseTarget(current, failure);

        List<ChatMessage> messages =
        [
            new(ChatRole.System, RepairPrompt.System),
            new(ChatRole.User, RepairPrompt.Build(
                issue, target.RelativePath, target.NewContents, failure,
                hypotheses, attempt, _options.MaxAttempts)),
        ];

        var options = new ChatOptions { MaxOutputTokens = _model.MaxOutputTokens };
        var response = await _chat.GetResponseAsync(messages, options, cancellationToken);

        if (!CodeReplyParser.TryParse(response.Text, out var edit))
        {
            _logger.LogWarning(
                "No parseable repair. FinishReason={Reason}, TextLength={Length}, MaxOutputTokens={Max}.",
                response.FinishReason?.ToString() ?? "none", response.Text.Length, _model.MaxOutputTokens);
            return null;
        }

        if (!string.Equals(edit.Path, target.RelativePath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Repair returned '{Returned}' when asked for '{Target}'.", edit.Path, target.RelativePath);
            return null;
        }

        if (!SyntaxVerifier.Verify(target.RelativePath, edit.Contents, out var errors))
        {
            _logger.LogWarning(
                "Repair to '{Target}' does not parse: {Errors}",
                target.RelativePath, string.Join("; ", errors.Take(3)));
            return null;
        }

        if (!string.IsNullOrWhiteSpace(edit.Hypothesis))
        {
            hypotheses.Add(edit.Hypothesis);
            _logger.LogInformation("Attempt {Attempt} hypothesis: {Hypothesis}", attempt, edit.Hypothesis);
        }

        return current
            .Select(e => e.RelativePath == target.RelativePath
                ? e with { NewContents = edit.Contents }
                : e)
            .ToList();
    }

    /// <summary>The edited file the errors mention, falling back to the first.</summary>
    private static CodeEdit ChooseTarget(List<CodeEdit> edits, BuildResult failure)
    {
        foreach (var edit in edits)
        {
            var name = Path.GetFileName(edit.RelativePath);

            if (failure.Errors.Any(e => e.Contains(name, StringComparison.OrdinalIgnoreCase)))
            {
                return edit;
            }
        }

        return edits[0];
    }
}
