using Loop.Engine.Core.Model;

namespace Loop.Engine.Core.Abstractions;

/// <summary>
/// Writes the fix and returns a diff.
///
/// Note what this signature does <b>not</b> carry: no repository path, no retriever, no
/// search capability. The Coder can only touch files the investigation identified, because
/// those are the only files it is given. The issue asks that the Coder not explore the
/// repository — a constraint the model is merely asked to honour lapses under pressure;
/// one it cannot physically violate does not.
/// </summary>
public interface ICoder
{
    Task<CodeChangeSet> WriteCodeAsync(
        Issue issue,
        AnalysisResult analysis,
        FixPlan plan,
        CancellationToken cancellationToken = default);
}
