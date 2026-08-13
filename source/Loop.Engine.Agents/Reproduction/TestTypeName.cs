using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Loop.Engine.Agents.Reproduction;

/// <summary>
/// Gives the generated reproduction a class name that cannot already exist.
///
/// The model names the test class after the thing it is testing, which is what a person would
/// do — and on a real run it produced <c>HealthScoringServiceTests</c> in a repository that
/// already had one. <c>CS0101</c>, nothing compiled, and the run stopped one step from a fix
/// it had otherwise worked out correctly.
///
/// Naming is mechanical, so it is decided here rather than asked for, exactly as placement is
/// in <see cref="TestFilePath"/>. Deriving it from the issue number also makes a re-run
/// overwrite its own previous attempt instead of accumulating dead files.
/// </summary>
public static class TestTypeName
{
    /// <summary>The class name a reproduction for <paramref name="issueNumber"/> always gets.</summary>
    public static string For(int issueNumber) => $"Issue{issueNumber}ReproductionTests";

    /// <summary>
    /// Renames the class holding the first test method. Returns the source unchanged when
    /// there is nothing to rename — an absent test class is a rejection the gate already
    /// reports, and inventing one here would hide it.
    /// </summary>
    public static string Rewrite(string source, int issueNumber)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return source;
        }

        var root = CSharpSyntaxTree.ParseText(source).GetRoot();

        var type = root
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(IsTestMethod)
            .Select(m => m.Ancestors().OfType<TypeDeclarationSyntax>().LastOrDefault())
            .FirstOrDefault(t => t is not null);

        if (type is null)
        {
            return source;
        }

        var wanted = For(issueNumber);

        if (string.Equals(type.Identifier.Text, wanted, StringComparison.Ordinal))
        {
            return source;
        }

        // Rewrite the token, not the text: a textual replace would also rename every mention
        // of the old name elsewhere in the file, including inside strings and comments.
        var updated = root.ReplaceToken(
            type.Identifier,
            SyntaxFactory.Identifier(wanted)
                .WithLeadingTrivia(type.Identifier.LeadingTrivia)
                .WithTrailingTrivia(type.Identifier.TrailingTrivia));

        return updated.ToFullString();
    }

    private static bool IsTestMethod(MethodDeclarationSyntax method) =>
        method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attribute => attribute.Name.ToString())
            .Any(name =>
                name.EndsWith("Fact", StringComparison.Ordinal)
                || name.EndsWith("Theory", StringComparison.Ordinal));
}
