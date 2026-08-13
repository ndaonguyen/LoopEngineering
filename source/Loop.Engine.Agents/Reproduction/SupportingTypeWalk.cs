using Loop.Engine.Agents.Retrieval;

namespace Loop.Engine.Agents.Reproduction;

/// <summary>
/// Walks outwards from the code under test to the types a reproduction test has to build.
///
/// One hop is not enough, and a real run proved it. Scoring a project needs a
/// <c>List&lt;Finding&gt;</c>, so <c>Finding</c> was retrieved — but building a <c>Finding</c>
/// needs a <c>Citation</c>, which is a property *on* <c>Finding</c> and appears in no signature
/// of the code under test. The model had to guess a constructor it had never seen
/// (<c>CS1729</c>), and guessed an enum member that does not exist (<c>CS0117</c>). Neither is
/// carelessness; both are the same request to write against something invisible.
///
/// Two hops reach the whole shape a constructor call needs, because the second hop reads the
/// retrieved type's own properties. Deeper buys little and costs prompt budget fast.
///
/// Pure apart from the injected retrieve — a traversal that only runs behind a model call is a
/// traversal nobody can test.
/// </summary>
public static class SupportingTypeWalk
{
    /// <summary>How far out to walk. Two: the code under test, then the types it names.</summary>
    public const int DefaultHops = 2;

    /// <summary>
    /// Files defining the types reachable from <paramref name="seed"/>, nearest first.
    /// </summary>
    /// <param name="seed">Files already in the prompt — the code under test.</param>
    /// <param name="alreadyInPrompt">Paths not worth retrieving again. Not mutated.</param>
    /// <param name="retrieve">Type names in, candidate files out.</param>
    /// <param name="maxFiles">Total budget across every hop, not per hop.</param>
    /// <param name="hops">How many times to expand. One reproduces the old behaviour.</param>
    public static IReadOnlyList<RetrievedFile> From(
        IReadOnlyList<RetrievedFile> seed,
        IEnumerable<string> alreadyInPrompt,
        Func<IReadOnlyList<string>, IReadOnlyList<RetrievedFile>> retrieve,
        int maxFiles,
        int hops = DefaultHops)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(retrieve);

        var seen = new HashSet<string>(alreadyInPrompt, StringComparer.OrdinalIgnoreCase);
        var collected = new List<RetrievedFile>();
        var frontier = seed;

        for (var hop = 0; hop < hops && collected.Count < maxFiles; hop++)
        {
            var wanted = frontier
                .SelectMany(f => SignatureTypes.Extract(f.Excerpt))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (wanted.Count == 0)
            {
                break;
            }

            var found = retrieve(wanted)
                .Where(f => seen.Add(f.RelativePath))
                .Take(maxFiles - collected.Count)
                .ToList();

            // Nothing new: another hop would ask the same question and get the same answer.
            if (found.Count == 0)
            {
                break;
            }

            collected.AddRange(found);

            // The next hop reads what this one pulled in — that is where a property type like
            // Citation finally becomes visible.
            frontier = found;
        }

        return collected;
    }
}
