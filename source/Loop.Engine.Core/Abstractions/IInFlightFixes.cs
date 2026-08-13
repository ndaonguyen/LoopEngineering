namespace Loop.Engine.Core.Abstractions;

/// <summary>
/// Which issues already have a fix in flight.
///
/// A separate port from <see cref="IIssueSource"/> because it answers a different question:
/// that one knows what needs doing, this one knows what is already being done. An issue stays
/// open until its pull request merges, so without this the engine re-investigates, re-plans and
/// re-writes the same fix on every tick — then fails at publish because the branch exists.
/// Nothing breaks; it just spends money producing a diff that already exists.
///
/// Read-only on purpose. Deciding what is in flight must not be able to change what is.
/// </summary>
public interface IInFlightFixes
{
    /// <summary>
    /// Issue numbers with an open fix pull request. Empty when nothing is in flight —
    /// never null, so a caller cannot forget the difference.
    /// </summary>
    Task<IReadOnlySet<int>> GetIssuesWithOpenFixAsync(CancellationToken cancellationToken = default);
}
