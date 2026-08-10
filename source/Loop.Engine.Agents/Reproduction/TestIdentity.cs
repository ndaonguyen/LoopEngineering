using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Loop.Engine.Agents.Reproduction;

/// <summary>
/// Reads the fully-qualified name of the test out of the generated source.
///
/// Asked of the model instead, this would be a second, unverifiable claim about a file the
/// model also wrote — and a name that does not match anything makes <c>dotnet test</c> exit
/// non-zero, which is indistinguishable from the test failing. That reads as a successful
/// reproduction. Roslyn already parses this file for the syntax check, so the name is taken
/// from the syntax tree, where it cannot disagree with the code.
/// </summary>
public static class TestIdentity
{
    /// <summary>
    /// The first xunit test method in the file, as <c>Namespace.Class.Method</c>.
    /// Null when the source has no test method — which is itself a rejection, not a detail.
    /// </summary>
    public static string? TryReadFullyQualifiedName(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var root = CSharpSyntaxTree.ParseText(source).GetRoot();

        var method = root
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(IsTestMethod);

        if (method is null)
        {
            return null;
        }

        var type = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();

        if (type is null)
        {
            return null;
        }

        // Nested classes are joined with '+' by the runner, but a generated reproduction is
        // a single top-level class; walking outward keeps a nested one from silently
        // producing a name that matches nothing.
        var names = method.Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .Select(t => t.Identifier.Text)
            .Reverse()
            .ToList();

        var ns = method.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .Reverse();

        var parts = ns.Concat(names).Append(method.Identifier.Text);

        return string.Join('.', parts);
    }

    private static bool IsTestMethod(MethodDeclarationSyntax method) =>
        method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attribute => attribute.Name.ToString())
            .Any(name =>
                name.EndsWith("Fact", StringComparison.Ordinal)
                || name.EndsWith("Theory", StringComparison.Ordinal));
}
