using Loop.Engine.Core.Model;

namespace Loop.Engine.Core.Abstractions;

/// <summary>
/// Somewhere the fix can be written and built.
///
/// The retry loop needs a directory it can put files in and point a compiler at — it does
/// not need git. Depending on the concrete worktree would force every test of the loop to
/// initialise a real repository, and the offline-tests rule would quietly become
/// "offline except for git".
/// </summary>
public interface IFixWorkspace
{
    /// <summary>Directory the build runs in.</summary>
    string Path { get; }

    void Apply(IReadOnlyList<CodeEdit> edits);
}
