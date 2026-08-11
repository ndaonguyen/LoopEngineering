using Loop.Engine.Core.Model;
using Loop.Engine.Agents.Retrieval;
using Xunit;

namespace Loop.Engine.Tests;

/// <summary>
/// Tests for the SymbolExtractor class.
/// </summary>
public sealed class SymbolExtractorTests
{
    [Fact]
    public void Extract_returns_a_single_word_type_name()
    {
        // Arrange
        var issue = new Issue(
            Number: 1,
            Title: "Test Issue",
            Body: "Widget throws NullReferenceException",
            Url: "http://example.com",
            Labels: new List<string>(),
            Assignee: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow
        );

        // Act
        var symbols = SymbolExtractor.Extract(issue);

        // Assert
        Assert.Contains("Widget", symbols); // This assertion fails because "Widget" is not extracted.
    }
}