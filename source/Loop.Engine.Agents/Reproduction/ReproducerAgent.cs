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

        var supporting = SupportingTypes(files, exemplars);

        List<ChatMessage> messages =
        [
            new(ChatRole.System, ReproducerPrompt.System),
            new(ChatRole.User, ReproducerPrompt.User(
                issue, analysis, plan, files, supporting, exemplars, _verification.TestProject)),
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

        // Before reading the name, so the filter targets the class that will actually exist.
        var contents = TestTypeName.Rewrite(reply.Contents, issue.Number);

        var name = TestIdentity.TryReadFullyQualifiedName(contents);

        if (name is null)
        {
            // Without a name there is nothing to filter on, and an unfiltered run cannot
            // tell this test's failure from any other test's.
            _logger.LogWarning(
                "Reproduction test '{Path}' contains no [Fact] or [Theory] method.", reply.Path);
            return null;
        }

        // The file name follows the class name, for the reason the class name is decided
        // here at all: both are mechanical, and a name the repository already uses does not
        // compile. Only the leaf changes; the directory is still the model's, then relocated.
        var directory = Path.GetDirectoryName(reply.Path)?.Replace('\\', '/').TrimEnd('/');
        var proposed = string.IsNullOrWhiteSpace(directory)
            ? $"{TestTypeName.For(issue.Number)}.cs"
            : $"{directory}/{TestTypeName.For(issue.Number)}.cs";

        var path = TestFilePath.InsideProject(proposed, _verification.TestProject);

        if (!string.Equals(path, reply.Path, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Reproduction test was addressed to '{Proposed}'; writing it to '{Relocated}' " +
                "so a compiled project owns it and its type name cannot collide.",
                reply.Path, path);
        }

        _logger.LogInformation("Reproduction test for #{Number}: {Name}", issue.Number, name);

        return new ReproductionTest(path, contents, name);
    }

    /// <summary>
    /// The declarations of types the test will have to construct.
    ///
    /// Two runs died here before this existed — <c>CS7036</c> for a record the model had
    /// never seen, <c>CS0029</c> for a <c>string[]</c> it assumed was a list. The model was
    /// not being careless; it was writing against signatures it had no way to read.
    ///
    /// Files already in the prompt are excluded, since repeating them buys nothing and the
    /// Reproduction stage is the cheapest place in the pipeline to keep cheap.
    /// </summary>
    private List<RetrievedFile> SupportingTypes(
        IReadOnlyList<RetrievedFile> files, IReadOnlyList<RetrievedFile> exemplars)
    {
        var supporting = SupportingTypeWalk.From(
            files,
            files.Concat(exemplars).Select(f => f.RelativePath),
            _retriever.Retrieve,
            MaxSupportingFiles);

        _logger.LogInformation(
            "Retrieved {Count} supporting type file(s) within {Hops} hop(s) of the code under test.",
            supporting.Count, SupportingTypeWalk.DefaultHops);

        return supporting.ToList();
    }

    /// <summary>
    /// A cap across every hop, because retrieval scores content matches too and a common type
    /// name can drag in half the repository.
    ///
    /// Raised from four when the walk became two hops: the first hop spends the budget on the
    /// types in the signature, leaving nothing for the types *those* need — which is the whole
    /// point of the second hop.
    /// </summary>
    private const int MaxSupportingFiles = 8;
}
