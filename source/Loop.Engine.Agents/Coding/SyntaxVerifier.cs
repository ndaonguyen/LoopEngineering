using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Loop.Engine.Agents.Coding;

/// <summary>
/// Checks that an edited C# file still <b>parses</b>.
///
/// Named for what it does. It is <i>not</i> a compile check: <c>class A { void M() {
/// Undefined(); } }</c> parses without a single error. Calls to methods that do not exist,
/// misspelled identifiers, and type mismatches all pass here and fail <c>dotnet build</c>.
/// Semantic diagnostics need a full <c>CSharpCompilation</c> with the reference set, which
/// is most of a build — and building is Phase 4's job, done once and properly, rather than
/// half-built here as well.
///
/// What this does buy: a mangled edit is caught immediately, at the point the Coder made
/// it, instead of surfacing as a confusing build failure a phase later.
/// </summary>
public static class SyntaxVerifier
{
    /// <param name="errors">Human-readable diagnostics, empty when the file parses.</param>
    public static bool Verify(string relativePath, string contents, out IReadOnlyList<string> errors)
    {
        if (!relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            errors = [];
            return true;
        }

        var diagnostics = CSharpSyntaxTree.ParseText(contents)
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Id} at line {d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage()}")
            .ToList();

        errors = diagnostics;
        return diagnostics.Count == 0;
    }
}
