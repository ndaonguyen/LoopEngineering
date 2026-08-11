using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Loop.Engine.Agents.Reproduction;

/// <summary>
/// The types a test would have to construct in order to call the code under test.
///
/// A reproduction test cannot be written against a method whose parameter types are unknown.
/// Two real runs failed on exactly this: <c>CS7036</c> for an <c>Issue</c> record the model
/// had never been shown, and <c>CS0029</c> for an option declared <c>string[]</c> that the
/// model guessed was a <c>List&lt;string&gt;</c>. Both look like the model writing careless
/// code; both were the model being asked to write against a signature it could not see.
///
/// Syntax only, like <c>SyntaxVerifier</c> — there is no compilation here, so this reads the
/// names that appear in signatures rather than resolving symbols.
/// </summary>
public static class SignatureTypes
{
    /// <summary>
    /// Types the framework supplies. Retrieving these would spend the file budget on files
    /// that are not in the repository, pushing out the one type the test actually needs.
    ///
    /// Deliberately short. It covers what appears in nearly every signature; anything rarer
    /// is cheaper to retrieve and ignore than to maintain a list about.
    /// </summary>
    private static readonly HashSet<string> Framework = new(StringComparer.Ordinal)
    {
        "String", "Int32", "Int64", "Boolean", "Object", "Void", "Char", "Byte", "Double",
        "Decimal", "Single", "Guid", "DateTime", "DateTimeOffset", "TimeSpan", "Uri", "Type",
        "Task", "ValueTask", "CancellationToken", "Exception", "Stream", "Nullable",
        "List", "IList", "IEnumerable", "ICollection", "IReadOnlyList", "IReadOnlyCollection",
        "Dictionary", "IDictionary", "IReadOnlyDictionary", "HashSet", "ISet", "Array",
        "Span", "ReadOnlySpan", "Memory", "ReadOnlyMemory", "Func", "Action", "Tuple",
        "IDisposable", "IAsyncDisposable", "IAsyncEnumerable", "Regex", "StringBuilder",
    };

    /// <summary>
    /// Type names appearing in the declared signatures of <paramref name="source"/> —
    /// parameters, return types, property types, and base types.
    ///
    /// Generic arguments are unwrapped, so <c>IReadOnlyList&lt;Issue&gt;</c> yields
    /// <c>Issue</c>: the interesting type is almost always the argument, not the container.
    /// </summary>
    public static IReadOnlyList<string> Extract(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return [];
        }

        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var found = new List<string>();

        foreach (var type in DeclaredTypes(root))
        {
            // One TypeSyntax can carry several names — IReadOnlyList<Issue> holds both, and
            // a qualified Core.Model.Issue holds each segment. Taking every identifier and
            // filtering afterwards is simpler than walking each shape by hand.
            foreach (var identifier in type.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            {
                Add(found, identifier.Identifier.Text);
            }
        }

        return found;
    }

    private static IEnumerable<TypeSyntax> DeclaredTypes(SyntaxNode root)
    {
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            yield return method.ReturnType;

            foreach (var parameter in method.ParameterList.Parameters)
            {
                if (parameter.Type is { } type)
                {
                    yield return type;
                }
            }
        }

        foreach (var constructor in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            foreach (var parameter in constructor.ParameterList.Parameters)
            {
                if (parameter.Type is { } type)
                {
                    yield return type;
                }
            }
        }

        // Positional records — the shape that produced the CS7036 above. The parameter list
        // hangs off the type declaration rather than a constructor node.
        foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            foreach (var parameter in declaration.ParameterList?.Parameters ?? [])
            {
                if (parameter.Type is { } type)
                {
                    yield return type;
                }
            }

            foreach (var baseType in declaration.BaseList?.Types ?? [])
            {
                yield return baseType.Type;
            }
        }

        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            yield return property.Type;
        }
    }

    private static void Add(List<string> found, string name)
    {
        if (name.Length < 3 || !char.IsUpper(name[0]) || Framework.Contains(name))
        {
            return;
        }

        if (!found.Contains(name, StringComparer.Ordinal))
        {
            found.Add(name);
        }
    }
}
