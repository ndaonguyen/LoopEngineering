using AwesomeAssertions;
using Loop.Engine.Agents.Reproduction;
using Loop.Engine.Agents.Retrieval;
using Xunit;

namespace Loop.Engine.Tests;

/// <summary>
/// Covers the defect a real run against another repository exposed: the reproduction test
/// needed a <c>Citation</c>, which is a property on <c>Finding</c> and appears in no signature
/// of the code under test. One hop reached <c>Finding</c> and stopped, so the model guessed a
/// constructor (<c>CS1729</c>) and an enum member (<c>CS0117</c>) it had never been shown.
/// </summary>
public class SupportingTypeWalkTests
{
    // A miniature of the repository that broke it.
    private static readonly RetrievedFile Service = File(
        "source/HealthScoringService.cs",
        "public sealed class HealthScoringService { public HealthScore? Score(string key, IReadOnlyList<Finding> findings) => null; }");

    private static readonly RetrievedFile Finding = File(
        "source/Finding.cs",
        "public sealed class Finding { public Citation Citation { get; init; } public HealthArea? Area { get; init; } }");

    private static readonly RetrievedFile Citation = File("source/Citation.cs", "public sealed record Citation(Guid Id, string Locator);");
    private static readonly RetrievedFile HealthArea = File("source/HealthArea.cs", "public enum HealthArea { Schedule, Budget }");
    private static readonly RetrievedFile HealthScore = File("source/HealthScore.cs", "public sealed record HealthScore(string Key);");

    private static RetrievedFile File(string path, string body) => new(path, body, []);

    /// <summary>Stands in for the retriever: a type name resolves to the file declaring it.</summary>
    private static IReadOnlyList<RetrievedFile> Retrieve(IReadOnlyList<string> names)
    {
        var index = new[] { Service, Finding, Citation, HealthArea, HealthScore };

        return names
            .SelectMany(n => index.Where(f => f.Excerpt.Contains($" {n} ") || f.Excerpt.Contains($" {n}(") || f.Excerpt.Contains($" {n}?")))
            .Distinct()
            .ToList();
    }

    [Fact]
    public void Reaches_a_type_that_only_appears_on_a_retrieved_type()
    {
        var walk = SupportingTypeWalk.From([Service], [Service.RelativePath], Retrieve, maxFiles: 8);

        // Finding is one hop out — it is in the method signature.
        walk.Should().Contain(f => f.RelativePath == Finding.RelativePath);

        // Citation and HealthArea are two hops out: properties on Finding, named in no
        // signature of the service. This is the assertion the old single hop failed.
        walk.Should().Contain(f => f.RelativePath == Citation.RelativePath);
        walk.Should().Contain(f => f.RelativePath == HealthArea.RelativePath);
    }

    [Fact]
    public void A_single_hop_still_misses_it()
    {
        // Pins the reason the default changed, so lowering it back is a deliberate act.
        var walk = SupportingTypeWalk.From([Service], [Service.RelativePath], Retrieve, maxFiles: 8, hops: 1);

        walk.Should().Contain(f => f.RelativePath == Finding.RelativePath);
        walk.Should().NotContain(f => f.RelativePath == Citation.RelativePath);
    }

    [Fact]
    public void Never_returns_a_file_already_in_the_prompt()
    {
        var walk = SupportingTypeWalk.From(
            [Service], [Service.RelativePath, Finding.RelativePath], Retrieve, maxFiles: 8);

        walk.Should().NotContain(f => f.RelativePath == Finding.RelativePath);
    }

    [Fact]
    public void Never_returns_the_same_file_twice()
    {
        var walk = SupportingTypeWalk.From([Service], [Service.RelativePath], Retrieve, maxFiles: 8);

        walk.Select(f => f.RelativePath).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void The_budget_covers_every_hop_not_each_one()
    {
        // Retrieval scores content matches too, so an unbounded walk drags in the repository.
        var walk = SupportingTypeWalk.From([Service], [Service.RelativePath], Retrieve, maxFiles: 2);

        walk.Should().HaveCount(2);
    }

    [Fact]
    public void Stops_when_a_hop_finds_nothing_new()
    {
        // A leaf type names nothing, so there is no second question to ask.
        var walk = SupportingTypeWalk.From([Citation], [Citation.RelativePath], Retrieve, maxFiles: 8);

        walk.Should().NotContain(f => f.RelativePath == Citation.RelativePath);
    }
}
