using Loop.Engine.Agents.Coding;
using Loop.Engine.Agents.Providers;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Agents.Verification;
using Loop.Engine.Core.Abstractions;
using Loop.Engine.Core.Model;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loop.Engine.Agents.Reproduction;

/// <summary>
/// Writes the failing test, in one model call.
///
/// It runs on the reasoning client. Deciding what would distinguish "broken" from "fixed"
/// is the same kind of judgement as investigating, and getting it wrong is worse than
/// getting the fix wrong: a bad fix fails a build, a bad reproduction passes one.
/// </summary>
public sealed class ReproducerAgent : IReproducer
{
    private readonly IChatClient _chat;
    private readonly FileRetriever _retriever;
    private readonly AiOptions _options;
    private readonly VerificationOptions _verification;
    private readonly ILogger<ReproducerAgent> _logger;

    public ReproducerAgent(
        [FromKeyedServices(DependencyInjection.ReasoningClientKey)] IChatClient chat,
        FileRetriever retriever,
        IOptions<AiOptions> options,
        IOptions<VerificationOptions> verification,
        ILogger<ReproducerAgent> logger)
    {
        _chat = chat;
        _retriever = retriever;
        _options = options.Value;
        _verification = verification.Value;
        _logger = logger;
    }

    public async Task<ReproductionTest?> WriteFailingTestAsync(
        Issue issue,
        AnalysisResult analysis,
        FixPlan plan,
        CancellationToken cancellationToken = default)
    {
        var files = _retriever.ReadFiles(analysis.AffectedFiles);

        if (files.Count == 0)
        {
            _logger.LogWarning(
                "No source files could be read for #{Number}; cannot write a reproduction.", issue.Number);
            return null;
        }

        // Existing tests come along as style exemplars. Without one the model invents an
        // assertion library and a namespace, and the result fails to compile for reasons
        // that have nothing to do with the bug.
        var exemplars = _retriever.Retrieve(["Tests"]).Take(2).ToList();

        List<ChatMessage> messages =
        [
            new(ChatRole.System, ReproducerPrompt.System),
            new(ChatRole.User, ReproducerPrompt.User(
                issue, analysis, plan, [.. files, .. exemplars], _verification.TestProject)),
        ];

        var options = new ChatOptions { MaxOutputTokens = _options.MaxOutputTokens };
        var response = await _chat.GetResponseAsync(messages, options, cancellationToken);

        if (!CodeReplyParser.TryParse(response.Text, out var reply))
        {
            _logger.LogWarning(
                "No parseable reproduction test for #{Number}. FinishReason={Reason}, " +
                "TextLength={Length}, MaxOutputTokens={Max}.",
                issue.Number, response.FinishReason?.ToString() ?? "none",
                response.Text.Length, _options.MaxOutputTokens);
            return null;
        }

        if (!SyntaxVerifier.Verify(reply.Path, reply.Contents, out var errors))
        {
            _logger.LogWarning(
                "Rejecting reproduction test '{Path}' — it does not parse: {Errors}",
                reply.Path, string.Join("; ", errors.Take(3)));
            return null;
        }

        var name = TestIdentity.TryReadFullyQualifiedName(reply.Contents);

        if (name is null)
        {
            // Without a name there is nothing to filter on, and an unfiltered run cannot
            // tell this test's failure from any other test's.
            _logger.LogWarning(
                "Reproduction test '{Path}' contains no [Fact] or [Theory] method.", reply.Path);
            return null;
        }

        _logger.LogInformation("Reproduction test for #{Number}: {Name}", issue.Number, name);

        return new ReproductionTest(reply.Path, reply.Contents, name);
    }
}
