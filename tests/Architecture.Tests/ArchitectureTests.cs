using AwesomeAssertions;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// The dependency rules from CLAUDE.md, as a build failure rather than a paragraph.
///
/// The graph points inwards and its innermost project depends on nothing at all. Those facts
/// were true, documented, and unenforced — which is the state in which they stop being true
/// without anyone noticing.
/// </summary>
public class ArchitectureTests
{
    /// <summary>
    /// The only project references allowed to exist under <c>source/</c>.
    ///
    /// Exhaustive on purpose: adding a project makes this suite fail until someone writes down
    /// where it sits. That prompt is the point — a new project silently inheriting whatever
    /// references were convenient is how a layered design stops being layered.
    /// </summary>
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.Ordinal)
    {
        // Core ← {Agents, GitHub} ← Worker ← host
        ["Loop.Engine.Core"] = [],
        ["Loop.Engine.Agents"] = ["Loop.Engine.Core"],
        ["Loop.Engine.GitHub"] = ["Loop.Engine.Core"],
        ["Loop.Engine.Worker"] = ["Loop.Engine.Agents", "Loop.Engine.Core", "Loop.Engine.GitHub"],
        ["Loop.Engine"] = ["Loop.Engine.Agents", "Loop.Engine.GitHub", "Loop.Engine.Worker"],
    };

    /// <summary>
    /// The inner ring. It holds the ports and the model; everything depends on it and it depends
    /// on nothing, so a port can never acquire a dependency on its own implementation.
    /// </summary>
    public static TheoryData<string> InnerRing => ["Loop.Engine.Core"];

    [Theory]
    [MemberData(nameof(InnerRing))]
    public void Inner_ring_projects_have_no_project_references(string project)
    {
        var graph = SolutionGraph.Read();

        graph.Should().ContainKey(project);

        graph[project].Should().BeEmpty(
            "{0} is the innermost project of its stack and must depend on nothing", project);
    }

    [Fact]
    public void Adapters_do_not_reference_each_other()
    {
        var graph = SolutionGraph.Read();

        graph["Loop.Engine.Agents"].Should().NotContain("Loop.Engine.GitHub",
            "the agents must not reach GitHub directly — model output would become an API call " +
            "with nothing in between");

        graph["Loop.Engine.GitHub"].Should().NotContain("Loop.Engine.Agents",
            "the GitHub adapter is a sibling of the agents, not a consumer of them");
    }

    [Fact]
    public void No_project_references_anything_outside_the_allow_list()
    {
        var graph = SolutionGraph.Read();

        var violations = graph
            .SelectMany(entry => entry.Value
                .Where(reference => !Allowed.TryGetValue(entry.Key, out var allowed)
                                    || !allowed.Contains(reference, StringComparer.Ordinal))
                .Select(reference => $"{entry.Key} -> {reference}"))
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        violations.Should().BeEmpty(
            "the dependency rules in CLAUDE.md allow only the graph in {0}.{1}",
            nameof(ArchitectureTests), nameof(Allowed));
    }

    [Fact]
    public void Every_project_under_source_has_a_documented_place_in_the_graph()
    {
        var graph = SolutionGraph.Read();

        graph.Keys.Should().BeSubsetOf(Allowed.Keys,
            "a new project must be placed in the dependency rules deliberately, not inherit " +
            "whatever references were convenient");
    }
}
