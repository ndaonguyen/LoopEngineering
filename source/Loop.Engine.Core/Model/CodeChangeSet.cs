namespace Loop.Engine.Core.Model;

/// <summary>
/// One file the Coder wants to replace. Whole contents rather than a patch: models are
/// markedly more reliable at producing a correct file than a valid unified diff, and git
/// computes the diff for us afterwards.
/// </summary>
public sealed record CodeEdit(string RelativePath, string NewContents);

/// <summary>The Coder's output (Phase 3). No branch, no commit, no PR — that is Phase 5.</summary>
public sealed record CodeChangeSet(IReadOnlyList<CodeEdit> Edits, string UnifiedDiff)
{
    public static CodeChangeSet Empty { get; } = new([], string.Empty);

    public bool HasChanges => Edits.Count > 0 && !string.IsNullOrWhiteSpace(UnifiedDiff);
}
