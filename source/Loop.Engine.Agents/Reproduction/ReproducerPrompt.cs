using System.Text;
using Loop.Engine.Agents.Retrieval;
using Loop.Engine.Core.Model;

namespace Loop.Engine.Agents.Reproduction;

/// <summary>Builds the Reproducer's prompt. Pure and static so the instructions are assertable.</summary>
public static class ReproducerPrompt
{
    public const string System = """
        You write one xunit test that FAILS because a bug is present, and would pass once
        the bug is fixed. You do not fix the bug. Someone else does that afterwards, and
        your test is how they will know they succeeded.

        This is the whole job, so be precise about what it means. The test must fail
        **against the code you are shown**, for the reason described in the issue. A test
        that passes right now is worthless here: it will still pass after the fix, and the
        pull request will claim to prove something it never demonstrated.

        Write the test against the behaviour the issue describes, never against the buggy
        implementation. Assert what the code *should* do. Do not assert the current wrong
        result and do not write a test that merely calls the method and checks it does not
        throw.

        Prefer the smallest possible test. One arrange, one act, one assertion about the
        thing the issue complains about. No setup for scenarios the issue does not mention.

        **Make the failure deterministic.** Many bugs only misbehave under a particular
        condition — a working directory, an environment variable, a culture, a clock, a
        relative path, an empty collection. Your test must *establish that condition
        itself*, not hope the test host happens to provide it. A test that depends on where
        the runner was launched from will pass on the machine that wrote it and prove
        nothing anywhere else.

        Before you answer, state to yourself what the assertion evaluates to **today**,
        with the bug present. If the answer is "it passes", you have written the wrong
        test — find the input that actually distinguishes broken from fixed.

        Follow the conventions of the test files you are shown: framework, assertion style,
        naming, namespace, and file layout. If those files use a particular assertion
        library, use it — do not introduce another.

        Add a brief comment saying what the test proves and why it fails today. Whoever
        reads the pull request needs that sentence.

        # Output format

        Reply with exactly two things and nothing else — no commentary, no explanation:

        FILE: tests/Some.Project/TheBugTests.cs
        ```csharp
        the entire test file, verbatim
        ```

        Write the code exactly as it should appear on disk. Do not escape anything: a
        backslash is a backslash, a quote is a quote.

        The file must contain exactly one test method, marked `[Fact]` or `[Theory]`, in a
        namespaced class. It must compile on its own against the code shown.
        """;

    public static string User(
        Issue issue,
        AnalysisResult analysis,
        FixPlan plan,
        IReadOnlyList<RetrievedFile> files,
        IReadOnlyList<RetrievedFile> supportingTypes,
        IReadOnlyList<RetrievedFile> exemplars,
        string? testProject)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine($"# Issue #{issue.Number}: {issue.Title}");
        prompt.AppendLine();
        prompt.AppendLine(issue.Body);
        prompt.AppendLine();

        prompt.AppendLine("# What the investigation found");
        prompt.AppendLine();
        prompt.AppendLine(analysis.Symptoms);
        prompt.AppendLine();

        if (analysis.PossibleRootCauses.Count > 0)
        {
            prompt.AppendLine("Likely cause:");
            foreach (var cause in analysis.PossibleRootCauses)
            {
                prompt.AppendLine($"- {cause}");
            }

            prompt.AppendLine();
        }

        if (plan.Steps.Count > 0)
        {
            // The plan is context, not instructions: it says what the fix will change, so
            // the test can target that behaviour rather than guessing at it.
            prompt.AppendLine("# What the fix will change (for context — do not implement it)");
            prompt.AppendLine();
            foreach (var step in plan.Steps)
            {
                prompt.AppendLine($"{step.Order}. {step.Description}");
            }

            prompt.AppendLine();
        }

        prompt.AppendLine("# The code as it stands today, with the bug in it");
        prompt.AppendLine();

        foreach (var file in files)
        {
            AppendFile(prompt, file);
        }

        if (supportingTypes.Count > 0)
        {
            // The signatures the test has to satisfy. Without these the model guesses at
            // constructors it has never seen, and the gate rejects a test that fails to
            // compile for reasons unrelated to the bug.
            prompt.AppendLine("# Types you will need to construct, as they are actually declared");
            prompt.AppendLine();
            prompt.AppendLine(
                "Use these exact constructors, parameter names, and types. Do not guess at a " +
                "shape that looks reasonable — if a parameter is `string[]`, it is not a `List<string>`.");
            prompt.AppendLine();

            foreach (var file in supportingTypes)
            {
                AppendFile(prompt, file);
            }
        }

        if (exemplars.Count > 0)
        {
            prompt.AppendLine("# Existing tests, for conventions only");
            prompt.AppendLine();
            prompt.AppendLine(
                "Match the framework, assertion library, namespace, and naming style used here. " +
                "Do not copy what they test.");
            prompt.AppendLine();

            foreach (var file in exemplars)
            {
                AppendFile(prompt, file);
            }
        }

        if (!string.IsNullOrWhiteSpace(testProject))
        {
            prompt.AppendLine("# Where the test goes");
            prompt.AppendLine();
            prompt.AppendLine(
                $"The suite that will run it is `{testProject}`. Put the file inside that " +
                "project's directory and use its namespace.");
            prompt.AppendLine();
            prompt.AppendLine(
                "Name the file after the type under test: a bug in `SymbolExtractor` goes in " +
                "`SymbolExtractorTests.cs`. Never name the file after the project or after the " +
                "bug — `Loop.Engine.Tests.cs` and `TheBugTests.cs` are both wrong.");
            prompt.AppendLine();
        }

        prompt.AppendLine(
            "Write the one test that fails today because of this bug, and would pass once " +
            "it is fixed.");
        prompt.AppendLine();
        prompt.AppendLine(
            "Name it for the behaviour that should hold, not the behaviour that does. If the " +
            "bug is that single-word names are dropped, the test is " +
            "`Extract_returns_a_single_word_type_name` — never `..._does_not_return_...`, " +
            "which describes the defect you are supposed to be catching.");

        return prompt.ToString();
    }

    private static void AppendFile(StringBuilder prompt, RetrievedFile file)
    {
        prompt.AppendLine($"## {file.RelativePath}");
        prompt.AppendLine();
        prompt.AppendLine("```csharp");
        prompt.AppendLine(file.Excerpt);
        prompt.AppendLine("```");
        prompt.AppendLine();
    }
}
